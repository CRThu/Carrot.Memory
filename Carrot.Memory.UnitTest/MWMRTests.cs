using System;
using System.Linq;
using System.Threading.Tasks;
using Carrot.Memory;
using CommunityToolkit.HighPerformance;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Carrot.Memory.UnitTest
{
    [TestClass]
    public class MWMRTests
    {
        [TestMethod]
        public void SetElement_ShouldIncreaseRowCount()
        {
            var pagedMemory = new PagedMemory2D<int>(width: 10, pageSize: 8);
            Assert.AreEqual(0, pagedMemory.RowCount);

            pagedMemory.SetElement(100, 5, 42);
            Assert.AreEqual(101, pagedMemory.RowCount);
            Assert.AreEqual(42, pagedMemory[100, 5]);
        }

        [TestMethod]
        public void SetBlock_ShouldWriteCorrectData()
        {
            var pagedMemory = new PagedMemory2D<int>(width: 20, pageSize: 16);
            int[,] data = new int[5, 10];
            for (int i = 0; i < 5; i++)
                for (int j = 0; j < 10; j++)
                    data[i, j] = i * 10 + j;

            pagedMemory.SetBlock(10, 5, data.AsSpan2D());

            Assert.AreEqual(15, pagedMemory.RowCount);
            for (int i = 0; i < 5; i++)
            {
                for (int j = 0; j < 10; j++)
                {
                    Assert.AreEqual(i * 10 + j, pagedMemory[10 + i, 5 + j]);
                }
            }
        }

        [TestMethod]
        public async Task ConcurrentWriters_ShouldNotCorruptData()
        {
            var pagedMemory = new PagedMemory2D<int>(width: 10, pageSize: 64);
            int writeCount = 1000;
            
            // 并发写入不同行
            var tasks = Enumerable.Range(0, writeCount).Select(i => Task.Run(() =>
            {
                pagedMemory.SetElement(i, 0, i);
            })).ToArray();

            await Task.WhenAll(tasks);

            Assert.AreEqual(writeCount, pagedMemory.RowCount);
            for (int i = 0; i < writeCount; i++)
            {
                Assert.AreEqual(i, pagedMemory[i, 0]);
            }
        }

        [TestMethod]
        public async Task ConcurrentWriters_ToSamePosition_ShouldBeSerialized()
        {
            var pagedMemory = new PagedMemory2D<int>(width: 10, pageSize: 64);
            int writeCount = 1000;
            
            // 虽然 RWLock 保证了互斥，但谁最后写会覆盖前面的。
            // 这里我们测试不会崩溃且 RowCount 正确。
            var tasks = Enumerable.Range(0, writeCount).Select(i => Task.Run(() =>
            {
                pagedMemory.SetElement(0, 0, i);
            })).ToArray();

            await Task.WhenAll(tasks);

            Assert.AreEqual(1, pagedMemory.RowCount);
            // 结果应该是某一个写入的值
            int finalValue = pagedMemory[0, 0];
            Assert.IsTrue(finalValue >= 0 && finalValue < writeCount);
        }

        [TestMethod]
        public async Task ConcurrentReadersAndWriters_ShouldWork()
        {
            var pagedMemory = new PagedMemory2D<int>(width: 10, pageSize: 32);
            bool running = true;
            int lastKnownRow = 0;

            // Reader
            var readerTask = Task.Run(() =>
            {
                while (running)
                {
                    int rowCount = pagedMemory.RowCount;
                    if (rowCount > 0)
                    {
                        // 随机读一个
                        int r = Random.Shared.Next(rowCount);
                        int val = pagedMemory[r, 0];
                        Assert.AreEqual(r, val);
                        lastKnownRow = Math.Max(lastKnownRow, rowCount);
                    }
                }
            });

            // Writer
            int targetRows = 2000;
            for (int i = 0; i < targetRows; i++)
            {
                pagedMemory.SetElement(i, 0, i);
            }

            running = false;
            await readerTask;

            Assert.AreEqual(targetRows, pagedMemory.RowCount);
        }

        [TestMethod]
        public void SetRow_ShouldWork()
        {
            var paged = new PagedMemory2D<int>(width: 10, pageSize: 8);
            int[] data = { 1, 2, 3, 4, 5 };
            paged.SetRow(5, 2, data);

            Assert.AreEqual(6, paged.RowCount);
            for (int i = 0; i < data.Length; i++)
            {
                Assert.AreEqual(data[i], paged[5, 2 + i]);
            }
        }

        [TestMethod]
        public void SetColumn_ShouldWork()
        {
            var paged = new PagedMemory2D<int>(width: 10, pageSize: 8);
            int[] data = { 10, 20, 30, 40 };
            paged.SetColumn(10, 5, data);

            Assert.AreEqual(14, paged.RowCount);
            for (int i = 0; i < data.Length; i++)
            {
                Assert.AreEqual(data[i], paged[10 + i, 5]);
            }
        }

        [TestMethod]
        public void SetElement_ShouldWork()
        {
            var paged = new PagedMemory2D<int>(width: 5, pageSize: 4);
            paged.SetElement(10, 2, 999);
            Assert.AreEqual(11, paged.RowCount);
            Assert.AreEqual(999, paged[10, 2]);
        }

        [TestMethod]
        public void IndexerRef_ShouldAllowDirectModification()
        {
            var paged = new PagedMemory2D<int>(width: 5, pageSize: 4);
            paged.SetElement(0, 0, 10);
            
            // 通过 ref 直接修改（用户需自负锁安全责任）
            paged[0, 0] = 20; 
            Assert.AreEqual(20, paged[0, 0]);
        }
    }
}
