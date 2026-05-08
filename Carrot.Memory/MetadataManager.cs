using System;
using System.IO;
using System.Text.Json;

namespace Carrot.Memory
{
    /// <summary>
    /// 元数据管理器，负责容器逻辑状态的持久化与加载。
    /// </summary>
    internal static class MetadataManager
    {
        private const string MetadataFileName = "metadata.json";

        /// <summary>
        /// 内部元数据模型。
        /// </summary>
        public class Metadata
        {
            public int RowCount { get; set; }
            public int Width { get; set; }
            public int PageSize { get; set; }
            public string ProviderType { get; set; } = "Default";
        }

        /// <summary>
        /// 从指定目录加载元数据。
        /// </summary>
        public static Metadata Load(string rootPath)
        {
            var metadataPath = Path.Combine(rootPath, MetadataFileName);
            if (!File.Exists(metadataPath)) return null;

            try
            {
                var json = File.ReadAllText(metadataPath);
                return JsonSerializer.Deserialize<Metadata>(json);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 将元数据持久化到指定目录。
        /// 采用“临时文件 + 覆盖重命名”策略确保写入的原子性。
        /// </summary>
        public static void Save(string rootPath, int rowCount, int width, int pageSize, string providerType)
        {
            if (!Directory.Exists(rootPath)) Directory.CreateDirectory(rootPath);
            var metadataPath = Path.Combine(rootPath, MetadataFileName);

            var meta = new Metadata
            {
                RowCount = rowCount,
                Width = width,
                PageSize = pageSize,
                ProviderType = providerType
            };

            var json = JsonSerializer.Serialize(meta, new JsonSerializerOptions { WriteIndented = true });
            string tmpPath = metadataPath + ".tmp";

            try
            {
                File.WriteAllText(tmpPath, json);
                File.Move(tmpPath, metadataPath, overwrite: true);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"持久化元数据失败: {ex.Message}", ex);
            }
            finally
            {
                if (File.Exists(tmpPath))
                {
                    try { File.Delete(tmpPath); } catch { /* 忽略清理错误 */ }
                }
            }
        }
    }
}
