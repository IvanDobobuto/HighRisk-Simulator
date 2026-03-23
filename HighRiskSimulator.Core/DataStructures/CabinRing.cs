using System.Collections.Generic;
using System.Linq;
using HighRiskSimulator.Core.Domain.Models;

namespace HighRiskSimulator.Core.DataStructures;

/// <summary>
/// Fachada de dominio sobre la lista circular de cabinas.
/// 
/// Sirve para modelar el orden cíclico de despacho/atención dentro de un tramo.
/// Se usa una lista circular real para evitar wrap-around manual y para dejar
/// la semántica del problema explícita en el código.
/// </summary>
public sealed class CabinRing
{
    private readonly CircularLinkedList<Cabin> _ring = new();
    private readonly Dictionary<int, CircularLinkedListNode<Cabin>> _nodesByCabinId = new();

    public int Count => _ring.Count;

    public Cabin? Current => _ring.First?.Value;

    public void Register(Cabin cabin)
    {
        var node = _ring.AddLast(cabin);
        _nodesByCabinId[cabin.Id] = node;
    }

    public void Clear()
    {
        _ring.Clear();
        _nodesByCabinId.Clear();
    }

    public void Rebuild(IEnumerable<Cabin> cabinsInOrder)
    {
        Clear();

        foreach (var cabin in cabinsInOrder)
        {
            Register(cabin);
        }
    }

    public IEnumerable<Cabin> EnumerateDispatchOrder()
    {
        return _ring.EnumerateFrom().ToList();
    }

    public Cabin? GetNextCabin(int cabinId)
    {
        return _nodesByCabinId.TryGetValue(cabinId, out var node)
            ? node.Next?.Value
            : null;
    }

    public Cabin? GetPreviousCabin(int cabinId)
    {
        return _nodesByCabinId.TryGetValue(cabinId, out var node)
            ? node.Previous?.Value
            : null;
    }

    public void Rotate()
    {
        _ring.RotateForward();
    }
}
