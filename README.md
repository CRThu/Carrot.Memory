# Carrot.Memory

[![License](https://img.shields.io/badge/license-Apache%202.0-blue.svg)](LICENSE)

一个基于分页机制的高性能二维内存容器，专为大规模数据处理、多线程并发访问（MWMR）以及高安全性场景设计。

## 核心特性

- **分页存储**：使用分页机制管理底层内存，避免 LOH (Large Object Heap) 碎片，支持 2 的幂次页大小优化。
- **并发安全 (MWMR)**：内置 `ReaderWriterLockSlim` 与 `Volatile` 屏障，支持多线程并发读写与动态原子扩容。
- **零拷贝视图**：提供 `ReadOnlyPagedView<T>` 与 `PagedView<T>`，支持对行（Row）和列（Column）的无损切片访问。
- **存储扩展 (Provider)**：通过 `IPageProvider<T>` 接口，支持将后端映射到堆内存、非托管内存、**磁盘二进制文件 (Heap-Cache)** 或 **内存映射文件 (MMF)**。
- **持久化同步**：支持元数据 JSON 与二进制分页文件的自动同步与状态恢复。
- **零拷贝映射 (MMF)**：`MmfPageProvider` 通过非托管内存管理器将磁盘文件直接映射为容器内存，实现极致的 I/O 吞吐。

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

### 4. 数据持久化与高性能存储

本项目提供两种主要的持久化方案，均支持元数据（Metadata）的自动恢复：

#### A. 堆缓存模式 (Heap-Cache Mode)
使用 `FilePersistentHeapProvider`。数据驻留在托管堆中作为高速缓存，通过 `FlushAll()` 将脏页同步到磁盘。适用于常规持久化需求。

```csharp
// 初始化堆缓存持久化供应者
var provider = new FilePersistentHeapProvider<int>("my_database");

// 创建容器时传入供应者，将自动加载已有元数据
using var paged = new PagedMemory2D<int>(width: 10, pageSize: 1024, provider);

// 写入后手动持久化
paged.FlushAll();
```

#### B. 存储映射模式 (MMF Mode)
使用 `MmfPageProvider`。利用操作系统内存映射（Memory-Mapped Files）技术，将磁盘文件直接视为内存。

- **优势**：无内存拷贝开销、支持处理远超 RAM 大小的海量数据、系统崩溃后数据更安全。
- **限制**：仅支持 `unmanaged` 类型。

```csharp
// 初始化 MMF 供应者
var provider = new MmfPageProvider<int>("mmf_data");

// 容器操作直接作用于磁盘映射的虚拟内存
using var paged = new PagedMemory2D<int>(width: 10, pageSize: 1024, provider);

paged.SetElement(5, 5, 999);
paged.FlushAll(); // 强制执行 OS 物理页同步
```

## 线程安全协议

本库遵循 **MWMR (Multi-Writer Multi-Reader)** 协议：
- **读取**：完全并发，支持 `ref readonly` 索引器与视图读取。
- **写入**：受内置写锁保护，但在修改现有数据元素时（通过 `ref T`），应确保应用层的同步。
- **扩容**：写入操作会自动触发原子扩容，对读取线程透明且安全。

## 性能表现 (Performance)

以下是在 Windows 环境下对 `Carrot.Memory` 进行的性能基准测试结果。

**测试环境：**
- **硬件环境**：Intel Core i5-6400 CPU 2.70GHz (Skylake)
- **平台版本**：.NET 10.0.3 / Windows 10
- **分页规模**：1,024 行 (2^10)，单次 Ops 操作涉及 10,240,000 个元素
- **对比目标**：
  - **Baseline**: 原生 `int[,]` 二维数组。
  - **Heap Mode**: `PagedMemory2D` + `DefaultHeapPageProvider`。
  - **MMF Mode**: `PagedMemory2D` + `MmfPageProvider` (在 OS Page Cache 命中的热状态下)。

| 测试维度 (10M Ops) | Array2D (ms) | PagedHeap (ms) | MMF Mode (ms) | 结论 |
| :--- | :---: | :---: | :---: | :--- |
| **顺序行访问 (Row Access)** | 13.43 | 5.42 | 5.40 | **Paged 胜出** (~2.5x 提速，受益于 Span 遍历优化) |
| **随机索引访问 (Random Access)** | 3.83 | 4.53 | - | **Array 略快** (Paged 存在寻址与位运算开销) |
| **大块数据拷贝 (Block Transfer)** | 22.42 | 8.65 | - | **Paged 胜出** (~2.6x 提速，局部性表现更佳) |
| **页内列访问 (Col InPage)** | 7.29 | 7.78 | 2.88 | **MMF 胜出** (页内列访问 MMF 具有极高性能) |
| **全量列遍历 (Full Col)** | 104.61 | 406.39 | 364.87 | **Array 胜出** (跨页逻辑存在接口跳转开销) |

**数据解读：**
1. **极致吞吐**：在顺序遍历场景下，`PagedMemory2D` 通过 `GetRowView().AsSpan()` 暴露连续内存，性能优于原生 .NET 二维数组，因为后者在每个索引访问上都有受限的边界检查优化。
2. **MMF 零损耗**：在数据预热后（Page Cache 命中），`MMF Mode` 的访问速度与堆内存（Heap Mode）完全持平，甚至在页内列访问等场景下表现更优。
3. **权衡取舍**：列访问在跨物理页时（Column Access）由于涉及接口回调和位移计算，性能会有所下降。对于极致性能要求的列式处理，建议按页分段获取 Span 处理。
4. **超大规模优势**：PagedMemory 的真正优势在于能够打破单体大对象（LOH）限制，减轻 GC 堆压力，并支持透明的磁盘持久化扩展。


## 许可证

Apache License 2.0

---
*本README由 Gemini 3 Flash 生成*
