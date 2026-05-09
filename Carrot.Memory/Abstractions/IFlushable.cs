namespace Carrot.Memory.Abstractions
{
    /// <summary>
    /// 定义支持物理数据刷新的契约。
    /// 通常由 Provider 实现，用于将内存缓冲区的数据同步到持久化介质。
    /// </summary>
    public interface IFlushable
    {
        /// <summary>
        /// 触发物理刷新操作，将所有挂起的内存更改写入底层存储。
        /// </summary>
        void Flush();
    }
}
