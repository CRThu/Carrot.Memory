using System;
using System.Collections.Generic;
using System.IO;
using System.IO.MemoryMappedFiles;
using CommunityToolkit.HighPerformance;

namespace Carrot.Memory
{
    /// <summary>
    /// 提供基于内存映射文件 (MMF) 的分页存储供应者。
    /// 核心机制：将磁盘文件直接映射到进程虚拟地址空间，由 OS 负责页面交换与物理同步。
    /// </summary>
    /// <typeparam name="T">存储的数据类型，必须是 unmanaged。</typeparam>
    public sealed class MmfPageProvider<T> : JsonMetadataProviderBase<T>, IDisposable where T : unmanaged
    {
        private readonly Dictionary<int, (MemoryMappedFile Mmf, MemoryMappedViewAccessor Accessor)> _pages = new();
        private bool _disposed;

        /// <summary>
        /// 初始化 MMF 供应者。
        /// </summary>
        /// <param name="rootPath">存储数据文件与元数据的根目录路径。</param>
        public MmfPageProvider(string rootPath) : base(rootPath)
        {
        }

        /// <summary>
        /// 创建或映射一个物理页面。
        /// </summary>
        public override unsafe Memory2D<T> Create(int rows, int cols, int index)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(MmfPageProvider<T>));
            if (_pages.ContainsKey(index)) return Memory2D<T>.Empty;

            string pagePath = Path.Combine(_rootPath, $"page_{index}.dat");
            long bytesNeeded = (long)rows * cols * sizeof(T);

            // 1. 预扩容与对齐检查：确保物理文件大小与逻辑参数严格匹配
            EnsureFilePrepared(pagePath, bytesNeeded);

            // 2. 映射内存
            var mmf = MemoryMappedFile.CreateFromFile(pagePath, FileMode.Open, null, bytesNeeded, MemoryMappedFileAccess.ReadWrite);
            var accessor = mmf.CreateViewAccessor(0, bytesNeeded, MemoryMappedFileAccess.ReadWrite);

            try
            {
                byte* ptr = null;
                accessor.SafeMemoryMappedViewHandle.AcquirePointer(ref ptr);
                
                var manager = new UnmanagedMemoryManager<T>((T*)ptr, rows * cols);
                var memory2d = manager.Memory.AsMemory2D(rows, cols);

                _pages[index] = (mmf, accessor);
                return memory2d;
            }
            catch
            {
                accessor.Dispose();
                mmf.Dispose();
                throw;
            }
        }

        private static void EnsureFilePrepared(string path, long expectedLength)
        {
            if (File.Exists(path))
            {
                var currentLength = new FileInfo(path).Length;
                if (currentLength != expectedLength)
                {
                    // 严格防御：若物理大小不符，说明数据布局已损坏或配置发生了漂移
                    // 直接抛出异常以保护用户数据不被静默截断或填充
                    throw new IOException($"数据页面文件大小校验失败。路径: {path}, 物理大小: {currentLength}, 预期大小: {expectedLength}。这通常意味着持久化配置已更改或文件遭受损坏。");
                }
            }
            else
            {
                // 仅在创建新页面时进行预扩容
                using var fs = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.ReadWrite);
                fs.SetLength(expectedLength);
            }
        }

        /// <summary>
        /// 强制 OS 将映射视图中的脏页刷新到磁盘。
        /// </summary>
        public override void Flush(Memory2D<T> page, int index)
        {
            if (_pages.TryGetValue(index, out var entry))
            {
                try
                {
                    entry.Accessor.Flush();
                }
                catch (ObjectDisposedException) { }
            }
        }

        /// <summary>
        /// 释放所有已打开的 MMF 句柄和访问器。
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;

            lock (_pages)
            {
                if (_disposed) return;
                _disposed = true;

                foreach (var (mmf, accessor) in _pages.Values)
                {
                    try
                    {
                        accessor.SafeMemoryMappedViewHandle.ReleasePointer();
                        accessor.Dispose();
                        mmf.Dispose();
                    }
                    catch { /* 忽略释放时的异常，确保所有句柄都能被尝试释放 */ }
                }
                _pages.Clear();
            }
        }
    }
}
