namespace Carrot.Memory.UnitTest;

using System;
using System.Threading.Tasks;
using CommunityToolkit.HighPerformance;

[TestClass]
public class ReadOnlyProtectionTests
{
    private const int PageSize = 1024;
    private const int DefaultWidth = 100;

    [TestMethod]
    public void ReadOnlyInterface_ShouldAllowReading()
    {
        var paged = new PagedMemory2D<int>(DefaultWidth, PageSize);
        paged.SetElement(0, 0, 100);

        IReadonlyPagedMemory2D<int> readOnly = paged.AsReadOnly();

        // 验证读取
        Assert.AreEqual(100, readOnly[0, 0]);
        
        // 验证切片读取
        var slice = readOnly.GetRowView(0, 0, 1);
        Assert.AreEqual(100, slice[0]);
        
        // 验证系列读取
        var series = readOnly.GetColumnView(0, 0, 1);
        Assert.AreEqual(100, series[0]);
    }

    [TestMethod]
    public void WritableView_ShouldAllowWriting()
    {
        var paged = new PagedMemory2D<int>(DefaultWidth, PageSize);
        paged.SetElement(0, 0, 100);

        PagedView<int> slice = paged.GetRowView(0, 0, 1);
        slice[0] = 200;
        Assert.AreEqual(200, paged[0, 0]);

        PagedView<int> series = paged.GetColumnView(0, 0, 1);
        series[0] = 300;
        Assert.AreEqual(300, paged[0, 0]);
    }

    [TestMethod]
    public void FlushAll_ShouldBeStableDuringExpansion()
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

        // 大量跨页写入触发扩容
        for (int i = 0; i < 100; i++)
        {
            paged.SetElement(i * PageSize, 0, i);
        }

        running = false;
        flushTask.Wait();
    }
}
