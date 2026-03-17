using BenchmarkDotNet.Running;
using Carrot.Memory.Benchmarks;

var summary = BenchmarkRunner.Run<AccessBenchmarks>();
