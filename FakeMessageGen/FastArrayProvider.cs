using System;
using System.Buffers;
using System.Collections.Concurrent;

public class FastArrayProvider<T>(int size, Func<T> factory) where T : class
{
    readonly ConcurrentBag<T[]> _pool = new();

    public (T[] Array, int Length) Rent()
    {
        var array = _pool.TryTake(out var pooledArray) ? pooledArray : new T[size];

        for (int i = 0; i < size; i++)
            array[i] = factory();

        return (array, size);
    }

    public void Return(T[] array, int length)
    {
        if (array == null) return;
        _pool.Add(array);
    }
}