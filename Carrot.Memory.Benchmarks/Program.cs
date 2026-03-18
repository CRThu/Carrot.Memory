using BenchmarkDotNet.Running;
using Carrot.Memory.Benchmarks;

// 性能评测 - 深度分拆版 (V10.6)
// 依次运行所有 1GB 规模的测试组

BenchmarkRunner.Run<ColdStartBenchmarks>();
BenchmarkRunner.Run<RowReadBenchmarks>();
BenchmarkRunner.Run<ColumnReadBenchmarks>();
BenchmarkRunner.Run<RandomReadBenchmarks>();
BenchmarkRunner.Run<BulkWriteBenchmarks>();
