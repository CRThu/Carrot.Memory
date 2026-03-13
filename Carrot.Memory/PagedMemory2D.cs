using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Numerics;
using CommunityToolkit.HighPerformance;

namespace Carrot.Memory
{
    /// <summary>
    /// 提供一个基于分页机制的二维内存容器，支持动态行增长和高性能的行列切片访问。
    /// </summary>
    /// <typeparam name="T">存储的数据类型。</typeparam>
    public class PagedMemory2D<T>
    {
        private readonly List<Memory2D<T>> _pages = new();
        private readonly int _shift;
        private readonly int _mask;
        private readonly int _pageSize;
        private readonly int _width;
        private readonly Func<int, int, int, Memory2D<T>> _pageFactory;

        private int _rowCount = 0;

        /// <summary>
        /// 获取当前容器已存储的总行数。
        /// </summary>
        public int RowCount => _rowCount;

        /// <summary>
        /// 获取每一行的固定宽度。
        /// </summary>
        public int Width => _width;

        /// <summary>
        /// 初始化 <see cref="PagedMemory2D{T}"/> 类的新实例。
        /// </summary>
        /// <param name="width">每一行的列数（固定宽度）。</param>
        /// <param name="pageSize">分页行数（必须是 2 的幂，例如 8192）。</param>
        /// <param name="pageFactory">可选的页面创建工厂。参数为：(rows, cols, pageIndex)。</param>
        public PagedMemory2D(int width, int pageSize, Func<int, int, int, Memory2D<T>>? pageFactory = null)
        {
            if (pageSize <= 0 || (pageSize & (pageSize - 1)) != 0)
            {
                throw new ArgumentException("pageSize 必须是 2 的幂。", nameof(pageSize));
            }

            _width = width;
            _pageSize = pageSize;
            _shift = BitOperations.TrailingZeroCount((uint)pageSize);
            _mask = _pageSize - 1;

            // 默认实现：分配普通数组并转换为 Memory2D
            _pageFactory = pageFactory ?? ((rows, cols, index) => new T[rows * cols].AsMemory().AsMemory2D(rows, cols));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void EnsurePageExists(int pageIdx)
        {
            while (pageIdx >= _pages.Count)
            {
                int nextIndex = _pages.Count;
                _pages.Add(_pageFactory(_pageSize, _width, nextIndex));
            }
        }

        /// <summary>
        /// 向容器末尾添加一行数据。
        /// </summary>
        /// <param name="rowData">包含行数据的只读跨度，长度必须等于 <see cref="Width"/>。</param>
        /// <exception cref="ArgumentException">当输入数据的长度与容器宽度不匹配时抛出。</exception>
        public void AddRow(ReadOnlySpan<T> rowData)
        {
            if (rowData.Length != _width) ThrowArgumentException("行宽度不匹配");

            int pageIdx = _rowCount >> _shift;
            int rowInPage = _rowCount & _mask;

            EnsurePageExists(pageIdx);

            var span2d = _pages[pageIdx].Span;
            rowData.CopyTo(span2d.GetRowSpan(rowInPage));

            _rowCount++;
        }

        /// <summary>
        /// 向容器末尾批量添加多行数据。
        /// </summary>
        /// <param name="rowsData">包含多行数据的二维跨度，其宽度必须等于 <see cref="Width"/>。</param>
        /// <exception cref="ArgumentException">当输入数据的列宽与容器宽度不匹配时抛出。</exception>
        public void AddRows(ReadOnlySpan2D<T> rowsData)
        {
            if (rowsData.Width != _width) ThrowArgumentException("列宽不匹配");

            int rowsToAdd = rowsData.Height;
            int sourceRowOffset = 0;

            while (rowsToAdd > 0)
            {
                int pageIdx = _rowCount >> _shift;
                int rowInPage = _rowCount & _mask;

                EnsurePageExists(pageIdx);

                int canCopy = Math.Min(_pageSize - rowInPage, rowsToAdd);
                var targetSpan2d = _pages[pageIdx].Span;

                rowsData.Slice(sourceRowOffset, 0, canCopy, _width)
                    .CopyTo(targetSpan2d.Slice(rowInPage, 0, canCopy, _width));

                _rowCount += canCopy;
                sourceRowOffset += canCopy;
                rowsToAdd -= canCopy;
            }
        }

        /// <summary>
        /// 获取指定行号和列号的数据引用。
        /// </summary>
        /// <param name="r">行索引。</param>
        /// <param name="c">列索引。</param>
        /// <returns>指向数据的托管引用。</returns>
        public ref T this[int r, int c]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if ((uint)r >= (uint)_rowCount || (uint)c >= (uint)_width) ThrowIndexOutOfRangeException();
                // 使用 CollectionsMarshal 避免 List 内部的边界检查以提升性能
                var page = CollectionsMarshal.AsSpan(_pages)[r >> _shift].Span;
                return ref page[r & _mask, c];
            }
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
            if ((uint)row >= (uint)_rowCount || (uint)col + (uint)len > (uint)_width) ThrowIndexOutOfRangeException();
            var page = CollectionsMarshal.AsSpan(_pages)[row >> _shift].Span;
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
            if ((uint)col >= (uint)_width || (uint)row + (uint)len > (uint)_rowCount) ThrowIndexOutOfRangeException();

            int pageRowsLeft = _pageSize - (row & _mask);
            // 如果在同一页内，使用高效的 Span2D 模式
            if (len <= pageRowsLeft)
            {
                var span2d = CollectionsMarshal.AsSpan(_pages)[row >> _shift].Span;
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
    }

    /// <summary>
    /// 表示对 <see cref="PagedMemory2D{T}"/> 中某一部分数据的统一视图。
    /// 该结构为 ref struct，旨在提供零分配的切片访问。
    /// </summary>
    /// <typeparam name="T">数据类型。</typeparam>
    public readonly ref struct PagedView<T>
    {
        private readonly PagedMemory2D<T> _parent;
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
        internal PagedView(PagedMemory2D<T> parent, int r, int c, int len)
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