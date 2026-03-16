namespace Carrot.Memory.UnitTest;

using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using CommunityToolkit.HighPerformance;

/// <summary>
/// 基础功能测试：覆盖索引器、RowCount、基本插入与视图修改。
/// </summary>
[TestClass]
public class BasicTests
{
    private const int PageSize = 1024;
    private const int DefaultWidth = 100;

    [TestMethod]
    public void RowCount_Initial_ShouldBeZero()
    {
        var paged = new PagedMemory2D<int>(DefaultWidth, PageSize);
        Assert.AreEqual(0, paged.RowCount);
    }

    [TestMethod]
    public void SetElement_ValidPosition_ShouldUpdateDataAndRowCount()
    {
        var paged = new PagedMemory2D<int>(DefaultWidth, PageSize);
        paged.SetElement(10, 5, 42);
        
        Assert.AreEqual(11, paged.RowCount);
        Assert.AreEqual(42, paged[10, 5]);
    }

    [TestMethod]
    public void SetRow_ValidPosition_ShouldUpdateData()
    {
        var paged = new PagedMemory2D<int>(DefaultWidth, PageSize);
        int[] data = { 1, 2, 3, 4, 5 };
        paged.SetRow(0, 10, data);

        for (int i = 0; i < data.Length; i++)
        {
            Assert.AreEqual(data[i], paged[0, 10 + i]);
        }
    }

    [TestMethod]
    public void SetColumn_ValidPosition_ShouldUpdateData()
    {
        var paged = new PagedMemory2D<int>(DefaultWidth, PageSize);
        int[] data = { 10, 20, 30, 40 };
        paged.SetColumn(0, 5, data);

        for (int i = 0; i < data.Length; i++)
        {
            Assert.AreEqual(data[i], paged[i, 5]);
        }
    }

    [TestMethod]
    public void SetBlock_AcrossPages_DataCorrect()
    {
        var paged = new PagedMemory2D<int>(DefaultWidth, PageSize);
        int totalRows = 3000;
        int[,] sourceData = new int[totalRows, DefaultWidth];
        
        for (int r = 0; r < totalRows; r++)
            for (int c = 0; c < DefaultWidth; c++)
                sourceData[r, c] = r + c;

        paged.SetBlock(0, 0, sourceData.AsSpan2D());

        Assert.AreEqual(totalRows, paged.RowCount);
        Assert.AreEqual(0 + 0, paged[0, 0]);
        Assert.AreEqual(1024 + 50, paged[1024, 50]);
        Assert.AreEqual(2999 + 99, paged[2999, 99]);
    }

    [TestMethod]
    public void GetRowView_ModifyElement_ReflectedInParent()
    {
        var paged = new PagedMemory2D<int>(DefaultWidth, PageSize);
        paged.SetElement(0, 0, 100);

        PagedView<int> view = paged.GetRowView(0, 0, 1);
        view[0] = 200;
        
        Assert.AreEqual(200, paged[0, 0]);
    }

    [TestMethod]
    public void GetColumnView_AcrossPages_DataCorrect()
    {
        var paged = new PagedMemory2D<double>(DefaultWidth, PageSize);
        int startRow = PageSize - 10;
        int len = 20;
        
        for (int i = 0; i < PageSize * 2; i++)
            paged.SetElement(i, 5, i * 1.0);

        var view = paged.GetColumnView(startRow, 5, len);

        Assert.AreEqual(len, view.Length);
        for (int i = 0; i < len; i++)
        {
            Assert.AreEqual((startRow + i) * 1.0, view[i]);
        }
    }
}
