namespace Carrot.Memory.UnitTest;

using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Carrot.Memory;
using Carrot.Memory.Abstractions;
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
        var paged = new PagedBuffer2D<int>(new Buffer2DOptions { Width = DefaultWidth, PageSize = PageSize }, new HeapProvider<int>());
        Assert.AreEqual(0, paged.RowCount);
    }

    [TestMethod]
    public void SetElement_ValidPosition_ShouldUpdateDataAndRowCount()
    {
        var paged = new PagedBuffer2D<int>(new Buffer2DOptions { Width = DefaultWidth, PageSize = PageSize }, new HeapProvider<int>());
        paged.SetElement(10, 5, 42);
        
        Assert.AreEqual(11, paged.RowCount);
        Assert.AreEqual(42, paged[10, 5]);
    }

    [TestMethod]
    public void SetRow_ValidPosition_ShouldUpdateData()
    {
        var paged = new PagedBuffer2D<int>(new Buffer2DOptions { Width = DefaultWidth, PageSize = PageSize }, new HeapProvider<int>());
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
        var paged = new PagedBuffer2D<int>(new Buffer2DOptions { Width = DefaultWidth, PageSize = PageSize }, new HeapProvider<int>());
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
        var paged = new PagedBuffer2D<int>(new Buffer2DOptions { Width = DefaultWidth, PageSize = PageSize }, new HeapProvider<int>());
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
        var paged = new PagedBuffer2D<int>(new Buffer2DOptions { Width = DefaultWidth, PageSize = PageSize }, new HeapProvider<int>());
        paged.SetElement(0, 0, 100);

        RowView<int> view = paged.GetRowView(0, 0, 1);
        view[0] = 200;
        
        Assert.AreEqual(200, paged[0, 0]);
    }

    [TestMethod]
    public void GetColumnView_AcrossPages_DataCorrect()
    {
        var paged = new PagedBuffer2D<double>(new Buffer2DOptions { Width = DefaultWidth, PageSize = PageSize }, new HeapProvider<double>());
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

    [TestMethod]
    public void Views_ImplicitConversionAndForeach_ShouldWorkCorrectly()
    {
        var paged = new PagedBuffer2D<int>(new Buffer2DOptions { Width = DefaultWidth, PageSize = PageSize }, new HeapProvider<int>());
        int[] rowData = { 11, 22, 33, 44, 55 };
        paged.SetRow(5, 10, rowData);

        // 1. RowView 隐式转换为 Span / ReadOnlySpan
        RowView<int> rowView = paged.GetRowView(5, 10, 5);
        Span<int> span = rowView;
        Assert.AreEqual(5, span.Length);
        Assert.AreEqual(11, span[0]);
        ReadOnlySpan<int> roSpanFromMut = rowView;
        Assert.AreEqual(5, roSpanFromMut.Length);

        // 2. ReadOnlyRowView 隐式转换为 ReadOnlySpan
        IReadOnlyBuffer2D<int> roBuffer = paged;
        ReadOnlyRowView<int> roRowView = roBuffer.GetRowView(5, 10, 5);
        ReadOnlySpan<int> roSpan = roRowView;
        Assert.AreEqual(5, roSpan.Length);
        Assert.AreEqual(55, roSpan[4]);

        // 3. 行视图 foreach 遍历求和
        int rowSum = 0;
        foreach (var val in roRowView)
        {
            rowSum += val;
        }
        Assert.AreEqual(11 + 22 + 33 + 44 + 55, rowSum);

        // 4. 列视图（跨页）foreach 遍历求和
        int startRow = PageSize - 2;
        int len = 5; // 跨页
        for (int i = 0; i < len; i++)
        {
            paged.SetElement(startRow + i, 8, (i + 1) * 10);
        }

        ReadOnlyColumnView<int> roColView = roBuffer.GetColumnView(startRow, 8, len);
        int colSum = 0;
        foreach (var val in roColView)
        {
            colSum += val;
        }
        Assert.AreEqual(10 + 20 + 30 + 40 + 50, colSum);

        Views.ColumnView<int> colView = paged.GetColumnView(startRow, 8, len);
        int mutColSum = 0;
        foreach (ref int val in colView)
        {
            mutColSum += val;
        }
        Assert.AreEqual(colSum, mutColSum);

        // 5. 空视图 (Length = 0) 的 foreach 路径安全验证
        RowView<int> emptyRowView = paged.GetRowView(5, 10, 0);
        int emptyCount = 0;
        foreach (var _ in emptyRowView) emptyCount++;
        Assert.AreEqual(0, emptyCount);

        ReadOnlyRowView<int> roEmptyRowView = roBuffer.GetRowView(5, 10, 0);
        foreach (var _ in roEmptyRowView) emptyCount++;
        Assert.AreEqual(0, emptyCount);

        Views.ColumnView<int> emptyColView = paged.GetColumnView(startRow, 8, 0);
        foreach (var _ in emptyColView) emptyCount++;
        Assert.AreEqual(0, emptyCount);

        ReadOnlyColumnView<int> roEmptyColView = roBuffer.GetColumnView(startRow, 8, 0);
        foreach (var _ in roEmptyColView) emptyCount++;
        Assert.AreEqual(0, emptyCount);
    }
}
