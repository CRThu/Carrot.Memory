# Carrot.Memory

[![License](https://img.shields.io/badge/license-Apache%202.0-blue.svg)](LICENSE)

一个基于分页机制的高性能二维内存容器，专为大规模数据处理、多线程并发访问（MWMR）以及高安全性场景设计。

## 核心特性

- **分页存储**：使用分页机制管理底层内存，避免 LOH (Large Object Heap) 碎片，支持 2 的幂次页大小优化。
- **并发安全 (MWMR)**：内置 `ReaderWriterLockSlim` 与 `Volatile` 屏障，支持多线程并发读写与动态原子扩容。
- **零拷贝视图**：提供 `PagedRowView<T>` / `PagedColumnView<T>` 及其只读版本，支持对行（Row）和列（Column）的高性能无损切片访问。
- **解耦持久化**：采用 **Provider + MetadataManager** 的组合模式。Provider 专注于物理 IO（MMF、文件流等），`MetadataManager` 统一处理 JSON 元数据持久化。
- **原子提交**：通过 `Commit()` 方法（`IPersistable` 接口）执行“物理页刷新 + 临时文件重命名”策略，确保元数据与数据页同步的原子性。

## 快速开始

### 1. 初始化容器

```csharp
using Carrot.Memory;

// 推荐方式：直接通过 PagedMemory 工厂打开目录
// 工厂会自动探测 metadata.json 并恢复对应的 Provider (MMF 或 FileHeap) 与容器规模
using var pagedMemory = PagedMemory.Open<int>("data_path", new PagedMemoryOptions 
{ 
    Width = 100, 
    PageSize = 1024 
});
```

### 2. 写入数据

```csharp
// 直接索引写入
pagedMemory[0, 0] = 42;

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
// 获取只读视图
IReadonlyPagedMemory2D<int> readOnlyView = pagedMemory.AsReadOnly();

// 获取行视图 (ReadOnlyPagedRowView)
var rowView = readOnlyView.GetRowView(row: 5, col: 0, len: 10);
ReadOnlySpan<int> span = rowView.AsSpan();

// 获取跨页的列视图 (ReadOnlyPagedColumnView)
// 封装了行列索引寻址逻辑，支持垂直方向跨越多个物理页面进行统一访问
var colView = readOnlyView.GetColumnView(row: 500, col: 5, len: 2000);
int v = colView[1500]; 
```

### 4. 提交持久化

```csharp
pagedMemory.Commit(); // 触发物理同步并原子化保存元数据
```

## 存储持久化详解

本项目通过 `MetadataManager` 管理 `metadata.json`，支持以下持久化介质：

- **MmfPageProvider (默认)**：基于内存映射文件，利用 OS 页面交换实现零拷贝 IO。
- **FilePersistentHeapProvider**：基于托管堆缓存，同步时将数据序列化到二进制文件。

```csharp
// 若需手动指定 Provider，可在 PagedMemory2D 构造函数中注入
var provider = new FilePersistentHeapProvider<int>("my_path");
var paged = new PagedMemory2D<int>(1024, 1024, provider, "my_path");
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

| 测试维度 (Per Op) | Array2D | PagedHeap | MMF Mode | 结论 |
| :--- | :---: | :---: | :---: | :--- |
| **冷启动恢复 (Cold Start)** | 235.2 ms | 406.6 ms | **0.86 ms** | **MMF 完胜** (近乎瞬态的数据恢复能力) |
| **顺序行访问 (Row Sum)** | 1.25 ns | **0.48 ns** | **0.47 ns** | **Paged 胜出** (~2.6x 提速，受益于 Span 遍历) |
| **页内列访问 (Col InPage)** | **11.00 ns** | 15.47 ns | 15.59 ns | **Array 胜出** |
| **跨页列访问 (Col CrossPage)** | **10.34 ns** | 33.15 ns | 37.76 ns | **Array 胜出** (跨页逻辑存在寻址开销) |
| **全量列遍历 (Col Full)** | **10.99 ns** | 36.32 ns | 37.38 ns | **Array 胜出** |
| **随机索引访问 (Random)** | **26.62 ns** | 36.98 ns | 37.03 ns | **Array 略快** (Paged 存在分页寻址开销) |
| **大块数据写入 (SetBlock)** | 2.33 ns | 0.22 ns | **0.20 ns** | **Paged 胜出** (~11x 提速，写入稳定性极佳) |


**数据解读：**
1. **大规模提速**：在 1GB 级数据量级下，`PagedMemory2D` 的顺序遍历性能显著优于原生 `Array2D`（约 2.6x），这得益于对内存页布局优化和 Span 的应用。
2. **MMF 零损耗**：`MMF Mode` 在数据预热后（Page Cache 命中）与 `Heap Mode` 性能几乎一致，但在持久化恢复场景下具有绝对优势。
3. **写入爆发力**：`SetBlock` 性能在 Paged 模式下相比原生数组有数量级的提升（~11x），展示了分页机制下内存复制的局部性优势。
4. **瞬时恢复**：MMF 映射现有文件仅需 **0.86 ms**，而堆内存重新加载同样大小的文件需要 406.6 ms，这在海量数据处理系统重启时具有决定性意义。
5. **超大规模优势**：PagedMemory 的真正优势在于能够打破单体大对象（LOH）限制，减轻 GC 堆压力，并支持透明的磁盘持久化扩展。



## 许可证

Apache License 2.0

---
*本README由 Gemini 3 Flash 生成*
