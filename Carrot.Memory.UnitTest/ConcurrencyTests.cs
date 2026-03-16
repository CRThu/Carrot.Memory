namespace Carrot.Memory.UnitTest;

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Carrot.Memory;

/// <summary>
/// 并发性能测试：覆盖多写多读、原子扩容、无锁 Flush 安全边界。
/// </summary>
[TestClass]
public class ConcurrencyTests
{
    private const int PageSize = 64;
    private const int DefaultWidth = 10;

    [TestMethod]
    public async Task ConcurrentWriters_DifferentRows_AllDataCorrect()
    {
        var paged = new PagedMemory2D<int>(DefaultWidth, PageSize);
        int writeCount = 1000;

        var tasks = Enumerable.Range(0, writeCount).Select(i => Task.Run(() =>
        {
            paged.SetElement(i, 0, i);
        })).ToArray();

        await Task.WhenAll(tasks);

        Assert.AreEqual(writeCount, paged.RowCount);
        for (int i = 0; i < writeCount; i++)
        {
            Assert.AreEqual(i, paged[i, 0]);
        }
    }

    [TestMethod]
    public async Task ConcurrentWriters_SamePosition_NoCorruption()
    {
        var paged = new PagedMemory2D<int>(DefaultWidth, PageSize);
        int writeCount = 500;

        var tasks = Enumerable.Range(0, writeCount).Select(i => Task.Run(() =>
        {
            paged.SetElement(0, 0, i);
        })).ToArray();

        await Task.WhenAll(tasks);

        Assert.AreEqual(1, paged.RowCount);
        int finalValue = paged[0, 0];
        Assert.IsTrue(finalValue >= 0 && finalValue < writeCount);
    }

    [TestMethod]
    public async Task ConcurrentReadersAndWriters_DuringExpansion_Stable()
    {
        var paged = new PagedMemory2D<int>(DefaultWidth, PageSize);
        bool running = true;
        int targetRows = 2000;

        var readerTask = Task.Run(() =>
        {
            while (running)
            {
                int rowCount = paged.RowCount;
                if (rowCount > 0)
                {
                    int r = Random.Shared.Next(rowCount);
                    int val = paged[r, 0];
                    // 数据一致性验证依赖于写入逻辑，此处仅验证不崩溃
                }
            }
        });

        for (int i = 0; i < targetRows; i++)
        {
            paged.SetElement(i, 0, i);
        }

        running = false;
        await readerTask;

        Assert.AreEqual(targetRows, paged.RowCount);
    }

    [TestMethod]
    public async Task FlushAll_DuringConcurrentExpansion_LockFreeAndSafe()
    {
        var paged = new PagedMemory2D<int>(DefaultWidth, PageSize);
        bool running = true;

        var flushTask = Task.Run(() =>
        {
            while (running)
            {
                paged.FlushAll();
            }
        });

        // 密集扩容
        for (int i = 0; i < 50; i++)
        {
            paged.SetElement(i * PageSize, 0, i);
            await Task.Delay(1);
        }

        running = false;
        await flushTask;
    }
}
