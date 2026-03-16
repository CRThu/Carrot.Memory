using CommunityToolkit.HighPerformance;

namespace Carrot.Memory
{
    /// <summary>
    /// 定义非泛型的元数据持久化协议。
    /// 分离该接口是为了允许 PagedMemory2D 在不了解具体类型泛型约束的情况下，
    /// 能够统一进行元数据的安全加载与同步。
    /// </summary>
    public interface IPersistentMetadataProvider
    {
        /// <summary>
        /// 尝试从存储介质恢复容器的逻辑状态。
        /// </summary>
        /// <param name="rowCount">已存储的有效行数。</param>
        /// <param name="width">已存储的物理宽度。</param>
        /// <param name="pageSize">已存储的分页规模。</param>
        /// <returns>若元数据文件存在且格式正确，返回 true。</returns>
        bool TryLoadMetadata(out int rowCount, out int width, out int pageSize);

        /// <summary>
        /// 将容器当前的逻辑状态同步到存储介质。
        /// </summary>
        /// <param name="rowCount">当前逻辑行数。</param>
        /// <param name="width">容器宽度。</param>
        /// <param name="pageSize">分页行数。</param>
        void SaveMetadata(int rowCount, int width, int pageSize);
    }

    /// <summary>
    /// 支持全量持久化的分页供应者接口。
    /// 继承自 <see cref="IPageProvider{T}"/> 并引入元数据同步能力。
    /// </summary>
    /// <typeparam name="T">页面中存储的数据类型，由于涉及二进制 I/O，必须为 unmanaged。</typeparam>
    public interface IPersistentPageProvider<T> : IPageProvider<T>, IPersistentMetadataProvider where T : unmanaged
    {
    }
}
