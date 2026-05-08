namespace Carrot.Memory;

/// <summary>
/// 封装 PagedMemory2D 的初始化配置参数。
/// </summary>
public class PagedMemoryOptions
{
    /// <summary>
    /// 容器宽度（列数）。默认为 1024。
    /// </summary>
    public int Width { get; set; } = 1024;

    /// <summary>
    /// 每个分页包含的行数。必须是 2 的幂。默认为 1024。
    /// </summary>
    public int PageSize { get; set; } = 1024;

    /// <summary>
    /// 数据持久化的根目录路径。
    /// </summary>
    public string? RootPath { get; set; }
}
