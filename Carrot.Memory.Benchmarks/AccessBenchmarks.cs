using System;
using System.IO;
using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;
using Carrot.Memory;
using CommunityToolkit.HighPerformance;

namespace Carrot.Memory.Benchmarks
{
    [MemoryDiagnoser]
    [Config(typeof(BenchmarkConfig))]
    public class AccessBenchmarks
    {
        private const int TotalRows = 10000;
        private const int Cols = 1024;
        private const int PageSize = 1024;
        
        private const int InPageLen = 1024;
        private const int CrossPageLen = 2048;

        private int[,] _array;
        private PagedMemory2D<int> _heapMemory;
        private PagedMemory2D<int> _mmfMemory;
        private string _mmfPath;

        private int[] _rowData;
        private int[] _colInPageData;
        private int[] _colCrossPageData;
        private int[] _colFullData;

        [GlobalSetup]
        public void Setup()
        {
            _array = new int[TotalRows, Cols];
            _heapMemory = new PagedMemory2D<int>(Cols, PageSize, new DefaultHeapPageProvider<int>());
            
            _mmfPath = Path.Combine(Path.GetTempPath(), "CarrotMemoryBenchmarks_" + Guid.NewGuid().ToString());
            Directory.CreateDirectory(_mmfPath);
            _mmfMemory = new PagedMemory2D<int>(Cols, PageSize, new MmfPageProvider<int>(_mmfPath));

            // 初始化数据
            _rowData = new int[Cols];
            _colInPageData = new int[InPageLen];
            _colCrossPageData = new int[CrossPageLen];
            _colFullData = new int[TotalRows];

            for (int r = 0; r < TotalRows; r++)
            {
                for (int c = 0; c < Cols; c++)
                {
                    int val = r + c;
                    _array[r, c] = val;
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
            if (Directory.Exists(_mmfPath)) Directory.Delete(_mmfPath, true);
        }

        #region Row Access (Sequential)

        [Benchmark(Baseline = true)]
        public long Array_Row()
        {
            long sum = 0;
            for (int r = 0; r < TotalRows; r++)
                for (int c = 0; c < Cols; c++) sum += _array[r, c];
            return sum;
        }

        [Benchmark]
        public long Heap_Row()
        {
            long sum = 0;
            for (int r = 0; r < TotalRows; r++)
            {
                var row = _heapMemory.GetRowView(r, 0, Cols).AsSpan();
                for (int i = 0; i < row.Length; i++) sum += row[i];
            }
            return sum;
        }

        [Benchmark]
        public long Mmf_Row()
        {
            long sum = 0;
            for (int r = 0; r < TotalRows; r++)
            {
                var row = _mmfMemory.GetRowView(r, 0, Cols).AsSpan();
                for (int i = 0; i < row.Length; i++) sum += row[i];
            }
            return sum;
        }

        #endregion

        #region Column Access (In-Page)

        [Benchmark]
        public long Array_Col_InPage()
        {
            long sum = 0;
            for (int c = 0; c < Cols; c++)
                for (int r = 0; r < InPageLen; r++) sum += _array[r, c];
            return sum;
        }

        [Benchmark]
        public long Heap_Col_InPage()
        {
            long sum = 0;
            for (int c = 0; c < Cols; c++)
            {
                var view = _heapMemory.GetColumnView(0, c, InPageLen);
                for (int r = 0; r < InPageLen; r++) sum += view[r];
            }
            return sum;
        }

        [Benchmark]
        public long Mmf_Col_InPage()
        {
            long sum = 0;
            for (int c = 0; c < Cols; c++)
            {
                var view = _mmfMemory.GetColumnView(0, c, InPageLen);
                for (int r = 0; r < InPageLen; r++) sum += view[r];
            }
            return sum;
        }

        #endregion

        #region Column Access (Cross-Page)

        [Benchmark]
        public long Array_Col_Cross()
        {
            long sum = 0;
            for (int c = 0; c < Cols; c++)
                for (int r = 0; r < CrossPageLen; r++) sum += _array[r, c];
            return sum;
        }

        [Benchmark]
        public long Heap_Col_Cross()
        {
            long sum = 0;
            for (int c = 0; c < Cols; c++)
            {
                var view = _heapMemory.GetColumnView(0, c, CrossPageLen);
                for (int r = 0; r < CrossPageLen; r++) sum += view[r];
            }
            return sum;
        }

        [Benchmark]
        public long Mmf_Col_Cross()
        {
            long sum = 0;
            for (int c = 0; c < Cols; c++)
            {
                var view = _mmfMemory.GetColumnView(0, c, CrossPageLen);
                for (int r = 0; r < CrossPageLen; r++) sum += view[r];
            }
            return sum;
        }

        #endregion

        #region Column Access (Full)

        [Benchmark]
        public long Array_Col_Full()
        {
            long sum = 0;
            for (int c = 0; c < Cols; c++)
                for (int r = 0; r < TotalRows; r++) sum += _array[r, c];
            return sum;
        }

        [Benchmark]
        public long Heap_Col_Full()
        {
            long sum = 0;
            for (int c = 0; c < Cols; c++)
            {
                var view = _heapMemory.GetColumnView(0, c, TotalRows);
                for (int r = 0; r < TotalRows; r++) sum += view[r];
            }
            return sum;
        }

        [Benchmark]
        public long Mmf_Col_Full()
        {
            long sum = 0;
            for (int c = 0; c < Cols; c++)
            {
                var view = _mmfMemory.GetColumnView(0, c, TotalRows);
                for (int r = 0; r < TotalRows; r++) sum += view[r];
            }
            return sum;
        }

        #endregion

        #region Set Operations (Additions)

        [Benchmark]
        public void Array_SetRow()
        {
            for (int r = 0; r < TotalRows; r++)
                for (int c = 0; c < Cols; c++) _array[r, c] = _rowData[c];
        }

        [Benchmark]
        public void Heap_SetRow()
        {
            for (int r = 0; r < TotalRows; r++) _heapMemory.SetRow(r, 0, _rowData);
        }

        [Benchmark]
        public void Mmf_SetRow()
        {
            for (int r = 0; r < TotalRows; r++) _mmfMemory.SetRow(r, 0, _rowData);
        }

        [Benchmark]
        public void Array_SetCol_InPage()
        {
            for (int c = 0; c < Cols; c++)
                for (int r = 0; r < InPageLen; r++) _array[r, c] = _colInPageData[r];
        }

        [Benchmark]
        public void Heap_SetCol_InPage()
        {
            for (int c = 0; c < Cols; c++) _heapMemory.SetColumn(0, c, _colInPageData);
        }

        [Benchmark]
        public void Mmf_SetCol_InPage()
        {
            for (int c = 0; c < Cols; c++) _mmfMemory.SetColumn(0, c, _colInPageData);
        }

        [Benchmark]
        public void Array_SetCol_Cross()
        {
            for (int c = 0; c < Cols; c++)
                for (int r = 0; r < CrossPageLen; r++) _array[r, c] = _colCrossPageData[r];
        }

        [Benchmark]
        public void Heap_SetCol_Cross()
        {
            for (int c = 0; c < Cols; c++) _heapMemory.SetColumn(0, c, _colCrossPageData);
        }

        [Benchmark]
        public void Mmf_SetCol_Cross()
        {
            for (int c = 0; c < Cols; c++) _mmfMemory.SetColumn(0, c, _colCrossPageData);
        }

        [Benchmark]
        public void Array_SetCol_Full()
        {
            for (int c = 0; c < Cols; c++)
                for (int r = 0; r < TotalRows; r++) _array[r, c] = _colFullData[r];
        }

        [Benchmark]
        public void Heap_SetCol_Full()
        {
            for (int c = 0; c < Cols; c++) _heapMemory.SetColumn(0, c, _colFullData);
        }

        [Benchmark]
        public void Mmf_SetCol_Full()
        {
            for (int c = 0; c < Cols; c++) _mmfMemory.SetColumn(0, c, _colFullData);
        }

        #endregion

        #region Random & Block

        [Benchmark]
        public long Array_Random()
        {
            long sum = 0;
            Random rand = new Random(42);
            for (int i = 0; i < 100000; i++) sum += _array[rand.Next(0, TotalRows), rand.Next(0, Cols)];
            return sum;
        }

        [Benchmark]
        public long Heap_Random()
        {
            long sum = 0;
            Random rand = new Random(42);
            for (int i = 0; i < 100000; i++) sum += _heapMemory[rand.Next(0, TotalRows), rand.Next(0, Cols)];
            return sum;
        }

        [Benchmark]
        public void Array_Block()
        {
            int[,] source = new int[100, 100];
            for (int r = 0; r < TotalRows / 100; r++)
                for (int c = 0; c < Cols / 100; c++)
                    for (int sr = 0; sr < 100; sr++)
                        for (int sc = 0; sc < 100; sc++) _array[r * 100 + sr, c * 100 + sc] = source[sr, sc];
        }

        [Benchmark]
        public void Heap_Block()
        {
            int[,] sourceArr = new int[100, 100];
            var source = sourceArr.AsSpan2D();
            for (int r = 0; r < TotalRows / 100; r++)
                for (int c = 0; c < Cols / 100; c++) _heapMemory.SetBlock(r * 100, c * 100, source);
        }

        #endregion
    }

    public class BenchmarkConfig : ManualConfig
    {
        public BenchmarkConfig()
        {
            AddExporter(BenchmarkDotNet.Exporters.MarkdownExporter.GitHub);
        }
    }
}
