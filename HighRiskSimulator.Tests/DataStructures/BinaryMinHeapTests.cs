using HighRiskSimulator.Core.DataStructures;
using Xunit;

namespace HighRiskSimulator.Tests.DataStructures;

/// <summary>
/// Pruebas del heap mínimo manual.
/// </summary>
public sealed class BinaryMinHeapTests
{
    [Fact]
    public void Dequeue_ReturnsItemsInAscendingOrder()
    {
        var heap = new BinaryMinHeap<int>();
        heap.Enqueue(4);
        heap.Enqueue(1);
        heap.Enqueue(7);
        heap.Enqueue(3);

        Assert.Equal(1, heap.Dequeue());
        Assert.Equal(3, heap.Dequeue());
        Assert.Equal(4, heap.Dequeue());
        Assert.Equal(7, heap.Dequeue());
    }

    [Fact]
    public void TryDequeue_WhenEmpty_ReturnsFalse()
    {
        var heap = new BinaryMinHeap<int>();

        var success = heap.TryDequeue(out var value);

        Assert.False(success);
        Assert.Equal(default, value);
    }
}
