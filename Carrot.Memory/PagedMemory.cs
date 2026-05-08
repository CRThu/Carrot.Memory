using System;

namespace Carrot.Memory
{
    /// <summary>
    /// 提供对 <see cref="PagedMemory2D{T}"/> 的统一创建与加载工厂。
    /// </summary>
    public static class PagedMemory
    {
        /// <summary>
        /// 打开或创建一个二维分页内存容器。
        /// </summary>
        /// <typeparam name="T">存储的数据类型，必须是 unmanaged。</typeparam>
        /// <param name="path">持久化根目录路径。</param>
        /// <param name="width">容器宽度（列数）。</param>
        /// <param name="pageSize">分页大小（行数）。</param>
        /// <param name="provider">可选的页面供应者。如果为 null 且指定了路径，则默认使用 <see cref="MmfPageProvider{T}"/>。</param>
        /// <returns>初始化完成的容器实例。</returns>
        public static PagedMemory2D<T> Open<T>(string path, int width, int pageSize, IPageProvider<T>? provider = null) 
            where T : unmanaged
        {
            // 若未指定 provider，则根据路径自动选择超高性能的 MMF 供应者
            var actualProvider = provider ?? new MmfPageProvider<T>(path);
            
            // 构造容器：PagedMemory2D 内部构造函数会自动处理元数据加载与校验
            return new PagedMemory2D<T>(width, pageSize, actualProvider);
        }
    }
}
