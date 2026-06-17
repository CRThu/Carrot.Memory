using System;
using System.Collections.Concurrent;
using Carrot.Memory.Abstractions;
using Carrot.Memory.Providers;

namespace Carrot.Memory;

/// <summary>
/// Buffer2D 统一入口，隐藏底层 PagedBuffer2D 细节。
/// </summary>
public static class Buffer2D
{
    private static class ProviderRegistry<T> where T : unmanaged
    {
        public static readonly ConcurrentDictionary<string, Func<string, IPageProvider<T>>> Map = new();

        static ProviderRegistry()
        {
            Map[MmfPageProvider<T>.Key] = path => new MmfPageProvider<T>(path);
            Map[FileHeapProvider<T>.Key] = path => new FileHeapProvider<T>(path);
            Map[HeapProvider<T>.Key] = _ => new HeapProvider<T>();
        }
    }

    /// <summary>
    /// 注册自定义存储介质供应者。
    /// </summary>
    public static void RegisterProvider<T>(string providerKey, Func<string, IPageProvider<T>> factory)
        where T : unmanaged
    {
        ProviderRegistry<T>.Map[providerKey] = factory;
    }

    /// <summary>
    /// 创建一个纯内存的二维分页容器，零磁盘依赖。
    /// </summary>
    public static PagedBuffer2D<T> Create<T>(int width, int rowCount, int pageSize = 1024, string? persistPath = null)
        where T : unmanaged
    {
        var options = new Buffer2DOptions
        {
            Width = width,
            PageSize = pageSize,
            RowCount = rowCount,
            ProviderKey = HeapProvider<T>.Key
        };
        var provider = new HeapProvider<T>();
        return new PagedBuffer2D<T>(options, provider, rowCount, persistPath);
    }

    /// <summary>
    /// 从磁盘恢复一个二维分页内存容器。
    /// </summary>
    public static PagedBuffer2D<T> Open<T>(string path, Buffer2DOptions? overrides = null)
        where T : unmanaged
    {
        var options = MetadataManager.Load<Buffer2DOptions>(path) ?? overrides ?? new Buffer2DOptions();

        if (!ProviderRegistry<T>.Map.TryGetValue(options.ProviderKey, out var factory))
            throw new NotSupportedException($"未找到类型为 '{options.ProviderKey}' 的存储供应者注册。");

        var provider = factory(path);
        return new PagedBuffer2D<T>(options, provider, options.RowCount, path);
    }
}
