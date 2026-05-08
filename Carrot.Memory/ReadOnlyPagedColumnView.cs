using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Carrot.Memory
{
    /// <summary>
    /// 专用于列访问的只读静态视图类型，封装行列索引与 <see cref="IReadonlyPagedMemory2D{T}"/> 的寻址逻辑。
    /// </summary>
    /// <typeparam name="T">数据类型。</typeparam>
    public readonly ref struct ReadOnlyPagedColumnView<T>
    {
        private readonly IReadonlyPagedMemory2D<T> _parent;
        private readonly int _r, _c, _len;

        /// <summary>
        /// 获取视图中的元素总数。
        /// </summary>
        public int Length => _len;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal ReadOnlyPagedColumnView(IReadonlyPagedMemory2D<T> parent, int r, int c, int len)
        {
            _parent = parent;
            _r = r;
            _c = c;
            _len = len;
        }

        /// <summary>
        /// 获取视图中指定偏移位置的元素只读引用。
        /// </summary>
        /// <param name="i">索引。</param>
        /// <returns>数据的只读引用。</returns>
        public ref readonly T this[int i]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if ((uint)i >= (uint)_len) ThrowIndexOutOfRangeException();
                return ref _parent[_r + i, _c];
            }
        }

        [DoesNotReturn]
        private static void ThrowIndexOutOfRangeException() => throw new IndexOutOfRangeException("视图访问越界。");
    }
}
