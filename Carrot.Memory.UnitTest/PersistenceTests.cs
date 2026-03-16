using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Carrot.Memory.UnitTest
{
    [TestClass]
    public class PersistenceTests
    {
        private string _currentTestPath = string.Empty;
        private const int PageSize = 1024;
        private const int Width = 10;

        public TestContext TestContext { get; set; } = null!;

        [TestInitialize]
        public void Setup()
        {
            // 为每个测试方法创建独立的目录名，避免并行冲突
            _currentTestPath = Path.Combine("test_db", TestContext.TestName ?? Guid.NewGuid().ToString());
            if (Directory.Exists(_currentTestPath))
            {
                Directory.Delete(_currentTestPath, true);
            }
            Directory.CreateDirectory(_currentTestPath);
        }

        [TestCleanup]
        public void Cleanup()
        {
            try
            {
                if (Directory.Exists(_currentTestPath))
                {
                    Directory.Delete(_currentTestPath, true);
                }
            }
            catch { /* 忽略占用错误，Setup 时会再次尝试清理 */ }
        }

        [TestMethod]
        public void Persistence_RestartSimulation_ShouldRecoverData()
        {
            // 阶段 1：创建容器 A，写入数据并持久化
            {
                var provider = new FilePersistentHeapProvider<int>(_currentTestPath);
                using var pagedA = new PagedMemory2D<int>(Width, PageSize, provider);
                
                pagedA.SetElement(0, 0, 123);
                pagedA.SetElement(PageSize + 5, 2, 456);
                pagedA.SetElement(100, 1, 789);

                Assert.AreEqual(PageSize + 6, pagedA.RowCount);
                
                pagedA.FlushAll();
            }

            // 阶段 2：创建容器 B，指向相同路径，验证自动恢复
            {
                var provider = new FilePersistentHeapProvider<int>(_currentTestPath);
                using var pagedB = new PagedMemory2D<int>(Width, PageSize, provider);

                // 验证 RowCount 恢复
                Assert.AreEqual(PageSize + 6, pagedB.RowCount, "RowCount 恢复失败");

                // 验证内容恢复
                Assert.AreEqual(123, pagedB[0, 0], "页 0 数据恢复失败");
                Assert.AreEqual(789, pagedB[100, 1], "页 0 跨行数据恢复失败");
                Assert.AreEqual(456, pagedB[PageSize + 5, 2], "页 1 数据恢复失败");
            }
        }

        [TestMethod]
        public void Persistence_ConfigMismatch_ShouldThrow()
        {
            // 阶段 1：写入原始配置
            {
                var provider = new FilePersistentHeapProvider<int>(_currentTestPath);
                using var pagedA = new PagedMemory2D<int>(Width, PageSize, provider);
                pagedA.FlushAll();
            }

            // 阶段 2：以错误配置启动
            {
                var provider = new FilePersistentHeapProvider<int>(_currentTestPath);
                bool caught = false;
                try
                {
                    using var pagedB = new PagedMemory2D<int>(Width + 1, PageSize, provider);
                }
                catch (InvalidOperationException)
                {
                    caught = true;
                }
                Assert.IsTrue(caught, "应该抛出 InvalidOperationException 但实际没有。");
            }
        }
    }
}
