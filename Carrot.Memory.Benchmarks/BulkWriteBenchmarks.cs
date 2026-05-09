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

        private int[,] _baselineArray = null!;
        private PagedBuffer2D<int> _heapMemory = null!;
        private PagedBuffer2D<int> _mmfMemory = null!;
        private string _mmfPath = null!;

        private int[,] _sourceBlock = null!;

        [GlobalSetup]
        public void Setup()
        {
            _baselineArray = new int[BlockSize, BlockSize];
            var options = new PagedBuffer2DOptions { Width = _width, PageSize = _pageSize };
            _heapMemory = new PagedBuffer2D<int>(options, new HeapProvider<int>());
            
            _mmfPath = Path.Combine(Path.GetTempPath(), "Carrot_Bench_MMF_BulkW_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_mmfPath);
            _mmfMemory = new PagedBuffer2D<int>(options, new MmfPageProvider<int>(_mmfPath));

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

        [Benchmark(Baseline = true, OperationsPerInvoke = BlockSize * BlockSize)]
        public void Array_SetBlock()
        {
            for (int r = 0; r < BlockSize; r++)
                for (int c = 0; c < BlockSize; c++)
                    _baselineArray[r, c] = _sourceBlock[r, c];
        }

        [Benchmark(OperationsPerInvoke = BlockSize * BlockSize)]
        public void Heap_SetBlock()
        {
            _heapMemory.SetBlock(0, 0, _sourceBlock.AsSpan2D());
        }

        [Benchmark(OperationsPerInvoke = BlockSize * BlockSize)]
        public void MMF_SetBlock()
        {
            _mmfMemory.SetBlock(0, 0, _sourceBlock.AsSpan2D());
        }
    }
}
