using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Numerics;
using System.Threading;
using CommunityToolkit.HighPerformance;

namespace Carrot.Memory
{
    /// <summary>
    /// 提供对二维分页内存的只读访问接口。
    /// </summary>
    public interface IReadonlyPagedMemory2D<T>
    {
        int RowCount { get; }
        int Width { get; }
        ref T this[int r, int c] { get; }
        PagedView<T> GetSlice(int row, int col, int len);
        PagedView<T> GetSeries(int row, int col, int len);
    }

    /// <summary>
    /// 提供对二维分页内存的读写访问接口。
    /// </summary>
    public interface IPagedMemory2D<T> : IReadonlyPagedMemory2D<T>, IDisposable
    {
        void SetElement(int r, int c, T value);
        void SetBlock(int r, int c, ReadOnlySpan2D<T> data);
        void SetRow(int r, int c, ReadOnlySpan<T> data);
        void SetColumn(int r, int c, ReadOnlySpan<T> data);
        void FlushAll();
    }

    /// <summary>
    /// 分页供应者接口，支持自定义页面分配和刷新逻辑。
    /// </summary>
    public interface IPageProvider<T>
    {
        Memory2D<T> Create(int rows, int cols, int index);
        void Flush(Memory2D<T> page, int index);
    }

    /// <summary>
    /// 默认的堆内存页面供应者。
    /// </summary>
    public class DefaultHeapPageProvider<T> : IPageProvider<T>
    {
        public Memory2D<T> Create(int rows, int cols, int index) => 
            new T[rows * cols].AsMemory().AsMemory2D(rows, cols);

        public void Flush(Memory2D<T> page, int index) { /* 堆内存无需执行物理刷新 */ }
    }

    /// <summary>
    /// 提供一个基于分页机制的二维内存容器，支持动态行增长和高性能的行列切片访问。
    /// </summary>
    /// <typeparam name="T">存储的数据类型。</typeparam>
    public class PagedMemory2D<T> : IPagedMemory2D<T>
    {
        private const int InitialPageCapacity = 16;
        private readonly ReaderWriterLockSlim _rwLock = new();
        private Memory2D<T>[] _pages;
        private int _pageCount;
        private readonly int _shift;
        private readonly int _mask;
        private readonly int _pageSize;
        private readonly int _width;
        private readonly IPageProvider<T> _provider;

        private int _rowCount = 0;
        private bool _disposed;

        /// <summary>
        /// 获取当前容器已存储的总行数。
        /// </summary>
        public int RowCount => Volatile.Read(ref _rowCount);

        /// <summary>
        /// 获取每一行的固定宽度。
        /// </summary>
        public int Width => _width;

        /// <summary>
        /// 初始化 <see cref="PagedMemory2D{T}"/> 类的新实例。
        /// </summary>
        /// <param name="width">每一行的列数（固定宽度）。</param>
        /// <param name="pageSize">分页行数（必须是 2 的幂）。</param>
        /// <param name="provider">页面供应者。如果为 null 则使用默认堆内存分配。</param>
        public PagedMemory2D(int width, int pageSize, IPageProvider<T>? provider = null)
        {
            if (pageSize <= 0 || (pageSize & (pageSize - 1)) != 0)
            {
                throw new ArgumentException("pageSize 必须是 2 的幂。", nameof(pageSize));
            }

            _width = width;
            _pageSize = pageSize;
            _shift = BitOperations.TrailingZeroCount((uint)pageSize);
            _mask = _pageSize - 1;
            _pages = new Memory2D<T>[InitialPageCapacity];
            _provider = provider ?? new DefaultHeapPageProvider<T>();
        }

        /// <summary>
        /// 获取只读视图。
        /// </summary>
        public IReadonlyPagedMemory2D<T> AsReadOnly() => this;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void EnsurePageExists(int pageIdx)
        {
            if (pageIdx < _pageCount) return;

            // 注意：调用者必须持有 _rwLock 的写锁
            // 调整：不再采用翻倍策略，改为按需扩展到足以容纳当前 pageIdx
            if (pageIdx >= _pages.Length)
            {
                int newSize = pageIdx + 1;
                var newPages = new Memory2D<T>[newSize];
                Array.Copy(_pages, newPages, _pageCount);
                Volatile.Write(ref _pages, newPages);
            }

                while (_pageCount <= pageIdx)
                {
                    int nextIndex = _pageCount;
                    _pages[nextIndex] = _provider.Create(_pageSize, _width, nextIndex);
                    _pageCount++;
                }
        }

        /// <summary>
        /// 在指定位置设置单个元素。
        /// </summary>
        public void SetElement(int r, int c, T value)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(PagedMemory2D<T>));
            if ((uint)c >= (uint)_width) ThrowArgumentException("列索引越界");

            _rwLock.EnterWriteLock();
            try
            {
                EnsurePageExists(r >> _shift);
                var pages = Volatile.Read(ref _pages);
                pages[r >> _shift].Span[r & _mask, c] = value;
                
                int targetHeight = r + 1;
                if (targetHeight > _rowCount)
                {
                    Volatile.Write(ref _rowCount, targetHeight);
                }
            }
            finally
            {
                _rwLock.ExitWriteLock();
            }
        }

        /// <summary>
        /// 将二维数据块写入指定位置。
        /// </summary>
        /// <param name="r">起始行。</param>
        /// <param name="c">起始列。</param>
        /// <param name="data">待写入的数据块。</param>
        public void SetBlock(int r, int c, ReadOnlySpan2D<T> data)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(PagedMemory2D<T>));
            if (r < 0 || c < 0 || c + data.Width > _width) ThrowArgumentException("写入区域越界或非法");
            
            _rwLock.EnterWriteLock();
            try
            {
                SetBlockInternal(r, c, data);
            }
            finally
            {
                _rwLock.ExitWriteLock();
            }
        }

        private void SetBlockInternal(int r, int c, ReadOnlySpan2D<T> data)
        {
            int targetHeight = r + data.Height;
            if (targetHeight > 0)
            {
                EnsurePageExists((targetHeight - 1) >> _shift);
            }

            int rowsLeft = data.Height;
            int sourceRowOffset = 0;
            int currentRow = r;

            while (rowsLeft > 0)
            {
                int pageIdx = currentRow >> _shift;
                int rowInPage = currentRow & _mask;
                int canCopy = Math.Min(_pageSize - rowInPage, rowsLeft);

                var pages = Volatile.Read(ref _pages);
                var targetSpan2d = pages[pageIdx].Span;

                data.Slice(sourceRowOffset, 0, canCopy, data.Width)
                    .CopyTo(targetSpan2d.Slice(rowInPage, c, canCopy, data.Width));

                currentRow += canCopy;
                sourceRowOffset += canCopy;
                rowsLeft -= canCopy;
            }

            if (targetHeight > _rowCount)
            {
                Volatile.Write(ref _rowCount, targetHeight);
            }
        }


        /// <summary>
        /// 获取指定行号和列号的数据引用。
        /// 注意：通过引用直接修改数据将绕过写锁保护。建议仅用于读取或性能敏感的批量操作。
        /// </summary>
        public ref T this[int r, int c]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                int rowCount = Volatile.Read(ref _rowCount);
                if ((uint)r >= (uint)rowCount || (uint)c >= (uint)_width) ThrowIndexOutOfRangeException();
                var pages = Volatile.Read(ref _pages);
                return ref pages[r >> _shift].Span[r & _mask, c];
            }
        }

        /// <summary>
        /// 在指定位置设置单行数据块（水平写入）。
        /// </summary>
        public void SetRow(int r, int c, ReadOnlySpan<T> data)
        {
            SetBlock(r, c, data.AsSpan2D(1, data.Length));
        }

        /// <summary>
        /// 在指定位置设置单列数据块（垂直写入）。
        /// </summary>
        public void SetColumn(int r, int c, ReadOnlySpan<T> data)
        {
            SetBlock(r, c, data.AsSpan2D(data.Length, 1));
        }



        /// <summary>
        /// 获取指定行中某一段的水平视图（Slice）。
        /// </summary>
        /// <param name="row">起始行索引。</param>
        /// <param name="col">起始列索引。</param>
        /// <param name="len">截取长度。</param>
        /// <returns>对应的视图对象。</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public PagedView<T> GetSlice(int row, int col, int len)
        {
            int rowCount = Volatile.Read(ref _rowCount);
            if ((uint)row >= (uint)rowCount || (uint)col + (uint)len > (uint)_width) ThrowIndexOutOfRangeException();
            var page = Volatile.Read(ref _pages)[row >> _shift].Span;
            return new PagedView<T>(page.GetRowSpan(row & _mask).Slice(col, len));
        }

        /// <summary>
        /// 获取指定列中某一段的垂直视图（Series），支持跨页。
        /// </summary>
        /// <param name="row">起始行索引。</param>
        /// <param name="col">列索引。</param>
        /// <param name="len">垂直截取长度。</param>
        /// <returns>对应的视图对象。</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public PagedView<T> GetSeries(int row, int col, int len)
        {
            int rowCount = Volatile.Read(ref _rowCount);
            if ((uint)col >= (uint)_width || (uint)row + (uint)len > (uint)rowCount) ThrowIndexOutOfRangeException();

            int pageRowsLeft = _pageSize - (row & _mask);
            // 如果在同一页内，使用高效的 Span2D 模式
            if (len <= pageRowsLeft)
            {
                var span2d = Volatile.Read(ref _pages)[row >> _shift].Span;
                return new PagedView<T>(span2d.Slice(row & _mask, col, len, 1));
            }

            // 跨页则降级到 PagedParent 模式
            return new PagedView<T>(this, row, col, len);
        }

        #region Throw Helpers

        [DoesNotReturn]
        private static void ThrowIndexOutOfRangeException() =>
            throw new IndexOutOfRangeException("访问越界：超出了 PagedMemory2D 的有效范围。");

        [DoesNotReturn]
        private static void ThrowArgumentException(string msg) => throw new ArgumentException(msg);

        #endregion

        /// <summary>
        /// 刷新所有页面到持久化层（由 Provider 实现）。此操作完全无锁。
        /// </summary>
        public void FlushAll()
        {
            // 获取当前页面数组的快照，避免遍历过程中数组引用变更
            var pagesSnapshot = Volatile.Read(ref _pages);
            int count = _pageCount;
            for (int i = 0; i < count; i++)
            {
                _provider.Flush(pagesSnapshot[i], i);
            }
        }

        /// <summary>
        /// 释放容器占用的资源，主要释放 ReaderWriterLockSlim。
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;
            _rwLock.Dispose();
            _disposed = true;
        }
    }

    /// <summary>
    /// 表示对 <see cref="PagedMemory2D{T}"/> 中某一部分数据的统一视图。
    /// 该结构为 ref struct，旨在提供零分配的切片访问。
    /// </summary>
    /// <typeparam name="T">数据类型。</typeparam>
    public readonly ref struct PagedView<T>
    {
        private readonly IReadonlyPagedMemory2D<T> _parent;
        private readonly Span<T> _rowSpan;
        private readonly Span2D<T> _colSpan2d;
        private readonly int _r, _c;
        private readonly byte _mode;

        /// <summary>
        /// 获取视图中的元素总数。
        /// </summary>
        public int Length { get; }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal PagedView(Span<T> rowSpan)
        {
            _rowSpan = rowSpan;
            _colSpan2d = default;
            _parent = null!;
            Length = rowSpan.Length;
            _mode = 0;
            _r = _c = 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal PagedView(Span2D<T> colSpan2d)
        {
            _colSpan2d = colSpan2d;
            _rowSpan = default;
            _parent = null!;
            Length = colSpan2d.Height;
            _mode = 1;
            _r = _c = 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal PagedView(IReadonlyPagedMemory2D<T> parent, int r, int c, int len)
        {
            _parent = parent;
            _rowSpan = default;
            _colSpan2d = default;
            Length = len;
            _mode = 2;
            _r = r;
            _c = c;
        }

        /// <summary>
        /// 获取视图中指定偏移位置的元素引用。
        /// </summary>
        /// <param name="i">视图内的逻辑索引。</param>
        /// <returns>数据的引用。</returns>
        public ref T this[int i]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if ((uint)i >= (uint)Length) ThrowIndexOutOfRangeException();

                if (_mode == 0) return ref _rowSpan[i];
                if (_mode == 1) return ref _colSpan2d[i, 0];
                // 模式 2：跨页访问
                return ref _parent[_r + i, _c];
            }
        }

        /// <summary>
        /// 将视图转换为 <see cref="Span{T}"/>。仅当视图为单行水平切片时支持。
        /// </summary>
        /// <returns>对应的 Span。</returns>
        /// <exception cref="NotSupportedException">当视图不是单行切片时抛出。</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Span<T> AsSpan() =>
            _mode == 0 ? _rowSpan : throw new NotSupportedException("仅行视图（GetSlice）可转 Span，列视图或跨页视图不支持此操作。");

        [DoesNotReturn]
        private static void ThrowIndexOutOfRangeException() => throw new IndexOutOfRangeException("视图访问越界。");
    }
}
