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
        /// <param name="overrides">可选的初始化配置。若元数据已存在，则优先从元数据恢复，overrides 仅在新建或作为补充时生效。</param>
        /// <returns>初始化完成的容器实例。</returns>
        public static PagedBuffer2D<T> Open<T>(string path, PagedBuffer2DOptions? overrides = null) 
            where T : unmanaged
        {
            // 优先级：磁盘元数据 > 传入的 overrides > 默认配置
            var options = MetadataManager.Load(path) ?? overrides ?? new PagedBuffer2DOptions { RootPath = path };
            
            // 确保 RootPath 被正确设置
            options.RootPath = path;

            var provider = CreateProviderFromType<T>(options.ProviderType, path);
            
            // 使用 options 中的 RowCount 初始化容器
            return new PagedBuffer2D<T>(options, provider, options.RowCount);
        }

        private static IPageProvider<T> CreateProviderFromType<T>(string type, string path) where T : unmanaged
        {
            // 简单的映射逻辑，可根据需要扩展
            if (string.IsNullOrEmpty(type) || type.Contains("MmfPageProvider")) 
                return new MmfPageProvider<T>(path);
            
            if (type.Contains("FileHeapProvider")) 
                return new FileHeapProvider<T>(path);

            if (type.Contains("HeapProvider"))
                return new HeapProvider<T>();
            
            return new MmfPageProvider<T>(path);
        }
    }
}
