using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using CommunityToolkit.HighPerformance;

namespace Carrot.Memory
{
    /// <summary>
    /// 提供基于文件系统的持久化堆内存供应者。
    /// 核心机制：内存中使用托管堆作为高速缓存，磁盘中使用二进制文件进行冷备份。
    /// </summary>
    /// <typeparam name="T">存储的数据类型，必须是 unmanaged 以确保二进制序列化的跨平台一致性。</typeparam>
    public class FilePersistentHeapProvider<T> : JsonMetadataProviderBase<T> where T : unmanaged
    {
        /// <summary>
        /// 初始化持久化供应者。
        /// </summary>
        /// <param name="rootPath">存储数据文件与元数据的根目录路径。</param>
        public FilePersistentHeapProvider(string rootPath) : base(rootPath)
        {
        }

        /// <summary>
        /// 创建或加载一个页面。
        /// 若磁盘存在对应的 page_{index}.dat 文件，则自动反序列化到内存。
        /// </summary>
        public override Memory2D<T> Create(int rows, int cols, int index)
        {
            var data = new T[rows * cols];
            string pagePath = Path.Combine(_rootPath, $"page_{index}.dat");

            if (File.Exists(pagePath))
            {
                // 加载物理分页：直接将二进制流读入数组 Span
                using var fs = File.OpenRead(pagePath);
                var byteSpan = MemoryMarshal.AsBytes(data.AsSpan());
                fs.ReadExactly(byteSpan);
            }

            return data.AsMemory().AsMemory2D(rows, cols);
        }

        /// <summary>
        /// 将内存页面内容同步到磁盘二进制文件。
        /// </summary>
        public override void Flush(Memory2D<T> page, int index)
        {
            string pagePath = Path.Combine(_rootPath, $"page_{index}.dat");
            using var fs = File.Create(pagePath);
            
            var span2d = page.Span;
            if (span2d.TryGetSpan(out var span))
            {
                // 连续内存块直接全量写入
                fs.Write(MemoryMarshal.AsBytes(span));
            }
            else
            {
                // 非连续内存（如切片后的视图）则逐行遍历写入，确保物理布局正确
                for (int r = 0; r < span2d.Height; r++)
                {
                    fs.Write(MemoryMarshal.AsBytes(span2d.GetRowSpan(r)));
                }
            }
        }
    }
}

