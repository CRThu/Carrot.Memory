namespace Carrot.Memory;

/// <summary>
/// 封装 Buffer2D 的初始化配置参数。
/// </summary>
public class Buffer2DOptions
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
    /// 标识存储策略（例如 "Heap"、"Mmf"）。
    /// </summary>
    public string ProviderKey { get; set; } = "Heap";

    /// <summary>
    /// 当前容器的行数（用于持久化恢复）。
    /// </summary>
    public int RowCount { get; set; }
}
