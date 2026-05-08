using System;
using System.IO;
using System.Text.Json;

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
        /// <param name="options">可选的初始化配置。若元数据已存在，则优先从元数据恢复。</param>
        /// <returns>初始化完成的容器实例。</returns>
        public static PagedMemory2D<T> Open<T>(string path, PagedMemoryOptions? options = null) 
            where T : unmanaged
        {
            var metaPath = Path.Combine(path, "metadata.json");
            IPageProvider<T> provider;

            if (File.Exists(metaPath))
            {
                try
                {
                    var meta = LoadMetadataInternal(metaPath);
                    provider = CreateProviderFromType<T>(meta.ProviderType, path);
                    return new PagedMemory2D<T>(meta.Width, meta.PageSize, provider);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException($"无法从路径恢复容器: {path}", ex);
                }
            }
            else
            {
                // 新建逻辑
                options ??= new PagedMemoryOptions();
                provider = new MmfPageProvider<T>(path); // 默认策略
                return new PagedMemory2D<T>(options.Width, options.PageSize, provider);
            }
        }

        private static JsonMetadataProviderBase.Metadata LoadMetadataInternal(string path)
        {
            var json = File.ReadAllText(path);
            var meta = JsonSerializer.Deserialize<JsonMetadataProviderBase.Metadata>(json);
            return meta ?? throw new IOException("元数据格式错误。");
        }

        private static IPageProvider<T> CreateProviderFromType<T>(string type, string path) where T : unmanaged
        {
            return type switch
            {
                nameof(MmfPageProvider<T>) => new MmfPageProvider<T>(path),
                nameof(FilePersistentHeapProvider<T>) => new FilePersistentHeapProvider<T>(path),
                _ => new MmfPageProvider<T>(path)
            };
        }
    }
}
