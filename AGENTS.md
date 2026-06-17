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
- **显式路径**：持久化路径由调用方显式传入，不再从 Options 或构造函数隐式获取。
- **线程安全**：通过 `ReaderWriterLockSlim` 的读锁保护，确保在提交过程中容器结构（如行数）保持稳定。
- **原子性保证**：依次调用 `Provider.Flush()` 与 `MetadataManager.Save(path, options)`，确保"物理同步 -> 逻辑保存"的正确顺序。
- **生命周期解耦**：`Dispose()` 不再自动调用 `Commit()`，避免在纯内存场景下产生意外的磁盘 IO。

### 3. 创建与恢复分离 (Create vs Open)
工厂层提供两条语义明确的路径：
- **`Buffer2D.Create<T>(width, rowCount)`**：纯内存创建，零磁盘依赖。使用 `HeapProvider`，适用于回测等一次性加载场景。
- **`Buffer2D.Open<T>(path)`**：从磁盘恢复。通过 `MetadataManager.Load` 读取元数据，自动选择对应的 Provider 重建容器状态。

## 编程约束
- 除非性能极其敏感且已正确处理锁，否则建议通过 `SetElement` / `SetBlock` 等受保护方法写入。
- 外部消费只需读取时，应通过 `.AsReadOnly()` 获取只读接口，利用编译器检查防止误写。
- **命名规范**：视图方法统一命名为 `GetRowView` 和 `GetColumnView` 以提升可读性。

### 4. 持久化扩展 (Persistence Extension)
通过 **能力接口 + 组合模式** 实现状态同步：
- **职责解耦**：`PagedBuffer2D` 容器实现 `IPersistable` (Commit)，负责逻辑状态提交；Provider 实现 `IFlushable` (Flush)，负责物理页同步。
- **依赖注入初始化**：`PagedBuffer2D` 构造函数不再包含任何 IO 操作（如加载元数据），仅负责逻辑组件的组装。配置解析、元数据加载及 Provider 选型逻辑全部上移至 `Buffer2D`。
- **状态恢复**：容器通过构造函数接收 `initialRowCount` 参数，实现从持久化元数据的平滑恢复。
- **原子提交**：容器的 `Commit()` 依次调用 `MetadataManager.Save(path, options)` 与 `Provider.Flush()`。这种分层确保了"逻辑保存 -> 物理同步"的正确顺序。
- **线程安全约束**：Provider 内部管理页面引用的集合必须是线程安全的（如 `ConcurrentDictionary`），以配合容器层的分层锁模型。

### 5. MMF 存储协议 (Memory-Mapped Storage Protocol)
`MmfPageProvider<T>` 采用超高性能的进程外持久化方案：
- **零拷贝映射**：利用 `UnmanagedMemoryManager<T>` 将非托管内存直接暴露为 `Memory2D<T>`，所有写操作直连 OS 虚拟内存页，无需内存/磁盘间反复拷贝。
- **预扩容策略**：创建页面时会根据物理计算进行预扩容，并执行严格的文件大小校验，防止数据布局损坏。
- **显式同步**：`Flush` 调用会强制执行视图同步（`MemoryMappedViewAccessor.Flush`），确保高优先级数据的持久性。

### 6. 工厂初始化协议 (Factory Initialization Protocol)
`Buffer2D` 是统一入口，提供创建、恢复与扩展能力：
- **`Buffer2D.Create<T>(width, rowCount, pageSize)`**：纯内存创建，使用 `HeapProvider`，零磁盘依赖。适用于回测、一次性计算等场景。
- **`Buffer2D.Open<T>(path)`**：从磁盘恢复。通过 `MetadataManager.Load<Buffer2DOptions>` 探测目标路径并加载配置。
- **注册表模式 (Registry Pattern)**：所有的 Provider 工厂方法均注册在内置的静态泛型注册表 `ProviderRegistry<T>` 中。通过 `options.ProviderKey` 字符串作为键进行匹配，从而彻底消除了基于硬编码 `switch-case` 的类型解析。
- **自定义扩展 (Extension)**：AI 执行者在新增存储介质时，无需修改核心库，只需在初始化前调用 `Buffer2D.RegisterProvider<T>("NewProvider", path => new NewProvider<T>(path))` 注入即可。
- **纯净构造**：工厂负责编排所有外部资源（Provider、Options、Metadata），最后注入 `PagedBuffer2D` 构造函数，使其保持为纯内存逻辑组件。
