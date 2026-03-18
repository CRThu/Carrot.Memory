using System;
using System.IO;
using BenchmarkDotNet.Attributes;
using Carrot.Memory;

namespace Carrot.Memory.Benchmarks
{
    [MemoryDiagnoser]
    [Config(typeof(BenchmarkConfig))]
    public class ColumnReadBenchmarks
    {
        private const int _width = BenchmarkConfig.Cols;
        private const int _pageSize = BenchmarkConfig.PageSize;
        private const int _totalRows = BenchmarkConfig.TotalRows;

        private const int InPageLimit = 8192;
        private const int CrossPageStart = 8000;
        private const int CrossPageEnd = 10000;

        private int[,] _baselineArray;
        private PagedMemory2D<int> _heapMemory;
        private PagedMemory2D<int> _mmfMemory;
        private string _mmfPath;

        [GlobalSetup]
        public void Setup()
        {
            _baselineArray = new int[_totalRows, _width];
            _heapMemory = new PagedMemory2D<int>(_width, _pageSize, new DefaultHeapPageProvider<int>());
            
            _mmfPath = Path.Combine(Path.GetTempPath(), "Carrot_Bench_MMF_ColR_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_mmfPath);
            _mmfMemory = new PagedMemory2D<int>(_width, _pageSize, new MmfPageProvider<int>(_mmfPath));

            for (int r = 0; r < _totalRows; r++)
            {
                for (int c = 0; c < _width; c++)
                {
                    int val = r ^ c;
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

        #region InPage

        [Benchmark, BenchmarkCategory("InPage")]
        public long Array_Col_InPage()
        {
            long sum = 0;
            for (int c = 0; c < _width; c++)
                for (int r = 0; r < InPageLimit; r++) sum += _baselineArray[r, c];
            return sum;
        }

        [Benchmark, BenchmarkCategory("InPage")]
        public long Heap_Col_InPage()
        {
            long sum = 0;
            for (int c = 0; c < _width; c++)
            {
                var view = _heapMemory.GetColumnView(0, c, InPageLimit);
                for (int r = 0; r < InPageLimit; r++) sum += view[r];
            }
            return sum;
        }

        [Benchmark, BenchmarkCategory("InPage")]
        public long MMF_Col_InPage()
        {
            long sum = 0;
            for (int c = 0; c < _width; c++)
            {
                var view = _mmfMemory.GetColumnView(0, c, InPageLimit);
                for (int r = 0; r < InPageLimit; r++) sum += view[r];
            }
            return sum;
        }

        #endregion

        #region CrossPage

        [Benchmark, BenchmarkCategory("CrossPage")]
        public long Array_Col_CrossPage()
        {
            long sum = 0;
            for (int c = 0; c < _width; c++)
                for (int r = CrossPageStart; r < CrossPageEnd; r++) sum += _baselineArray[r, c];
            return sum;
        }

        [Benchmark, BenchmarkCategory("CrossPage")]
        public long Heap_Col_CrossPage()
        {
            long sum = 0;
            int len = CrossPageEnd - CrossPageStart;
            for (int c = 0; c < _width; c++)
            {
                var view = _heapMemory.GetColumnView(CrossPageStart, c, len);
                for (int i = 0; i < len; i++) sum += view[i];
            }
            return sum;
        }

        [Benchmark, BenchmarkCategory("CrossPage")]
        public long MMF_Col_CrossPage()
        {
            long sum = 0;
            int len = CrossPageEnd - CrossPageStart;
            for (int c = 0; c < _width; c++)
            {
                var view = _mmfMemory.GetColumnView(CrossPageStart, c, len);
                for (int i = 0; i < len; i++) sum += view[i];
            }
            return sum;
        }

        #endregion

        #region Full

        [Benchmark(Baseline = true), BenchmarkCategory("Full")]
        public long Array_Col_Full()
        {
            long sum = 0;
            for (int c = 0; c < _width; c++)
                for (int r = 0; r < _totalRows; r++) sum += _baselineArray[r, c];
            return sum;
        }

        [Benchmark, BenchmarkCategory("Full")]
        public long Heap_Col_Full()
        {
            long sum = 0;
            for (int c = 0; c < _width; c++)
            {
                var view = _heapMemory.GetColumnView(0, c, _totalRows);
                for (int r = 0; r < _totalRows; r++) sum += view[r];
            }
            return sum;
        }

        [Benchmark, BenchmarkCategory("Full")]
        public long MMF_Col_Full()
        {
            long sum = 0;
            for (int c = 0; c < _width; c++)
            {
                var view = _mmfMemory.GetColumnView(0, c, _totalRows);
                for (int r = 0; r < _totalRows; r++) sum += view[r];
            }
            return sum;
        }

        #endregion
    }
}
