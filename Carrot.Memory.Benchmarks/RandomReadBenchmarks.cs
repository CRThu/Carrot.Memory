using System;
using System.IO;
using BenchmarkDotNet.Attributes;
using Carrot.Memory;

namespace Carrot.Memory.Benchmarks
{
    [MemoryDiagnoser]
    [Config(typeof(BenchmarkConfig))]
    public class RandomReadBenchmarks
    {
        private const int _width = BenchmarkConfig.Cols;
        private const int _pageSize = BenchmarkConfig.PageSize;
        private const int _totalRows = BenchmarkConfig.TotalRows;

        private int[,] _baselineArray = null!;
        private PagedBuffer2D<int> _heapMemory = null!;
        private PagedBuffer2D<int> _mmfMemory = null!;
        private string _mmfPath = null!;

        private (int r, int c)[] _randomIndices = null!;
        private int _idx = 0;

        [GlobalSetup]
        public void Setup()
        {
            _baselineArray = new int[_totalRows, _width];
            var options = new Buffer2DOptions { Width = _width, PageSize = _pageSize };
            _heapMemory = new PagedBuffer2D<int>(options, new HeapProvider<int>());
            
            _mmfPath = Path.Combine(Path.GetTempPath(), "Carrot_Bench_MMF_RandR_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_mmfPath);
            _mmfMemory = new PagedBuffer2D<int>(options, new MmfPageProvider<int>(_mmfPath));

            _randomIndices = new (int r, int c)[65536];
            Random rand = new Random(42);
            for (int i = 0; i < _randomIndices.Length; i++)
            {
                _randomIndices[i] = (rand.Next(0, _totalRows), rand.Next(0, _width));
                int val = i;
                _baselineArray[_randomIndices[i].r, _randomIndices[i].c] = val;
                _heapMemory.SetElement(_randomIndices[i].r, _randomIndices[i].c, val);
                _mmfMemory.SetElement(_randomIndices[i].r, _randomIndices[i].c, val);
            }
            _mmfMemory.Commit();
        }

        [GlobalCleanup]
        public void Cleanup()
        {
            _heapMemory.Dispose();
            _mmfMemory.Dispose();
            if (Directory.Exists(_mmfPath)) try { Directory.Delete(_mmfPath, true); } catch { }
        }

        [Benchmark(Baseline = true)]
        public int Array_Random_Point()
        {
            var idx = _randomIndices[(uint)_idx++ & 65535];
            return _baselineArray[idx.r, idx.c];
        }

        [Benchmark]
        public int Heap_Random_Point()
        {
            var idx = _randomIndices[(uint)_idx++ & 65535];
            return _heapMemory[idx.r, idx.c];
        }

        [Benchmark]
        public int MMF_Random_Point()
        {
            var idx = _randomIndices[(uint)_idx++ & 65535];
            return _mmfMemory[idx.r, idx.c];
        }
    }
}
