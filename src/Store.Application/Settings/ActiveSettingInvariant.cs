namespace Store.Application;

public static class ActiveSettingInvariant
{
    public static void EnsureExactlyOne(int activeCount)
    {
        if (activeCount != 1)
        {
            throw new InvalidOperationException("Exactly one setting must be active.");
        }
    }
}
