using System;
using CommunityToolkit.HighPerformance;

namespace Carrot.Memory
{
    /// <summary>
    /// 默认的堆内存页面供应者。
    /// 基于普通的 C# 托管数组分配内存，适用于简单的内存缓存场景。
    /// </summary>
    /// <typeparam name="T">存储的数据类型。</typeparam>
    public class DefaultHeapPageProvider<T> : IPageProvider<T>
    {
        /// <inheritdoc />
        public Memory2D<T> Create(int rows, int cols, int index) => 
            new T[rows * cols].AsMemory().AsMemory2D(rows, cols);

        /// <summary>
        /// 堆内存页面无需执行特殊的物理刷新操作。
        /// </summary>
        /// <param name="page">页面内存。</param>
        /// <param name="index">页面索引。</param>
        public void Flush(Memory2D<T> page, int index) { /* 堆内存由 GC 管理，无需物理刷新 */ }
    }
}
