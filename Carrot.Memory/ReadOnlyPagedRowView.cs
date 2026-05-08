using System;
using System.Runtime.CompilerServices;

namespace Carrot.Memory
{
    /// <summary>
    /// 专用于行访问的只读静态视图类型，直接包装 <see cref="ReadOnlySpan{T}"/>。
    /// </summary>
    /// <typeparam name="T">数据类型。</typeparam>
    public readonly ref struct ReadOnlyPagedRowView<T>
    {
        private readonly ReadOnlySpan<T> _span;

        /// <summary>
        /// 获取视图中的元素总数。
        /// </summary>
        public int Length => _span.Length;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal ReadOnlyPagedRowView(ReadOnlySpan<T> span) => _span = span;

        /// <summary>
        /// 获取视图中指定偏移位置的元素只读引用。
        /// </summary>
        /// <param name="i">索引。</param>
        /// <returns>数据的只读引用。</returns>
        public ref readonly T this[int i]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref _span[i];
        }

        /// <summary>
        /// 将视图转换为 <see cref="ReadOnlySpan{T}"/>。
        /// </summary>
        /// <returns>对应的只读 Span。</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadOnlySpan<T> AsSpan() => _span;
    }
}
