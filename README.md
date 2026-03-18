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

以下是在 Windows 环境下对 `Carrot.Memory` 进行的 1GB 级性能基准测试结果。

**测试环境：**
- **硬件环境**：Intel Core i5-6400 CPU 2.70GHz (Skylake)
- **平台版本**：.NET 10.0.3 / Windows 10
- **测试规模**：**1GB (256,000,000+ Int32 元素)**
- **分页配置**：单页大小 256MB，分页规模 2^16 行
- **对比目标**：
  - **Baseline**: 原生 `int[,]` 二维数组。
  - **Heap Mode**: `PagedMemory2D` + `DefaultHeapPageProvider`。
  - **MMF Mode**: `PagedMemory2D` + `MmfPageProvider` (在 OS Page Cache 命中的热状态下)。

| 测试维度 (1GB Scale) | Array2D | PagedHeap | MMF Mode | 结论 |
| :--- | :---: | :---: | :---: | :--- |
| **冷启动恢复 (Cold Start)** | 234.7 ms | 409.9 ms | **1.48 ms** | **MMF 完胜** (近乎瞬态的数据恢复能力) |
| **顺序行访问 (Row Sum)** | 362.4 ms | **137.7 ms** | **138.5 ms** | **Paged 胜出** (~2.6x 提速，受益于 Span 遍历) |
| **页内列访问 (Col InPage)** | **789.8 ms** | 1,101.2 ms | 1,124.7 ms | **Array 胜出** |
| **跨页列访问 (Col CrossPage)** | **183.3 ms** | 584.2 ms | 655.4 ms | **Array 胜出** (跨页逻辑存在寻址开销) |
| **全量列遍历 (Col Full)** | **3,155.3 ms** | 10,175.0 ms | 8,743.9 ms | **Array 胜出** |
| **随机索引访问 (Random)** | **29.11 ns** | 41.61 ns | 41.40 ns | **Array 略快** (Paged 存在分页寻址开销) |
| **大块数据写入 (SetBlock)** | 40.77 μs | 3.89 μs | **3.50 μs** | **Paged 胜出** (~10x 提速，写入稳定性极佳) |


**数据解读：**
1. **大规模提速**：在 1GB 级数据量级下，`PagedMemory2D` 的顺序遍历性能显著优于原生 `Array2D`（约 2.6x），这得益于对内存页布局的优化和 Span 的应用。
2. **MMF 零损耗**：`MMF Mode` 在数据预热后（Page Cache 命中）与 `Heap Mode` 性能几乎一致，但在持久化恢复场景下具有绝对优势。
3. **写入爆发力**：`SetBlock` 性能在 Paged 模式下相比原生数组有数量级的提升（~10x），展示了分页机制下内存复制的局部性优势。
4. **瞬时恢复**：MMF 映射现有文件仅需 **1.48 ms**，而堆内存重新加载同样大小的文件需要 409.9 ms，这在海量数据处理系统重启时具有决定性意义。
5. **超大规模优势**：PagedMemory 的真正优势在于能够打破单体大对象（LOH）限制，减轻 GC 堆压力，并支持透明的磁盘持久化扩展。



## 许可证

Apache License 2.0

---
*本README由 Gemini 3 Flash 生成*
