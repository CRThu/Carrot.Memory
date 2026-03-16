namespace Carrot.Memory.UnitTest;

using System;
using System.Linq;
using CommunityToolkit.HighPerformance;

[TestClass]
public class PagedMemory2DTests
{
    // 每页 1024 行
    private const int PageSize = 1024;
    // 每行 100 列
    private const int DefaultWidth = 100;

    [TestMethod]
    public void ShouldCorrectlyInsertAndAccessLargeDataAcrossPages()
    {
        var paged = new PagedMemory2D<int>(DefaultWidth, PageSize);
        
        // 准备 3000 行数据，这会占用 3 个完整的物理页 (1024*2 + 952)
        int totalRows = 3000;
        int[,] sourceData = new int[totalRows, DefaultWidth];
        
        // 填充特征数据：值为 行号 + 列号
        for (int r = 0; r < totalRows; r++)
        {
            for (int c = 0; c < DefaultWidth; c++)
            {
                sourceData[r, c] = r + c;
            }
        }

        // 执行批量插入
        paged.SetBlock(paged.RowCount, 0, sourceData.AsSpan2D());

        // 验证基本属性
        Assert.AreEqual(totalRows, paged.RowCount);
        Assert.AreEqual(DefaultWidth, paged.Width);

        // 验证第一页边界
        Assert.AreEqual(0, paged[0, 0]);
        Assert.AreEqual(1023, paged[1023, 0]);

        // 验证跨页点 (1024 是第二页的第一行)
        Assert.AreEqual(1024, paged[1024, 0]);
        Assert.AreEqual(1024 + 50, paged[1024, 50]);

        // 验证最后一页
        Assert.AreEqual(2999 + 99, paged[2999, 99]);
    }

    [TestMethod]
    public void ShouldRetrieveVerticalSliceCorrectlyAcrossPageBoundaries()
    {
        var paged = new PagedMemory2D<double>(DefaultWidth, PageSize);
        int totalRows = 2500;
        
        // 模拟写入大量数据
        for (int i = 0; i < totalRows; i++)
        {
            double[] row = new double[DefaultWidth];
            row[5] = i * 1.5; // 在第 5 列存储特征值
            paged.SetRow(paged.RowCount, 0, row);
        }

        // 目标：获取一个跨越物理页面边界的垂直切片
        // 从第 1000 行开始（第一页末尾），取 100 个元素
        // 第一页结束于 1023，所以这个切片包含：
        // 第一页：1000 ~ 1023 (24个元素)
        // 第二页：1024 ~ 1099 (76个元素)
        int startRow = 1000;
        int sliceLen = 100;
        int targetCol = 5;

        var series = paged.GetSeries(startRow, targetCol, sliceLen);

        Assert.AreEqual(sliceLen, series.Length);
        
        // 验证切片内的数据
        for (int i = 0; i < sliceLen; i++)
        {
            double expected = (startRow + i) * 1.5;
            Assert.AreEqual(expected, series[i], $"索引 {i} 处的值不匹配");
        }
    }

    [TestMethod]
    public void ShouldRetrieveHorizontalSliceCorrectlyFromDeepPages()
    {
        var paged = new PagedMemory2D<long>(DefaultWidth, PageSize);
        
        // 直接跳到第 2000 行写入（会自动触发多页分配）
        paged.SetBlock(0, 0, new long[2000, DefaultWidth].AsSpan2D());
        
        long[] targetRow = Enumerable.Range(0, DefaultWidth).Select(x => (long)x).ToArray();
        paged.SetRow(paged.RowCount, 0, targetRow); // 这是第 2000 行

        // 在第 2000 行获取列索引 10 到 20 的切片
        var slice = paged.GetSlice(2000, 10, 10);

        Assert.AreEqual(10, slice.Length);
        Assert.AreEqual(10L, slice[0]);
        Assert.AreEqual(19L, slice[9]);
        
        // 验证 AsSpan 性能路径
        var span = slice.AsSpan();
        Assert.AreEqual(15L, span[5]);
    }

    [TestMethod]
    public void ShouldMaintainStabilityUnderHighFrequencyRowInsertion()
    {
        // 测试在高频写入下，分页增长是否稳定
        var paged = new PagedMemory2D<int>(10, PageSize);
        int iterations = 100_000; // 写入 10 万行

        Span<int> row = new int[10];
        for (int i = 0; i < iterations; i++)
        {
            row.Fill(i);
            paged.SetRow(paged.RowCount, 0, row);
        }

        Assert.AreEqual(iterations, paged.RowCount);
        // 随机抽查深层数据
        Assert.AreEqual(88888, paged[88888, 5]);
        Assert.AreEqual(99999, paged[99999, 9]);
    }

    [TestMethod]
    public void ShouldThrowExceptionWhenAccessingOuterBoundariesInDeepPages()
    {
        var paged = new PagedMemory2D<byte>(DefaultWidth, PageSize);
        paged.SetBlock(0, 0, new byte[1500, DefaultWidth].AsSpan2D());

        Assert.Throws<IndexOutOfRangeException>(() =>
        {
            // 试图访问尚未添加的第 1501 行（虽然物理页可能已分配，但 RowCount 限制了访问）
            var fail = paged[1500, 0];
        });
    }
}
