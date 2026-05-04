using System;
using System.Collections.Generic;

namespace HighRiskSimulator.Core.DataStructures;

/// <summary>
/// Heap binario mínimo implementado manualmente.
/// 
/// Se usa para resolver con eficiencia la próxima acción/contingencia más urgente
/// sin depender de colecciones externas. Esto lo vuelve ideal para programar
/// eventos, transferencias de pasajeros y respuestas de emergencia.
/// </summary>
public sealed class BinaryMinHeap<T>
{
    private readonly List<T> _items = new();
    private readonly IComparer<T> _comparer;

    public BinaryMinHeap()
        : this(Comparer<T>.Default)
    {
    }

    public BinaryMinHeap(IComparer<T> comparer)
    {
        _comparer = comparer ?? throw new ArgumentNullException(nameof(comparer));
    }

    public int Count => _items.Count;

    public void Clear()
    {
        _items.Clear();
    }

    public void Enqueue(T item)
    {
        _items.Add(item);
        HeapifyUp(_items.Count - 1);
    }

    public T Peek()
    {
        if (_items.Count == 0)
        {
            throw new InvalidOperationException("El heap está vacío.");
        }

        return _items[0];
    }

    public bool TryPeek(out T? item)
    {
        if (_items.Count == 0)
        {
            item = default;
            return false;
        }

        item = _items[0];
        return true;
    }

    public T Dequeue()
    {
        if (_items.Count == 0)
        {
            throw new InvalidOperationException("El heap está vacío.");
        }

        var root = _items[0];
        var lastIndex = _items.Count - 1;
        _items[0] = _items[lastIndex];
        _items.RemoveAt(lastIndex);

        if (_items.Count > 0)
        {
            HeapifyDown(0);
        }

        return root;
    }

    public bool TryDequeue(out T? item)
    {
        if (_items.Count == 0)
        {
            item = default;
            return false;
        }

        item = Dequeue();
        return true;
    }

    private void HeapifyUp(int index)
    {
        while (index > 0)
        {
            var parentIndex = (index - 1) / 2;
            if (_comparer.Compare(_items[index], _items[parentIndex]) >= 0)
            {
                break;
            }

            Swap(index, parentIndex);
            index = parentIndex;
        }
    }

    private void HeapifyDown(int index)
    {
        while (true)
        {
            var leftIndex = (index * 2) + 1;
            var rightIndex = leftIndex + 1;
            var smallestIndex = index;

            if (leftIndex < _items.Count && _comparer.Compare(_items[leftIndex], _items[smallestIndex]) < 0)
            {
                smallestIndex = leftIndex;
            }

            if (rightIndex < _items.Count && _comparer.Compare(_items[rightIndex], _items[smallestIndex]) < 0)
            {
                smallestIndex = rightIndex;
            }

            if (smallestIndex == index)
            {
                break;
            }

            Swap(index, smallestIndex);
            index = smallestIndex;
        }
    }

    private void Swap(int leftIndex, int rightIndex)
    {
        (_items[leftIndex], _items[rightIndex]) = (_items[rightIndex], _items[leftIndex]);
    }
}
