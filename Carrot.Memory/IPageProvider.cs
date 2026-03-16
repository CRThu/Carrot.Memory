using CommunityToolkit.HighPerformance;

namespace Carrot.Memory
{
    /// <summary>
    /// 分页供应者接口，支持自定义页面分配和刷新逻辑。
    /// 可以通过实现该接口来对接磁盘映射、非托管内存或远程存储。
    /// </summary>
    /// <typeparam name="T">页面中存储的数据类型。</typeparam>
    public interface IPageProvider<T>
    {
        /// <summary>
        /// 创建一个新的物理页面。
        /// </summary>
        /// <param name="rows">页面行数。</param>
        /// <param name="cols">页面宽度（列数）。</param>
        /// <param name="index">页面的全局索引（从 0 开始）。</param>
        /// <returns>分配的 <see cref="Memory2D{T}"/> 对象。</returns>
        Memory2D<T> Create(int rows, int cols, int index);
    }
}
