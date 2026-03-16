using System;
using CommunityToolkit.HighPerformance;

namespace Carrot.Memory
{
    /// <summary>
    /// 提供对二维分页内存的读写访问接口。
    /// 该接口继承自 <see cref="IReadonlyPagedMemory2D{T}"/>，并提供了修改数据的能力。
    /// </summary>
    /// <typeparam name="T">存储的数据类型。</typeparam>
    public interface IPagedMemory2D<T> : IReadonlyPagedMemory2D<T>, IDisposable
    {
        /// <summary>
        /// 获取指定行号和列号的数据引用。
        /// 通过该引用可以直接修改内存中的数据。
        /// </summary>
        /// <param name="r">行索引。</param>
        /// <param name="c">列索引。</param>
        /// <returns>数据的可写引用。</returns>
        new ref T this[int r, int c] { get; }

        /// <summary>
        /// 获取指定行中某一段的水平可写视图（行视图）。
        /// </summary>
        /// <param name="row">起始行索引。</param>
        /// <param name="col">起始列索引。</param>
        /// <param name="len">截取长度。</param>
        /// <returns>对应的可写视图对象。</returns>
        new PagedView<T> GetRowView(int row, int col, int len);

        /// <summary>
        /// 获取指定列中某一段的垂直可写视图（列视图），支持跨页。
        /// </summary>
        /// <param name="row">起始行索引。</param>
        /// <param name="col">列索引。</param>
        /// <param name="len">垂直截取长度。</param>
        /// <returns>对应的可写视图对象。</returns>
        new PagedView<T> GetColumnView(int row, int col, int len);

        /// <summary>
        /// 在指定位置设置单个元素。
        /// </summary>
        /// <param name="r">行索引。</param>
        /// <param name="c">列索引。</param>
        /// <param name="value">要设置的值。</param>
        void SetElement(int r, int c, T value);

        /// <summary>
        /// 将二维数据块写入指定位置。
        /// </summary>
        /// <param name="r">起始行。</param>
        /// <param name="c">起始列。</param>
        /// <param name="data">待写入的数据块。</param>
        void SetBlock(int r, int c, ReadOnlySpan2D<T> data);

        /// <summary>
        /// 在指定位置设置单行数据块（水平写入）。
        /// </summary>
        /// <param name="r">行索引。</param>
        /// <param name="c">起始列索引。</param>
        /// <param name="data">待写入的一维数据。</param>
        void SetRow(int r, int c, ReadOnlySpan<T> data);

        /// <summary>
        /// 在指定位置设置单列数据块（垂直写入）。
        /// </summary>
        /// <param name="r">起始行索引。</param>
        /// <param name="c">列索引。</param>
        /// <param name="data">待写入的一维数据。</param>
        void SetColumn(int r, int c, ReadOnlySpan<T> data);

        /// <summary>
        /// 刷新所有页面到持久化层。
        /// </summary>
        void FlushAll();
    }
}
