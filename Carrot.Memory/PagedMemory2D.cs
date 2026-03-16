using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Numerics;
using System.Threading;
using CommunityToolkit.HighPerformance;

namespace Carrot.Memory
{
    /// <summary>
    /// 提供一个基于分页机制的二维内存容器，支持动态行增长和高性能的行列切片访问。
    /// 该类实现了双视图模式：通过显式接口实现支持只读访问，同时保留高性能的直接读写能力。
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

        /// <inheritdoc />
        public int RowCount => Volatile.Read(ref _rowCount);

        /// <inheritdoc />
        public int Width => _width;

        /// <summary>
        /// 初始化 <see cref="PagedMemory2D{T}"/> 类的新实例。
        /// </summary>
        /// <param name="width">每一行的列数（固定宽度）。</param>
        /// <param name="pageSize">分页行数（必须是 2 的幂，以便使用位运算优化）。</param>
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

            // 加载持久化元数据
            if (_provider is IPersistentMetadataProvider persistentProvider)
            {
                if (persistentProvider.TryLoadMetadata(out int savedRowCount, out int savedWidth, out int savedPageSize))
                {
                    if (savedWidth != _width || savedPageSize != _pageSize)
                    {
                        throw new InvalidOperationException("持久化元数据与容器配置不一致。");
                    }
                    _rowCount = savedRowCount;

                    // 预加载已有的分页
                    if (_rowCount > 0)
                    {
                        EnsurePageExists((_rowCount - 1) >> _shift);
                    }
                }
            }
        }

        /// <summary>
        /// 获取当前实例的只读视图接口。
        /// </summary>
        public IReadonlyPagedMemory2D<T> AsReadOnly() => this;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void EnsurePageExists(int pageIdx)
        {
            if (pageIdx < _pageCount) return;

            // 注意：调用者必须持有 _rwLock 的写锁
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

        /// <inheritdoc />
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

        /// <inheritdoc />
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
        /// 显式实现只读接口索引器，返回只读引用。
        /// </summary>
        ref readonly T IReadonlyPagedMemory2D<T>.this[int r, int c] => ref this[r, c];

        /// <inheritdoc />
        public void SetRow(int r, int c, ReadOnlySpan<T> data)
        {
            SetBlock(r, c, data.AsSpan2D(1, data.Length));
        }

        /// <inheritdoc />
        public void SetColumn(int r, int c, ReadOnlySpan<T> data)
        {
            SetBlock(r, c, data.AsSpan2D(data.Length, 1));
        }

        /// <summary>
        /// 获取指定行中某一段的水平可写视图（行视图）。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public PagedView<T> GetRowView(int row, int col, int len)
        {
            int rowCount = Volatile.Read(ref _rowCount);
            if ((uint)row >= (uint)rowCount || (uint)col + (uint)len > (uint)_width) ThrowIndexOutOfRangeException();
            var page = Volatile.Read(ref _pages)[row >> _shift].Span;
            return new PagedView<T>(page.GetRowSpan(row & _mask).Slice(col, len));
        }

        /// <summary>
        /// 显式实现只读接口视图获取，返回只读行视图。
        /// </summary>
        ReadOnlyPagedView<T> IReadonlyPagedMemory2D<T>.GetRowView(int row, int col, int len)
        {
            int rowCount = Volatile.Read(ref _rowCount);
            if ((uint)row >= (uint)rowCount || (uint)col + (uint)len > (uint)_width) ThrowIndexOutOfRangeException();
            var page = Volatile.Read(ref _pages)[row >> _shift].Span;
            return new ReadOnlyPagedView<T>(page.GetRowSpan(row & _mask).Slice(col, len));
        }

        /// <summary>
        /// 获取指定列中某一段的垂直可写视图（列视图），支持跨页。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public PagedView<T> GetColumnView(int row, int col, int len)
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

        /// <summary>
        /// 显式实现只读接口视图获取，返回只读列视图。
        /// </summary>
        ReadOnlyPagedView<T> IReadonlyPagedMemory2D<T>.GetColumnView(int row, int col, int len)
        {
            int rowCount = Volatile.Read(ref _rowCount);
            if ((uint)col >= (uint)_width || (uint)row + (uint)len > (uint)rowCount) ThrowIndexOutOfRangeException();

            int pageRowsLeft = _pageSize - (row & _mask);
            if (len <= pageRowsLeft)
            {
                var span2d = Volatile.Read(ref _pages)[row >> _shift].Span;
                return new ReadOnlyPagedView<T>(span2d.Slice(row & _mask, col, len, 1));
            }

            return new ReadOnlyPagedView<T>(this, row, col, len);
        }

        #region Throw Helpers

        [DoesNotReturn]
        private static void ThrowIndexOutOfRangeException() =>
            throw new IndexOutOfRangeException("访问越界：超出了 PagedMemory2D 的有效范围。");

        [DoesNotReturn]
        private static void ThrowArgumentException(string msg) => throw new ArgumentException(msg);

        #endregion

        /// <inheritdoc />
        public void FlushAll()
        {
            // 获取当前页面数组的快照，避免遍历过程中数组引用变更
            var pagesSnapshot = Volatile.Read(ref _pages);
            // 确保逻辑边界不超出物理快照边界
            int count = Math.Min(_pageCount, pagesSnapshot.Length);
            for (int i = 0; i < count; i++)
            {
                _provider.Flush(pagesSnapshot[i], i);
            }

            // 保存元数据
            if (_provider is IPersistentMetadataProvider persistentProvider)
            {
                persistentProvider.SaveMetadata(Volatile.Read(ref _rowCount), _width, _pageSize);
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
}
