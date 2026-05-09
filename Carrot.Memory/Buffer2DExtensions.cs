using System;
using System.Runtime.CompilerServices;
using Carrot.Memory.Abstractions;
using CommunityToolkit.HighPerformance;

namespace Carrot.Memory
{
    /// <summary>
    /// 提供对 <see cref="IBuffer2D{T}"/> 的便捷扩展方法。
    /// </summary>
    public static class Buffer2DExtensions
    {
        /// <summary>
        /// 在指定位置设置单行数据块（水平写入）。
        /// </summary>
        /// <typeparam name="T">存储的数据类型。</typeparam>
        /// <param name="container">目标容器。</param>
        /// <param name="r">行索引。</param>
        /// <param name="c">起始列索引。</param>
        /// <param name="data">待写入的一维数据。</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetRow<T>(this IBuffer2D<T> container, int r, int c, ReadOnlySpan<T> data)
        {
            container.SetBlock(r, c, data.AsSpan2D(1, data.Length));
        }

        /// <summary>
        /// 在指定位置设置单列数据块（垂直写入）。
        /// </summary>
        /// <typeparam name="T">存储的数据类型。</typeparam>
        /// <param name="container">目标容器。</param>
        /// <param name="r">起始行索引。</param>
        /// <param name="c">列索引。</param>
        /// <param name="data">待写入的一维数据。</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetColumn<T>(this IBuffer2D<T> container, int r, int c, ReadOnlySpan<T> data)
        {
            container.SetBlock(r, c, data.AsSpan2D(data.Length, 1));
        }
    }
}
