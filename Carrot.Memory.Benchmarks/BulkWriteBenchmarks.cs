using System;
using System.IO;
using BenchmarkDotNet.Attributes;
using Carrot.Memory;
using CommunityToolkit.HighPerformance;

namespace Carrot.Memory.Benchmarks
{
    [MemoryDiagnoser]
    [Config(typeof(BenchmarkConfig))]
    public class BulkWriteBenchmarks
    {
        private const int _width = BenchmarkConfig.Cols;
        private const int _pageSize = BenchmarkConfig.PageSize;

        private const int BlockSize = 128;

        private int[,] _baselineArray;
        private PagedMemory2D<int> _heapMemory;
        private PagedMemory2D<int> _mmfMemory;
        private string _mmfPath;

        private int[,] _sourceBlock;

        [GlobalSetup]
        public void Setup()
        {
            _baselineArray = new int[BlockSize, BlockSize];
            _heapMemory = new PagedMemory2D<int>(_width, _pageSize, new DefaultHeapPageProvider<int>());
            
            _mmfPath = Path.Combine(Path.GetTempPath(), "Carrot_Bench_MMF_BulkW_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_mmfPath);
            _mmfMemory = new PagedMemory2D<int>(_width, _pageSize, new MmfPageProvider<int>(_mmfPath));

            _sourceBlock = new int[BlockSize, BlockSize];
            for (int i = 0; i < BlockSize; i++)
                for (int j = 0; j < BlockSize; j++)
                    _sourceBlock[i, j] = i ^ j;
        }

        [GlobalCleanup]
        public void Cleanup()
        {
            _heapMemory.Dispose();
            _mmfMemory.Dispose();
            if (Directory.Exists(_mmfPath)) try { Directory.Delete(_mmfPath, true); } catch { }
        }

        [Benchmark(Baseline = true)]
        public void Array_SetBlock()
        {
            for (int r = 0; r < BlockSize; r++)
                for (int c = 0; c < BlockSize; c++)
                    _baselineArray[r, c] = _sourceBlock[r, c];
        }

        [Benchmark]
        public void Heap_SetBlock()
        {
            _heapMemory.SetBlock(0, 0, _sourceBlock.AsSpan2D());
        }

        [Benchmark]
        public void MMF_SetBlock()
        {
            _mmfMemory.SetBlock(0, 0, _sourceBlock.AsSpan2D());
        }
    }
}
