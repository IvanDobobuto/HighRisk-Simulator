using System;
using System.Collections;
using System.Collections.Generic;

namespace HighRiskSimulator.Core.DataStructures;

/// <summary>
/// Nodo de una lista doblemente enlazada circular.
/// 
/// Se guarda una referencia al propietario para proteger la estructura
/// frente al uso accidental de nodos pertenecientes a otra lista.
/// </summary>
public sealed class CircularLinkedListNode<T>
{
    internal CircularLinkedListNode(T value)
    {
        Value = value;
    }

    public T Value { get; internal set; }

    public CircularLinkedListNode<T>? Next { get; internal set; }

    public CircularLinkedListNode<T>? Previous { get; internal set; }

    internal CircularLinkedList<T>? Owner { get; set; }
}

/// <summary>
/// Implementación manual de lista circular doblemente enlazada.
/// 
/// Esta estructura encaja bien para cabinas porque el sistema operativo real
/// es cíclico: una cabina repite indefinidamente su ruta, y la siguiente/previa
/// cabina relevante se consulta constantemente.
/// </summary>
public sealed class CircularLinkedList<T> : IEnumerable<T>
{
    private CircularLinkedListNode<T>? _head;

    public int Count { get; private set; }

    public CircularLinkedListNode<T>? First => _head;

    public CircularLinkedListNode<T>? Last => _head?.Previous;

    public CircularLinkedListNode<T> AddFirst(T value)
    {
        var node = AddLast(value);
        _head = node;
        return node;
    }

    public CircularLinkedListNode<T> AddLast(T value)
    {
        var node = new CircularLinkedListNode<T>(value);

        if (_head is null)
        {
            // Caso base: el único nodo se enlaza consigo mismo.
            node.Next = node;
            node.Previous = node;
            node.Owner = this;
            _head = node;
            Count = 1;
            return node;
        }

        var tail = _head.Previous!;
        InsertBetween(node, tail, _head);
        return node;
    }

    public CircularLinkedListNode<T> AddAfter(CircularLinkedListNode<T> existingNode, T value)
    {
        ValidateNode(existingNode);

        var newNode = new CircularLinkedListNode<T>(value);
        InsertBetween(newNode, existingNode, existingNode.Next!);
        return newNode;
    }

    public CircularLinkedListNode<T> AddBefore(CircularLinkedListNode<T> existingNode, T value)
    {
        ValidateNode(existingNode);

        var newNode = new CircularLinkedListNode<T>(value);
        InsertBetween(newNode, existingNode.Previous!, existingNode);

        if (ReferenceEquals(existingNode, _head))
        {
            _head = newNode;
        }

        return newNode;
    }

    public CircularLinkedListNode<T>? Find(Predicate<T> predicate)
    {
        if (predicate is null)
        {
            throw new ArgumentNullException(nameof(predicate));
        }

        if (_head is null)
        {
            return null;
        }

        var current = _head;
        do
        {
            if (predicate(current.Value))
            {
                return current;
            }

            current = current.Next!;
        }
        while (!ReferenceEquals(current, _head));

        return null;
    }

    public void Remove(CircularLinkedListNode<T> node)
    {
        ValidateNode(node);

        if (Count == 1)
        {
            Clear();
            return;
        }

        node.Previous!.Next = node.Next;
        node.Next!.Previous = node.Previous;

        if (ReferenceEquals(node, _head))
        {
            _head = node.Next;
        }

        DetachNode(node);
        Count--;
    }

    public void RotateForward(int steps = 1)
    {
        if (_head is null || Count <= 1)
        {
            return;
        }

        for (var index = 0; index < Math.Max(0, steps); index++)
        {
            _head = _head.Next;
        }
    }

    public void RotateBackward(int steps = 1)
    {
        if (_head is null || Count <= 1)
        {
            return;
        }

        for (var index = 0; index < Math.Max(0, steps); index++)
        {
            _head = _head.Previous;
        }
    }

    public IEnumerable<T> EnumerateFrom(CircularLinkedListNode<T>? startNode = null)
    {
        if (_head is null)
        {
            yield break;
        }

        var current = startNode ?? _head;
        ValidateNode(current);

        do
        {
            yield return current.Value;
            current = current.Next!;
        }
        while (!ReferenceEquals(current, startNode ?? _head));
    }

    public void Clear()
    {
        if (_head is null)
        {
            return;
        }

        var current = _head;
        do
        {
            var next = current.Next;
            DetachNode(current);
            current = next!;
        }
        while (current.Owner is not null);

        _head = null;
        Count = 0;
    }

    public IEnumerator<T> GetEnumerator()
    {
        return EnumerateFrom().GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    private void InsertBetween(
        CircularLinkedListNode<T> newNode,
        CircularLinkedListNode<T> previousNode,
        CircularLinkedListNode<T> nextNode)
    {
        newNode.Owner = this;
        newNode.Previous = previousNode;
        newNode.Next = nextNode;

        previousNode.Next = newNode;
        nextNode.Previous = newNode;
        Count++;
    }

    private void ValidateNode(CircularLinkedListNode<T>? node)
    {
        if (node is null)
        {
            throw new ArgumentNullException(nameof(node));
        }

        if (!ReferenceEquals(node.Owner, this))
        {
            throw new InvalidOperationException("El nodo no pertenece a esta lista circular.");
        }
    }

    private static void DetachNode(CircularLinkedListNode<T> node)
    {
        node.Owner = null;
        node.Next = null;
        node.Previous = null;
    }
}
