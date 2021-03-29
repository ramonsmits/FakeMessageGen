using System;
using System.Collections.Generic;
using System.Linq;

static class TakeLastExtensions
{
    public static IEnumerable<T> TakeLast<T>(this IList<T> source, int N)
    {
        return source.Skip(Math.Max(0, source.Count - N));
    }
}