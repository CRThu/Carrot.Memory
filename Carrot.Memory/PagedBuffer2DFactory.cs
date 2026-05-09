using System;
using System.IO;
using System.Text.Json;
using System.Collections.Concurrent;
using Carrot.Memory.Abstractions;
using Carrot.Memory.Providers;

namespace Carrot.Memory
{
    /// <summary>
    /// 提供对 <see cref="PagedBuffer2D{T}"/> 的统一创建与加载工厂。
    /// </summary>
    public static class PagedBuffer2DFactory
    {
        private static class ProviderRegistry<T> where T : unmanaged
        {
            public static readonly ConcurrentDictionary<string, Func<string, IPageProvider<T>>> Map = new();

            static ProviderRegistry()
            {
                // 预先注册内置 Provider
                Map[MmfPageProvider<T>.Key] = path => new MmfPageProvider<T>(path);
                Map[FileHeapProvider<T>.Key] = path => new FileHeapProvider<T>(path);
                Map[HeapProvider<T>.Key] = _ => new HeapProvider<T>();
            }
        }

        /// <summary>
        /// 注册自定义存储介质供应者。
        /// </summary>
        /// <param name="providerKey">Provider 的唯一名称（对应 Options 中的 ProviderKey）。</param>
        /// <param name="factory">生成 Provider 实例的工厂方法。输入参数为 rootPath，输出为 IPageProvider 实例。</param>
        public static void RegisterProvider<T>(string providerKey, Func<string, IPageProvider<T>> factory)
            where T : unmanaged
        {
            ProviderRegistry<T>.Map[providerKey] = factory;
        }

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
            var options = MetadataManager.Load<PagedBuffer2DOptions>(path) ?? overrides ?? new PagedBuffer2DOptions { RootPath = path };
            
            // 确保 RootPath 被正确设置
            options.RootPath = path;

            // 从注册表中获取对应的工厂方法，采用精确匹配
            if (!ProviderRegistry<T>.Map.TryGetValue(options.ProviderKey, out var factory))
            {
                throw new NotSupportedException($"未找到类型为 '{options.ProviderKey}' 的存储供应者注册。");
            }

            var provider = factory(path);
            
            // 使用 options 中的 RowCount 初始化容器
            return new PagedBuffer2D<T>(options, provider, options.RowCount);
        }
    }
}
