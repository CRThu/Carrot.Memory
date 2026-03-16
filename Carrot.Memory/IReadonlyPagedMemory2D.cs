using CommunityToolkit.HighPerformance;

namespace Carrot.Memory
{
    /// <summary>
    /// 提供对二维分页内存的只读访问接口。
    /// 通过 ref readonly 索引器确保物理层面的只读隔离。
    /// </summary>
    /// <typeparam name="T">存储的数据类型。</typeparam>
    public interface IReadonlyPagedMemory2D<T>
    {
        /// <summary>
        /// 获取当前容器已存储的总行数。
        /// </summary>
        int RowCount { get; }

        /// <summary>
        /// 获取每一行的固定宽度。
        /// </summary>
        int Width { get; }

        /// <summary>
        /// 获取指定行号和列号的数据的只读引用。
        /// </summary>
        /// <param name="r">行索引。</param>
        /// <param name="c">列索引。</param>
        /// <returns>数据的只读引用。</returns>
        ref readonly T this[int r, int c] { get; }

        /// <summary>
        /// 获取指定行中某一段的水平只读视图（行视图）。
        /// </summary>
        /// <param name="row">起始行索引。</param>
        /// <param name="col">起始列索引。</param>
        /// <param name="len">截取长度。</param>
        /// <returns>对应的只读视图对象。</returns>
        ReadOnlyPagedView<T> GetRowView(int row, int col, int len);

        /// <summary>
        /// 获取指定列中某一段的垂直只读视图（列视图），支持跨页。
        /// </summary>
        /// <param name="row">起始行索引。</param>
        /// <param name="col">列索引。</param>
        /// <param name="len">垂直截取长度。</param>
        /// <returns>对应的只读视图对象。</returns>
        ReadOnlyPagedView<T> GetColumnView(int row, int col, int len);
    }
}
