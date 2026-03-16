using CommunityToolkit.HighPerformance;

namespace Carrot.Memory
{
    /// <summary>
    /// 支持持久化的分页供应者接口。
    /// 集成了元数据管理与物理页刷新功能。
    /// </summary>
    /// <typeparam name="T">页面中存储的数据类型。</typeparam>
    public interface IPersistentPageProvider<T> : IPageProvider<T>
    {
        /// <summary>
        /// 尝试从存储介质恢复容器的逻辑状态（行数、宽度、分页大小）。
        /// </summary>
        bool TryLoadMetadata(out int rowCount, out int width, out int pageSize);

        /// <summary>
        /// 将容器当前的逻辑状态同步到存储介质。
        /// </summary>
        void SaveMetadata(int rowCount, int width, int pageSize);

        /// <summary>
        /// 将指定的内存页面内容刷新到持久化存储。
        /// </summary>
        void Flush(Memory2D<T> page, int index);
    }
}
