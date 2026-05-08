using CommunityToolkit.HighPerformance;

namespace Carrot.Memory
{
    /// <summary>
    /// 支持持久化的分页供应者接口。
    /// 表明该供应者具备将内存数据同步到持久化存储的能力。
    /// </summary>
    /// <typeparam name="T">页面中存储的数据类型。</typeparam>
    public interface IPersistentPageProvider<T> : IPageProvider<T>
    {
        /// <summary>
        /// 将指定的内存页面内容刷新到持久化存储。
        /// </summary>
        void Flush(Memory2D<T> page, int index);
    }
}
