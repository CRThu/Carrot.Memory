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
            if (_pages.ContainsKey(index)) return Memory2D<T>.Empty; // 不应重复创建

            string pagePath = Path.Combine(_rootPath, $"page_{index}.dat");
            long bytesNeeded = (long)rows * cols * sizeof(T);

            // 确保文件存在并扩容到预定大小
            // 使用 FileStream 预分配空间可以减少碎片并提高 MMF 稳定性
            using (var fs = ArrayPoolFileStream(pagePath, bytesNeeded)) { }

            var mmf = MemoryMappedFile.CreateFromFile(pagePath, FileMode.Open, null, bytesNeeded, MemoryMappedFileAccess.ReadWrite);
            var accessor = mmf.CreateViewAccessor(0, bytesNeeded, MemoryMappedFileAccess.ReadWrite);

            byte* ptr = null;
            accessor.SafeMemoryMappedViewHandle.AcquirePointer(ref ptr);
            
            // 包装为 Memory2D
            var manager = new UnmanagedMemoryManager<T>((T*)ptr, rows * cols);
            var memory2d = manager.Memory.AsMemory2D(rows, cols);

            _pages[index] = (mmf, accessor);
            return memory2d;
        }

        private static FileStream ArrayPoolFileStream(string path, long length)
        {
            var fs = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
            if (fs.Length < length)
            {
                fs.SetLength(length);
            }
            return fs;
        }

        /// <summary>
        /// 强制 OS 将映射视图中的脏页刷新到磁盘。
        /// </summary>
        public override void Flush(Memory2D<T> page, int index)
        {
            if (_pages.TryGetValue(index, out var entry))
            {
                entry.Accessor.Flush();
            }
        }

        /// <summary>
        /// 释放所有已打开的 MMF 句柄和访问器。
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;

            foreach (var entry in _pages.Values)
            {
                entry.Accessor.SafeMemoryMappedViewHandle.ReleasePointer();
                entry.Accessor.Dispose();
                entry.Mmf.Dispose();
            }
            _pages.Clear();
            _disposed = true;
        }
    }
}
