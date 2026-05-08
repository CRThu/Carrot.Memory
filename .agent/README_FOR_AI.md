# Carrot.Memory AI 协作指南

## 核心设计协议

### 1. 双视图隔离 (ReadOnly/Writable Isolation)
为了确保内存安全性并利用 C# 7.2+ 的 `ref readonly` 特性，本项目采用双视图模型，并使用静态视图类型替代动态模式以消除运行时分支开销：
- **只读层级 (`IReadonlyPagedMemory2D<T>`)**：
  - 索引器返回 `ref readonly T`，禁止通过该接口进行任何修改。
  - `GetRowView` 返回 `ReadOnlyPagedRowView<T>`，直接包装 `ReadOnlySpan<T>`。
  - `GetColumnView` 返回 `ReadOnlyPagedColumnView<T>`，封装行列索引寻址。
  - 所有只读视图内部索引器同样受 `ref readonly` 保护。
- **读写层级 (`IPagedMemory2D<T>` / `PagedMemory2D<T>`)**：
  - 索引器返回 `ref T`，允许高性能的原地修改。
  - `GetRowView` 返回 `PagedRowView<T>`，支持对底层 `Span<T>` 的直接写操作。
  - `GetColumnView` 返回 `PagedColumnView<T>`，支持跨页的垂直写操作。
  - 具象类 `PagedMemory2D<T>` 通过显式接口实现来隔离只读路径。

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
通过 `IPersistentPageProvider<T>` 协议实现状态同步：
- **职责清晰**：该接口统一管理物理页刷新（Flush）与容器元数据（Metadata）的保存还原。
- **接口集成**：`PagedMemory2D` 在构造时通过类型检测自动恢复逻辑行数，在 `Commit()` 时同步数据与状态。
- **高效零开销**：对于非持久化供应者（如默认堆分配），`Commit()` 会跳过持久化逻辑，仅在必要时抛出异常。
- **原子性与顺序**：保证“先数据页、后元数据”的顺序。元数据保存采用原子写入策略（.tmp 转换），确保系统崩溃重启后逻辑状态不损坏。
- **生命周期保障**：`Dispose` 时会自动调用 `Commit()`，确保即使未手动同步，数据也能在容器关闭时安全入盘。
- **类型约束**：虽然接口本身不强制 `unmanaged`，但基于二进制实现的供应者（如文件持久化）应自行施加约束。

### 4. MMF 存储协议 (Memory-Mapped Storage Protocol)
`MmfPageProvider<T>` 采用超高性能的进程外持久化方案：
- **零拷贝映射**：利用 `UnmanagedMemoryManager<T>` 将非托管内存直接暴露为 `Memory2D<T>`，所有写操作直连 OS 虚拟内存页，无需内存/磁盘间反复拷贝。
- **预扩容策略**：创建页面时会根据 `pageSize * width * sizeof(T)` 自动调用 `fs.SetLength` 进行物理扩容，防止映射视图越界并保证物理连续性。
- **显式同步**：虽然 OS 会自动定时刷盘，但 `Flush` 调用会强制执行视图同步（`MemoryMappedViewAccessor.Flush`），确保高优先级数据的持久性。
- **句柄管理**：页面句柄由 Provider 统一管理并伴随容器 `Dispose` 释放。
