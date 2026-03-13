# Carrot.Memory

一个基于分页机制的高性能二维内存容器，支持动态行增长和极速的行列切片访问。

## 特性

- **分页存储**：使用分页机制管理底层内存，避免大面积连续内存分配。
- **动态增长**：按需自动分配新页面，支持极大规模数据。
- **零拷贝视图**：提供 `PagedView<T>` (ref struct)，支持对行（Slice）和列（Series）的无损切片访问。
- **高性能**：利用位运算、内联优化和 `CommunityToolkit.HighPerformance` 提升处理速度。

## 快速开始

### 1. 初始化容器

初始化时通过 `width` 指定固定列宽，通过 `pageSize` 指定每页的行数（必须是 2 的幂）。

```csharp
using Carrot.Memory;

// 创建一个每行 100 列，每页 1024 行的容器
var pagedMemory = new PagedMemory2D<double>(width: 100, pageSize: 1024);
```

### 2. 添加数据

支持单行添加或批量批量添加（基于 `ReadOnlySpan2D`）。

```csharp
// 添加单行
double[] rowData = new double[100];
pagedMemory.AddRow(rowData);

// 批量添加多行
double[,] multiRows = new double[50, 100];
pagedMemory.AddRows(multiRows.AsSpan2D());
```

### 3. 数据访问

支持通过索引器访问单个元素，或获取行/列切片。

```csharp
// 索引访问
ref double val = ref pagedMemory[0, 5];

// 获取行切片（第 10 行，第 5 列开始，长度 10）
var rowSlice = pagedMemory.GetSlice(10, 5, 10);
Span<double> span = rowSlice.AsSpan();

// 获取跨页的列切片（第 500 行开始，第 5 列，垂直方向取 2000 个元素）
var colSeries = pagedMemory.GetSeries(500, 5, 2000);
double v = colSeries[1500]; // 跨越物理页面边界访问
```

## 许可证

Apache License 2.0

---
*本README由 Gemini 3 Flash 生成*
