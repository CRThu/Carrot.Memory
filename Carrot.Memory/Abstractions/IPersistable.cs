namespace Carrot.Memory.Abstractions
{
    /// <summary>
    /// 定义可持久化提交的契约。
    /// </summary>
    public interface IPersistable
    {
        /// <summary>
        /// 将当前内存更改持久化到已绑定的路径。
        /// </summary>
        void Commit();
    }
}
