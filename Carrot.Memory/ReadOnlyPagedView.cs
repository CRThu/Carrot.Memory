using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using CommunityToolkit.HighPerformance;

namespace Carrot.Memory
{
    /// <summary>
    /// 表示对 <see cref="PagedMemory2D{T}"/> 中某一部分数据的只读视图。
    /// 该结构为 ref struct，提供零分配的切片访问，并强制执行只读语义。
    /// </summary>
    /// <typeparam name="T">数据类型。</typeparam>
    public readonly ref struct ReadOnlyPagedView<T>
    {
        private readonly IReadonlyPagedMemory2D<T> _parent;
        private readonly ReadOnlySpan<T> _rowSpan;
        private readonly ReadOnlySpan2D<T> _colSpan2d;
        private readonly int _r, _c;
        private readonly byte _mode;

        /// <summary>
        /// 获取视图中的元素总数。
        /// </summary>
        public int Length { get; }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal ReadOnlyPagedView(ReadOnlySpan<T> rowSpan)
        {
            _rowSpan = rowSpan;
            _colSpan2d = default;
            _parent = null!;
            Length = rowSpan.Length;
            _mode = 0;
            _r = _c = 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal ReadOnlyPagedView(ReadOnlySpan2D<T> colSpan2d)
        {
            _colSpan2d = colSpan2d;
            _rowSpan = default;
            _parent = null!;
            Length = colSpan2d.Height;
            _mode = 1;
            _r = _c = 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal ReadOnlyPagedView(IReadonlyPagedMemory2D<T> parent, int r, int c, int len)
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
        /// 获取视图中指定偏移位置的元素只读引用。
        /// </summary>
        /// <param name="i">视图内的逻辑索引。</param>
        /// <returns>数据的只读引用。</returns>
        public ref readonly T this[int i]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if ((uint)i >= (uint)Length) ThrowIndexOutOfRangeException();

                if (_mode == 0) return ref _rowSpan[i];
                if (_mode == 1) return ref _colSpan2d[i, 0];
                return ref _parent[_r + i, _c];
            }
        }

        /// <summary>
        /// 将视图转换为 <see cref="ReadOnlySpan{T}"/>。仅当视图为单行水平切片时支持。
        /// </summary>
        /// <returns>对应的只读 Span。</returns>
        /// <exception cref="NotSupportedException">当视图不是单行切片时抛出。</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadOnlySpan<T> AsSpan() =>
            _mode == 0 ? _rowSpan : throw new NotSupportedException("仅行视图（GetRowView）可转 Span，列视图或跨页视图不支持此操作。");

        [DoesNotReturn]
        private static void ThrowIndexOutOfRangeException() => throw new IndexOutOfRangeException("视图访问越界。");
    }
}
