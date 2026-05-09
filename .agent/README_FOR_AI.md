# Carrot.Memory AI 协作指南

## 核心设计协议

### 1. 双视图隔离 (ReadOnly/Writable Isolation)
为了确保内存安全性并利用 C# 7.2+ 的 `ref readonly` 特性，本项目采用双视图模型，并使用静态视图类型替代动态模式以消除运行时分支开销：
- **只读层级 (`IReadOnlyBuffer2D<T>`)**：
  - 索引器返回 `ref readonly T`，禁止通过该接口进行任何修改。
  - `GetRowView` 返回 `ReadOnlyRowView<T>`，直接包装 `ReadOnlySpan<T>`。
  - `GetColumnView` 返回 `ReadOnlyColumnView<T>`，封装行列索引寻址。
  - 所有只读视图内部索引器同样受 `ref readonly` 保护。
- **读写层级 (`IBuffer2D<T>` / `PagedBuffer2D<T>`)**：
  - 索引器返回 `ref T`，允许高性能的原地修改。
  - `GetRowView` 返回 `RowView<T>`，支持对底层 `Span<T>` 的直接写操作。
  - `GetColumnView` 返回 `ColumnView<T>`，支持跨页的垂直写操作。
  - 具象类 `PagedBuffer2D<T>` 通过显式接口实现来隔离只读路径。

### 2. 同步提交边界 (Commit Boundary)
`Commit()` 操作确保数据状态与物理存储的一致性：
- **线程安全**：通过 `ReaderWriterLockSlim` 的读锁保护，确保在提交过程中容器结构（如行数）保持稳定。
- **快照机制**：获取当前页面数组引用，确保遍历过程的安全。
- **原子性保证**：采用“先数据页刷新、后元数据保存”的严格顺序，且元数据保存使用“临时文件 + 覆盖”的原子操作。

## 编程约束
- 除非性能极其敏感且已正确处理锁，否则建议通过 `SetElement` / `SetBlock` 等受保护方法写入。
- 外部消费只需读取时，应通过 `.AsReadOnly()` 获取只读接口，利用编译器检查防止误写。
- **命名规范**：视图方法统一命名为 `GetRowView` 和 `GetColumnView` 以提升可读性。

### 3. 持久化扩展 (Persistence Extension)
通过 **能力接口 + 组合模式** 实现状态同步：
- **职责解耦**：`PagedBuffer2D` 容器实现 `IPersistable` (Commit)，负责逻辑状态提交；Provider 实现 `IFlushable` (Flush)，负责物理页同步。
- **状态加载**：`PagedBuffer2D` 构造函数直接调用 `MetadataManager.Load` 从 `rootPath` 恢复逻辑行数，不再依赖 Provider 提供加载逻辑。
- **原子提交**：容器的 `Commit()` 依次调用 `Provider.Flush()` 与 `MetadataManager.Save()`。这种分层确保了“物理同步 -> 逻辑保存”的正确顺序。
- **线程安全约束**：Provider 内部管理页面引用的集合必须是线程安全的（如 `ConcurrentDictionary`），以配合容器层的分层锁模型。
- **生命周期保障**：`Dispose` 时会自动调用 `Commit()`，确保即使未手动同步，数据也能在容器关闭时安全入盘。

### 4. MMF 存储协议 (Memory-Mapped Storage Protocol)
`MmfPageProvider<T>` 采用超高性能的进程外持久化方案：
- **零拷贝映射**：利用 `UnmanagedMemoryManager<T>` 将非托管内存直接暴露为 `Memory2D<T>`，所有写操作直连 OS 虚拟内存页，无需内存/磁盘间反复拷贝。
- **预扩容策略**：创建页面时会根据物理计算进行预扩容，并执行严格的文件大小校验，防止数据布局损坏。
- **显式同步**：`Flush` 调用会强制执行视图同步（`MemoryMappedViewAccessor.Flush`），确保高优先级数据的持久性。

### 5. 工厂初始化协议 (Factory Initialization Protocol)
`PagedBuffer2DFactory.Open<T>` 是推荐的生命周期管理方式：
- **探测 (Probe)**：通过 `MetadataManager.Load` 探测目标路径。
- **恢复 (Restore)**：若存在元数据，根据其记录的 `ProviderType` 自动实例化正确的供应者，并委托 `PagedBuffer2D` 恢复状态。
- **新建 (Create)**：若不存在，根据 `PagedBuffer2DOptions` 初始化，默认使用 `MMF` 策略。
- **灵活性**：工厂仅负责 Provider 的选型，核心逻辑状态的维护由容器主类闭环处理。
