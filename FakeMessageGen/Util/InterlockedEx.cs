public static class InterlockedEx
{
    public static T CompareExchange<T>(ref T location, T value, T comparand)
    {
        return CompareExchangeEnumImpl<T>.Impl(ref location, value, comparand);
    }
}