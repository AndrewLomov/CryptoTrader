namespace CryptoTrader.Infrastructure.Persistence;

internal static class StoredEnum
{
    public static T FromInt32<T>(int value, string column)
        where T : struct, Enum
    {
        if (!Enum.IsDefined(typeof(T), value))
        {
            throw new InvalidOperationException($"Column '{column}' contains undefined {typeof(T).Name} value {value}.");
        }

        return (T)Enum.ToObject(typeof(T), value);
    }
}
