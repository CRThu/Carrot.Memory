using System;
using System.IO;
using System.Text.Json;
using Carrot.Memory.Abstractions;
using Carrot.Memory.Providers;

namespace Carrot.Memory
{
    /// <summary>
    /// 提供对 <see cref="PagedPagedBuffer2D{T}"/> 的统一创建与加载工厂。
    /// </summary>
    public static class PagedBuffer2DFactory
    {
        /// <summary>
        /// 打开或创建一个二维分页内存容器。
        /// </summary>
        /// <typeparam name="T">存储的数据类型，必须是 unmanaged。</typeparam>
        /// <param name="path">持久化根目录路径。</param>
        /// <param name="options">可选的初始化配置。若元数据已存在，则优先从元数据恢复。</param>
        /// <returns>初始化完成的容器实例。</returns>
        public static PagedBuffer2D<T> Open<T>(string path, PagedBuffer2DOptions? options = null) 
            where T : unmanaged
        {
            var meta = MetadataManager.Load(path);
            IPageProvider<T> provider;

            if (meta != null)
            {
                provider = CreateProviderFromType<T>(meta.ProviderType, path);
                return new PagedBuffer2D<T>(meta.Width, meta.PageSize, provider, path);
            }
            else
            {
                // 新建逻辑
                options ??= new PagedBuffer2DOptions();
                provider = new MmfPageProvider<T>(path); // 默认策略
                return new PagedBuffer2D<T>(options.Width, options.PageSize, provider, path);
            }
        }

        private static IPageProvider<T> CreateProviderFromType<T>(string type, string path) where T : unmanaged
        {
            // 这里的 type 可能是类名全称或简称，或者是之前 nameof(MmfPageProvider<T>) 生成的字符串
            if (type.Contains("MmfPageProvider")) return new MmfPageProvider<T>(path);
            if (type.Contains("FileHeapProvider")) return new FileHeapProvider<T>(path);
            
            return new MmfPageProvider<T>(path);
        }
    }
}
