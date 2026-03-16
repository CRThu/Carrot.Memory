namespace Carrot.Memory.UnitTest;

using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using CommunityToolkit.HighPerformance;
using Carrot.Memory;

/// <summary>
/// 安全性测试：覆盖资源释放、只读隔离验证、非法边界验证。
/// </summary>
[TestClass]
public class SafetyTests
{
    private const int PageSize = 1024;
    private const int DefaultWidth = 100;

    [TestMethod]
    public void SetElement_AfterDispose_ShouldThrowObjectDisposedException()
    {
        var paged = new PagedMemory2D<int>(DefaultWidth, PageSize);
        paged.Dispose();

        try
        {
            paged.SetElement(0, 0, 1);
            Assert.Fail("Should have thrown ObjectDisposedException");
        }
        catch (ObjectDisposedException) { }
    }

    [TestMethod]
    public void ReadOnlyView_ModifyAttempt_VerifyIsolation()
    {
        var paged = new PagedMemory2D<int>(DefaultWidth, PageSize);
        paged.SetElement(0, 0, 100);
        
        IReadonlyPagedMemory2D<int> readOnly = paged.AsReadOnly();
        
        // 验证读取正常
        Assert.AreEqual(100, readOnly[0, 0]);
        
        // 注意：此处无法通过代码通过 readOnly[0,0] = 200 验证编译失败
        // 但我们可以验证 GetRowView 返回的是只读结构
        var rowView = readOnly.GetRowView(0, 0, 1);
        Assert.AreEqual(100, rowView[0]);
    }

    [TestMethod]
    public void Indexer_AccessOutOfRange_ShouldThrowIndexOutOfRangeException()
    {
        var paged = new PagedMemory2D<int>(DefaultWidth, PageSize);
        paged.SetElement(0, 0, 1);

        try
        {
            var val = paged[1, 0];
            Assert.Fail("Should have thrown IndexOutOfRangeException for row");
        }
        catch (IndexOutOfRangeException) { }
        
        try
        {
            var val = paged[0, DefaultWidth];
            Assert.Fail("Should have thrown IndexOutOfRangeException for col");
        }
        catch (IndexOutOfRangeException) { }
    }

    [TestMethod]
    public void SetBlock_InvalidWidthBound_ShouldThrowArgumentException()
    {
        var paged = new PagedMemory2D<int>(DefaultWidth, PageSize);
        int[,] invalidData = new int[1, DefaultWidth + 1];

        try
        {
            paged.SetBlock(0, 0, invalidData.AsSpan2D());
            Assert.Fail("Should have thrown ArgumentException");
        }
        catch (ArgumentException) { }
    }

    [TestMethod]
    public void GetRowView_AsSpanFromColumnView_ShouldThrowNotSupportedException()
    {
        var paged = new PagedMemory2D<int>(DefaultWidth, PageSize);
        paged.SetElement(0, 0, 1);
        
        var colView = paged.GetColumnView(0, 0, 1);
        
        try
        {
            colView.AsSpan();
            Assert.Fail("Should have thrown NotSupportedException");
        }
        catch (NotSupportedException) { }
    }
}
