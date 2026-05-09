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
        /// 从指定目录加载元数据。
        /// </summary>
        public static T? Load<T>(string rootPath) where T : class
        {
            var metadataPath = Path.Combine(rootPath, MetadataFileName);
            if (!File.Exists(metadataPath)) return null;

            try
            {
                var json = File.ReadAllText(metadataPath);
                return JsonSerializer.Deserialize<T>(json);
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
        public static void Save<T>(string rootPath, T data) where T : class
        {
            if (string.IsNullOrEmpty(rootPath)) 
                throw new ArgumentException("保存元数据时 rootPath 不能为空。");

            if (!Directory.Exists(rootPath)) 
                Directory.CreateDirectory(rootPath);
            
            var metadataPath = Path.Combine(rootPath, MetadataFileName);

            var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
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
