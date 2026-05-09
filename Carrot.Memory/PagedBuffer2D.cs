using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Numerics;
using System.Threading;
using Carrot.Memory.Abstractions;
using Carrot.Memory.Views;
using CommunityToolkit.HighPerformance;

namespace Carrot.Memory
{
    /// <summary>
    /// 提供一个基于分页机制的二维内存容器，支持动态行增长和高性能的行列切片访问。
    /// 该类实现了双视图模式：通过显式接口实现支持只读访问，同时保留高性能的直接读写能力。
    /// </summary>
    /// <typeparam name="T">存储的数据类型。</typeparam>
    public class PagedBuffer2D<T> : IBuffer2D<T>
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
        private readonly string? _rootPath;

        private int _rowCount = 0;
        private bool _disposed;

        /// <inheritdoc />
        public int RowCount => Volatile.Read(ref _rowCount);

        /// <inheritdoc />
        public int Width => _width;

        /// <summary>
        /// 初始化 <see cref="PagedBuffer2D{T}"/> 类的新实例。
        /// </summary>
        /// <param name="options">配置选项。</param>
        /// <param name="provider">页面供应者。</param>
        /// <param name="initialRowCount">初始行数（通常从元数据恢复）。</param>
        public PagedBuffer2D(PagedBuffer2DOptions options, IPageProvider<T> provider, int initialRowCount = 0)
        {
            if (options.PageSize <= 0 || (options.PageSize & (options.PageSize - 1)) != 0)
            {
                throw new ArgumentException("pageSize 必须是 2 的幂。", nameof(options.PageSize));
            }

            _width = options.Width;
            _pageSize = options.PageSize;
            _shift = BitOperations.TrailingZeroCount((uint)_pageSize);
            _mask = _pageSize - 1;
            _pages = new Memory2D<T>[InitialPageCapacity];
            _provider = provider;
            _rootPath = options.RootPath;
            _rowCount = initialRowCount;

            // 如果有初始行数，预加载已有的分页
            if (_rowCount > 0)
            {
                _rwLock.EnterWriteLock();
                try
                {
                    EnsurePageExists((_rowCount - 1) >> _shift);
                }
                finally
                {
                    _rwLock.ExitWriteLock();
                }
            }
        }

        /// <summary>
        /// 获取当前实例的只读视图接口。
        /// </summary>
        public IReadOnlyBuffer2D<T> AsReadOnly() => this;

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
            if (_disposed) throw new ObjectDisposedException(nameof(PagedBuffer2D<T>));
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
            if (_disposed) throw new ObjectDisposedException(nameof(PagedBuffer2D<T>));
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
        ref readonly T IReadOnlyBuffer2D<T>.this[int r, int c] => ref this[r, c];


        /// <summary>
        /// 获取指定行中某一段的水平可写视图（行视图）。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public RowView<T> GetRowView(int row, int col, int len)
        {
            int rowCount = Volatile.Read(ref _rowCount);
            if ((uint)row >= (uint)rowCount || (uint)col + (uint)len > (uint)_width) ThrowIndexOutOfRangeException();
            var page = Volatile.Read(ref _pages)[row >> _shift].Span;
            return new RowView<T>(page.GetRowSpan(row & _mask).Slice(col, len));
        }

        /// <summary>
        /// 显式实现只读接口视图获取，返回只读行视图。
        /// </summary>
        ReadOnlyRowView<T> IReadOnlyBuffer2D<T>.GetRowView(int row, int col, int len)
        {
            int rowCount = Volatile.Read(ref _rowCount);
            if ((uint)row >= (uint)rowCount || (uint)col + (uint)len > (uint)_width) ThrowIndexOutOfRangeException();
            var page = Volatile.Read(ref _pages)[row >> _shift].Span;
            return new ReadOnlyRowView<T>(page.GetRowSpan(row & _mask).Slice(col, len));
        }

        /// <summary>
        /// 获取指定列中某一段的垂直可写视图（列视图），支持跨页。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ColumnView<T> GetColumnView(int row, int col, int len)
        {
            int rowCount = Volatile.Read(ref _rowCount);
            if ((uint)col >= (uint)_width || (uint)row + (uint)len > (uint)rowCount) ThrowIndexOutOfRangeException();

            return new ColumnView<T>(this, row, col, len);
        }

        /// <summary>
        /// 显式实现只读接口视图获取，返回只读列视图。
        /// </summary>
        ReadOnlyColumnView<T> IReadOnlyBuffer2D<T>.GetColumnView(int row, int col, int len)
        {
            int rowCount = Volatile.Read(ref _rowCount);
            if ((uint)col >= (uint)_width || (uint)row + (uint)len > (uint)rowCount) ThrowIndexOutOfRangeException();

            return new ReadOnlyColumnView<T>(this, row, col, len);
        }

        #region Throw Helpers

        [DoesNotReturn]
        private static void ThrowIndexOutOfRangeException() =>
            throw new IndexOutOfRangeException("访问越界：超出了 PagedBuffer2D 的有效范围。");

        [DoesNotReturn]
        private static void ThrowArgumentException(string msg) => throw new ArgumentException(msg);

        #endregion

        public void Commit()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(PagedBuffer2D<T>));

            _rwLock.EnterReadLock();
            try
            {
                // 1. 如果供应者支持物理刷新，则触发其同步逻辑
                if (_provider is IFlushable flushable)
                {
                    flushable.Flush();
                }

                // 2. 无论供应者是否支持物理同步，只要有根目录，就同步容器元数据
                if (_rootPath != null)
                {
                    MetadataManager.Save<PagedBuffer2DOptions>(_rootPath, new PagedBuffer2DOptions
                    {
                        Width = _width,
                        PageSize = _pageSize,
                        RootPath = _rootPath,
                        RowCount = Volatile.Read(ref _rowCount),
                        ProviderKey = _provider.ProviderKey
                    });
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"提交持久化操作失败: {ex.Message}", ex);
            }
            finally
            {
                _rwLock.ExitReadLock();
            }
        }

        /// <summary>
        /// 释放容器占用的资源，主要释放 ReaderWriterLockSlim 及 Provider 句柄。
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;
            
            // 释放前自动执行最后一次持久化提交
            try
            {
                Commit();
            }
            catch
            {
                // 在 Dispose 中忽略提交异常，防止阻塞释放流程
            }
            
            _rwLock.Dispose();

            // 若供应者持有非托管资源（如 MmfPageProvider），则执行释放
            if (_provider is IDisposable disposableProvider)
            {
                disposableProvider.Dispose();
            }

            _disposed = true;
        }
    }
}
