using System;
using System.Linq;
using HighRiskSimulator.Core.DataStructures;
using Xunit;

namespace HighRiskSimulator.Tests.DataStructures;

/// <summary>
/// Pruebas de la lista circular manual.
/// </summary>
public sealed class CircularLinkedListTests
{
    [Fact]
    public void AddLast_CreatesStableCircularTopology()
    {
        var list = new CircularLinkedList<int>();

        var first = list.AddLast(10);
        var second = list.AddLast(20);
        var third = list.AddLast(30);

        Assert.Equal(3, list.Count);
        Assert.Same(first, list.First);
        Assert.Same(third, list.Last);
        Assert.Same(second, first.Next);
        Assert.Same(third, second.Next);
        Assert.Same(first, third.Next);
        Assert.Same(third, first.Previous);
    }

    [Fact]
    public void AddBefore_WhenAppliedToHead_UpdatesFirstNode()
    {
        var list = new CircularLinkedList<int>();
        var first = list.AddLast(1);
        list.AddLast(2);

        var inserted = list.AddBefore(first, 0);

        Assert.Same(inserted, list.First);
        Assert.Equal(new[] { 0, 1, 2 }, list.ToArray());
    }

    [Fact]
    public void RotateForward_MovesTheHeadWithoutBreakingOrder()
    {
        var list = new CircularLinkedList<string>();
        list.AddLast("A");
        list.AddLast("B");
        list.AddLast("C");

        list.RotateForward();

        Assert.Equal("B", list.First!.Value);
        Assert.Equal(new[] { "B", "C", "A" }, list.ToArray());
    }

    [Fact]
    public void Remove_UpdatesLinksAndCount()
    {
        var list = new CircularLinkedList<int>();
        var first = list.AddLast(1);
        var second = list.AddLast(2);
        list.AddLast(3);

        list.Remove(second);

        Assert.Equal(2, list.Count);
        Assert.Equal(new[] { 1, 3 }, list.ToArray());
        Assert.Same(first, list.First);
        Assert.Same(first, list.Last!.Next);
    }

    [Fact]
    public void Clear_DetachesNodesAndResetsStructure()
    {
        var list = new CircularLinkedList<int>();
        var first = list.AddLast(1);
        var second = list.AddLast(2);

        list.Clear();

        Assert.Equal(0, list.Count);
        Assert.Null(list.First);
        Assert.Null(first.Next);
        Assert.Null(first.Previous);
        Assert.Null(second.Next);
        Assert.Null(second.Previous);
    }

    [Fact]
    public void AddAfter_NodeFromAnotherList_Throws()
    {
        var left = new CircularLinkedList<int>();
        var right = new CircularLinkedList<int>();
        var foreignNode = right.AddLast(10);
        left.AddLast(1);

        Assert.Throws<InvalidOperationException>(() => left.AddAfter(foreignNode, 20));
    }
}
