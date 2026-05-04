using HighRiskSimulator.Core.DataStructures;
using Xunit;

namespace HighRiskSimulator.Tests.DataStructures;

/// <summary>
/// Pruebas de la pila enlazada manual.
/// </summary>
public sealed class LinkedStackTests
{
    [Fact]
    public void Pop_FollowsLifoOrder()
    {
        var stack = new LinkedStack<string>();
        stack.Push("uno");
        stack.Push("dos");
        stack.Push("tres");

        Assert.Equal("tres", stack.Pop());
        Assert.Equal("dos", stack.Pop());
        Assert.Equal("uno", stack.Pop());
    }

    [Fact]
    public void TryPeek_ReturnsFalseWhenEmpty()
    {
        var stack = new LinkedStack<int>();

        var success = stack.TryPeek(out var value);

        Assert.False(success);
        Assert.Equal(default, value);
    }
}
