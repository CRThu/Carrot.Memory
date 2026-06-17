using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Carrot.Memory;
using Carrot.Memory.Providers;

namespace Carrot.Memory.UnitTest
{
    [TestClass]
    public class MmfTests
    {
        private string? _testDir;

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
            var options = new Buffer2DOptions { Width = width, PageSize = pageSize };
            using (var container = new PagedBuffer2D<int>(options, new MmfPageProvider<int>(_testDir!), persistPath: _testDir))
            {
                container.SetElement(0, 0, 100);
                container.SetElement(5, 5, 200); // 跨页
                container.SetElement(10, 9, 300); // 跨页
                container.Commit();
            }

            // 第二阶段：重新加载验证
            using (var container = Buffer2D.Open<int>(_testDir!))
            {
                Assert.AreEqual(11, container.RowCount);
                Assert.AreEqual(100, container[0, 0]);
                Assert.AreEqual(200, container[5, 5]);
                Assert.AreEqual(300, container[10, 9]);
            }
        }

        [TestMethod]
        public void Mmf_Dispose_ShouldCloseHandles()
        {
            // 这个测试确保 Dispose 后文件不再被占用
            string pageFile;
            var options = new Buffer2DOptions { Width = 10, PageSize = 4 };
            using (var container = new PagedBuffer2D<int>(options, new MmfPageProvider<int>(_testDir!), persistPath: _testDir))
            {
                container.SetElement(0, 0, 1);
                container.Commit();
                pageFile = Path.Combine(_testDir!, "page_0.dat");
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
