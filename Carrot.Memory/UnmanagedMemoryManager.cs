using System;
using System.Buffers;
using System.Runtime.InteropServices;

namespace Carrot.Memory
{
    /// <summary>
    /// 非托管内存管理器，允许将非托管指针包装为 <see cref="Memory{T}"/>。
    /// </summary>
    /// <typeparam name="T">非托管数据类型。</typeparam>
    internal sealed unsafe class UnmanagedMemoryManager<T> : MemoryManager<T> where T : unmanaged
    {
        private readonly T* _pointer;
        private readonly int _length;

        /// <summary>
        /// 初始化 <see cref="UnmanagedMemoryManager{T}"/>。
        /// </summary>
        /// <param name="pointer">指向非托管内存的指针。</param>
        /// <param name="length">数据元素个数。</param>
        public UnmanagedMemoryManager(T* pointer, int length)
        {
            _pointer = pointer;
            _length = length;
        }

        /// <inheritdoc />
        public override Span<T> GetSpan() => new Span<T>(_pointer, _length);

        /// <inheritdoc />
        public override MemoryHandle Pin(int elementIndex = 0)
        {
            if (elementIndex < 0 || elementIndex >= _length)
                throw new ArgumentOutOfRangeException(nameof(elementIndex));

            return new MemoryHandle(_pointer + elementIndex);
        }

        /// <inheritdoc />
        public override void Unpin() { }

        /// <inheritdoc />
        protected override void Dispose(bool disposing) { }
    }
}
