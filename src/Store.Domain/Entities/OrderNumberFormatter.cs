namespace Store.Domain;

public static class OrderNumberFormatter
{
    public static string Format(DateTime createdAtUtc, long sequenceValue)
    {
        if (sequenceValue < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(sequenceValue));
        }

        return $"ORD-{createdAtUtc.Year:D4}-{sequenceValue:D6}";
    }
}
