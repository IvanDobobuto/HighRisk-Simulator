using System;
using System.Collections;
using System.Collections.Generic;

namespace HighRiskSimulator.Core.DataStructures;

/// <summary>
/// Pila manual enlazada simple.
/// 
/// Se usa para el historial reciente de sucesos porque el último evento emitido es,
/// precisamente, el primero que la interfaz y los reportes suelen necesitar inspeccionar.
/// </summary>
public sealed class LinkedStack<T> : IEnumerable<T>
{
    private Node? _top;

    public int Count { get; private set; }

    public void Push(T value)
    {
        _top = new Node(value, _top);
        Count++;
    }

    public T Peek()
    {
        if (_top is null)
        {
            throw new InvalidOperationException("La pila está vacía.");
        }

        return _top.Value;
    }

    public bool TryPeek(out T? value)
    {
        if (_top is null)
        {
            value = default;
            return false;
        }

        value = _top.Value;
        return true;
    }

    public T Pop()
    {
        if (_top is null)
        {
            throw new InvalidOperationException("La pila está vacía.");
        }

        var value = _top.Value;
        _top = _top.Next;
        Count--;
        return value;
    }

    public void Clear()
    {
        _top = null;
        Count = 0;
    }

    public IEnumerator<T> GetEnumerator()
    {
        var current = _top;
        while (current is not null)
        {
            yield return current.Value;
            current = current.Next;
        }
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    private sealed class Node
    {
        public Node(T value, Node? next)
        {
            Value = value;
            Next = next;
        }

        public T Value { get; }

        public Node? Next { get; }
    }
}
