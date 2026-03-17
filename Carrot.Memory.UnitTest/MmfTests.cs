using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Carrot.Memory.UnitTest
{
    [TestClass]
    public class MmfTests
    {
        private string _testDir;

        [TestInitialize]
        public void Setup()
        {
            _testDir = Path.Combine(Path.GetTempPath(), "CarrotMemoryMmfTests_" + Guid.NewGuid().ToString("N"));
        }

        [TestCleanup]
        public void Cleanup()
        {
            if (Directory.Exists(_testDir))
            {
                try { Directory.Delete(_testDir, true); } catch { /* Ignore */ }
            }
        }

        [TestMethod]
        public void Mmf_BasicReadWrite_ShouldPersist()
        {
            int width = 10;
            int pageSize = 4; // 2^2
            
            // 第一阶段：写入数据
            using (var container = new PagedMemory2D<int>(width, pageSize, new MmfPageProvider<int>(_testDir)))
            {
                container.SetElement(0, 0, 100);
                container.SetElement(5, 5, 200); // 跨页
                container.SetElement(10, 9, 300); // 跨页
                container.FlushAll();
            }

            // 第二阶段：重新加载验证
            using (var container = new PagedMemory2D<int>(width, pageSize, new MmfPageProvider<int>(_testDir)))
            {
                Assert.AreEqual(11, container.RowCount);
                Assert.AreEqual(100, container[0, 0]);
                Assert.AreEqual(200, container[5, 5]);
                Assert.AreEqual(300, container[10, 9]);
            }
        }

        [TestMethod]
        public void Mmf_CrossMode_Verification()
        {
            // 验证 MMF 写入的数据是否可以用 Heap 模式加载（反之亦然，因为物理布局一致）
            int width = 4;
            int pageSize = 8;

            using (var container = new PagedMemory2D<int>(width, pageSize, new MmfPageProvider<int>(_testDir)))
            {
                container.SetElement(0, 0, 999);
                container.SetElement(10, 3, 888);
                container.FlushAll();
            }

            // 改用 FilePersistentHeapProvider 加载同一目录
            using (var container = new PagedMemory2D<int>(width, pageSize, new FilePersistentHeapProvider<int>(_testDir)))
            {
                Assert.AreEqual(11, container.RowCount);
                Assert.AreEqual(999, container[0, 0]);
                Assert.AreEqual(888, container[10, 3]);
            }
        }

        [TestMethod]
        public void Mmf_Dispose_ShouldCloseHandles()
        {
            // 这个测试确保 Dispose 后文件不再被占用
            string pageFile;
            using (var container = new PagedMemory2D<int>(10, 4, new MmfPageProvider<int>(_testDir)))
            {
                container.SetElement(0, 0, 1);
                container.FlushAll();
                pageFile = Path.Combine(_testDir, "page_0.dat");
                Assert.IsTrue(File.Exists(pageFile));
            }

            // Dispose 后应该可以删除或重命名文件
            try
            {
                File.Delete(pageFile);
            }
            catch (IOException ex)
            {
                Assert.Fail($"文件句柄未释放: {ex.Message}");
            }
        }
    }
}
