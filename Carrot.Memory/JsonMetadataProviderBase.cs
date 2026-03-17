using System;
using System.IO;
using System.Text.Json;
using CommunityToolkit.HighPerformance;

namespace Carrot.Memory
{
    /// <summary>
    /// 提供基于 JSON 的元数据管理抽象基类。
    /// 统一处理 metadata.json 的读取、保存与校验逻辑。
    /// </summary>
    /// <typeparam name="T">存储的数据类型。</typeparam>
    public abstract class JsonMetadataProviderBase<T> : IPersistentPageProvider<T>
    {
        protected readonly string _rootPath;
        protected readonly string _metadataPath;

        /// <summary>
        /// 初始化元数据供应者基类。
        /// </summary>
        /// <param name="rootPath">根目录路径。</param>
        protected JsonMetadataProviderBase(string rootPath)
        {
            _rootPath = rootPath;
            _metadataPath = Path.Combine(rootPath, "metadata.json");
            if (!Directory.Exists(rootPath)) Directory.CreateDirectory(rootPath);
        }

        /// <inheritdoc />
        public abstract Memory2D<T> Create(int rows, int cols, int index);

        /// <inheritdoc />
        public abstract void Flush(Memory2D<T> page, int index);

        /// <summary>
        /// 从 metadata.json 加载容器的逻辑状态。
        /// </summary>
        public virtual bool TryLoadMetadata(out int rowCount, out int width, out int pageSize)
        {
            rowCount = width = pageSize = 0;
            if (!File.Exists(_metadataPath)) return false;

            try
            {
                var json = File.ReadAllText(_metadataPath);
                var meta = JsonSerializer.Deserialize<Metadata>(json);
                if (meta == null) return false;

                rowCount = meta.RowCount;
                width = meta.Width;
                pageSize = meta.PageSize;
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 将容器当前的逻辑规模（行数、宽度、分页大小）持久化。
        /// </summary>
        public virtual void SaveMetadata(int rowCount, int width, int pageSize)
        {
            var meta = new Metadata { RowCount = rowCount, Width = width, PageSize = pageSize };
            var json = JsonSerializer.Serialize(meta, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_metadataPath, json);
        }

        /// <summary>
        /// 内部元数据模型。
        /// </summary>
        protected class Metadata
        {
            public int RowCount { get; set; }
            public int Width { get; set; }
            public int PageSize { get; set; }
        }
    }
}
