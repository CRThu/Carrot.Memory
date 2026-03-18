using System;
using System.IO;
using BenchmarkDotNet.Attributes;
using Carrot.Memory;

namespace Carrot.Memory.Benchmarks
{
    [MemoryDiagnoser]
    [SimpleJob(iterationCount: 3, warmupCount: 1, invocationCount: 1)] 
    [Config(typeof(BenchmarkConfig))]
    public class ColdStartBenchmarks
    {
        private const int _width = BenchmarkConfig.Cols;
        private const int _pageSize = BenchmarkConfig.PageSize;
        private const int _totalRows = BenchmarkConfig.TotalRows;

        private string _mmfPath;
        private string _initTestDataPath;

        [GlobalSetup]
        public void Setup()
        {
            _mmfPath = Path.Combine(Path.GetTempPath(), "Carrot_Bench_MMF_Cold_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_mmfPath);

            _initTestDataPath = Path.Combine(Path.GetTempPath(), "Carrot_Bench_InitData_Cold_" + Guid.NewGuid().ToString("N"));
            using (var fs = new FileStream(_initTestDataPath, FileMode.Create, FileAccess.Write))
            {
                fs.SetLength((long)_totalRows * _width * sizeof(int));
            }
        }

        [GlobalCleanup]
        public void Cleanup()
        {
            if (Directory.Exists(_mmfPath)) try { Directory.Delete(_mmfPath, true); } catch { }
            if (File.Exists(_initTestDataPath)) try { File.Delete(_initTestDataPath); } catch { }
        }

        [Benchmark(Baseline = true)]
        public void Array_Alloc()
        {
            var arr = new int[_totalRows, _width];
            _ = arr[0, 0];
        }

        [Benchmark]
        public void Heap_Init_Empty()
        {
            using var mem = new PagedMemory2D<int>(_width, _pageSize, new DefaultHeapPageProvider<int>());
        }

        [Benchmark]
        public void Heap_Load_Disk()
        {
            using var mem = new PagedMemory2D<int>(_width, _pageSize, new DefaultHeapPageProvider<int>());
            using var fs = File.OpenRead(_initTestDataPath);
            byte[] buffer = new byte[_pageSize * _width * sizeof(int)];
            for (int p = 0; p < _totalRows / _pageSize; p++)
            {
                fs.Read(buffer, 0, buffer.Length);
            }
        }

        [Benchmark]
        public void MMF_Map_Existing()
        {
            using var mem = new PagedMemory2D<int>(_width, _pageSize, new MmfPageProvider<int>(_mmfPath));
        }
    }
}
