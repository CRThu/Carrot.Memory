using System;
using System.IO;
using BenchmarkDotNet.Attributes;
using Carrot.Memory;

namespace Carrot.Memory.Benchmarks
{
    [MemoryDiagnoser]
    [Config(typeof(BenchmarkConfig))]
    public class RowReadBenchmarks
    {
        private const int _width = BenchmarkConfig.Cols;
        private const int _pageSize = BenchmarkConfig.PageSize;
        private const int _totalRows = BenchmarkConfig.TotalRows;

        private int[,] _baselineArray;
        private PagedMemory2D<int> _heapMemory;
        private PagedMemory2D<int> _mmfMemory;
        private string _mmfPath;

        [GlobalSetup]
        public void Setup()
        {
            _baselineArray = new int[_totalRows, _width];
            _heapMemory = new PagedMemory2D<int>(_width, _pageSize, new DefaultHeapPageProvider<int>());
            
            _mmfPath = Path.Combine(Path.GetTempPath(), "Carrot_Bench_MMF_RowR_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_mmfPath);
            _mmfMemory = new PagedMemory2D<int>(_width, _pageSize, new MmfPageProvider<int>(_mmfPath));

            // 安全初始化：先完成所有 SetElement 确保 RowCount 正确
            for (int r = 0; r < _totalRows; r++)
            {
                for (int c = 0; c < _width; c++)
                {
                    int val = r + c;
                    _baselineArray[r, c] = val;
                    _heapMemory.SetElement(r, c, val);
                    _mmfMemory.SetElement(r, c, val);
                }
            }
            _mmfMemory.FlushAll();
        }

        [GlobalCleanup]
        public void Cleanup()
        {
            _heapMemory.Dispose();
            _mmfMemory.Dispose();
            if (Directory.Exists(_mmfPath)) try { Directory.Delete(_mmfPath, true); } catch { }
        }

        [Benchmark(Baseline = true)]
        public long Array_Row_Sum()
        {
            long sum = 0;
            for (int r = 0; r < _totalRows; r++)
                for (int c = 0; c < _width; c++) sum += _baselineArray[r, c];
            return sum;
        }

        [Benchmark]
        public long Heap_Row_Sum()
        {
            long sum = 0;
            for (int r = 0; r < _totalRows; r++)
            {
                var span = _heapMemory.GetRowView(r, 0, _width).AsSpan();
                for (int i = 0; i < span.Length; i++) sum += span[i];
            }
            return sum;
        }

        [Benchmark]
        public long MMF_Row_Sum()
        {
            long sum = 0;
            for (int r = 0; r < _totalRows; r++)
            {
                var span = _mmfMemory.GetRowView(r, 0, _width).AsSpan();
                for (int i = 0; i < span.Length; i++) sum += span[i];
            }
            return sum;
        }
    }
}
