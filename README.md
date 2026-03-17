# Carrot.Memory

[![License](https://img.shields.io/badge/license-Apache%202.0-blue.svg)](LICENSE)

一个基于分页机制的高性能二维内存容器，专为大规模数据处理、多线程并发访问（MWMR）以及高安全性场景设计。

## 核心特性

- **分页存储**：使用分页机制管理底层内存，避免 LOH (Large Object Heap) 碎片，支持 2 的幂次页大小优化。
- **并发安全 (MWMR)**：内置 `ReaderWriterLockSlim` 与 `Volatile` 屏障，支持多线程并发读写与动态原子扩容。
- **零拷贝视图**：提供 `ReadOnlyPagedView<T>` 与 `PagedView<T>`，支持对行（Row）和列（Column）的无损切片访问。
- **存储扩展 (Provider)**：通过 `IPageProvider<T>` 接口，支持将后端映射到堆内存、非托管内存或 **磁盘二进制文件 (FilePersistence)**。
- **持久化同步**：支持元数据 JSON 与二进制分页文件的自动同步与状态恢复。
- **完全无锁刷新**：`FlushAll` 操作采用快照机制，在执行持久化时无需阻塞任何读写操作。

## 快速开始

### 1. 初始化容器

```csharp
using Carrot.Memory;

// 创建一个每行 100 列，每页 1024 行的容器
// 默认使用堆内存分配器 (DefaultHeapPageProvider)
using var pagedMemory = new PagedMemory2D<int>(width: 100, pageSize: 1024);
```

### 2. 写入数据

```csharp
// 单元素设置
pagedMemory.SetElement(0, 0, 42);

// 批量设置单行 (Row)
int[] rowData = new int[100];
pagedMemory.SetRow(5, 0, rowData);

// 批量设置二维块 (Block)
int[,] blockData = new int[10, 10];
pagedMemory.SetBlock(10, 10, blockData.AsSpan2D());
```

### 3. 数据访问与视图

```csharp
// 获取只读接口
IReadonlyPagedMemory2D<int> readOnlyView = pagedMemory.AsReadOnly();

// 编译错误：readOnlyView[0, 0] = 99; 
ref readonly int val = ref readOnlyView[0, 0];

// 获取行视图 (RowView)
var rowView = readOnlyView.GetRowView(row: 5, col: 0, len: 10);
ReadOnlySpan<int> span = rowView.AsSpan();

// 获取跨页的列视图 (ColumnView)
// 支持垂直方向跨越多个物理页面进行统一访问
var colView = readOnlyView.GetColumnView(row: 500, col: 5, len: 2000);
int v = colView[1500]; 
```

### 4. 数据持久化 (File Persistence)

使用 `FilePersistentHeapProvider` 实现数据的自动分页存储与恢复：

```csharp
// 初始化持久化供应者，指定存储目录
var provider = new FilePersistentHeapProvider<int>("my_database");

// 创建容器时传入供应者，将自动加载已有元数据与数据页
using var paged = new PagedMemory2D<int>(width: 10, pageSize: 1024, provider);

// 写入数据
paged.SetElement(10, 0, 123);

// 手动持久化：同步所有内存页至磁盘，并更新 metadata.json
paged.FlushAll();
```

## 线程安全协议

本库遵循 **MWMR (Multi-Writer Multi-Reader)** 协议：
- **读取**：完全并发，支持 `ref readonly` 索引器与视图读取。
- **写入**：受内置写锁保护，但在修改现有数据元素时（通过 `ref T`），应确保应用层的同步。
- **扩容**：写入操作会自动触发原子扩容，对读取线程透明且安全。

## 许可证

Apache License 2.0

---
*本README由 Gemini 3 Flash 生成*
