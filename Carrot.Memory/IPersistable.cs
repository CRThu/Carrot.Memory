namespace Carrot.Memory
{
    /// <summary>
    /// 定义可持久化提交的契约。
    /// </summary>
    public interface IPersistable
    {
        /// <summary>
        /// 提交当前所有的内存更改到持久化存储。
        /// 该操作应当保证数据页与元数据的同步原子性。
        /// </summary>
        void Commit();
    }
}
