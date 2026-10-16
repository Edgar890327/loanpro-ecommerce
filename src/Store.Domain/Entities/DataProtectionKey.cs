namespace Store.Domain;

public sealed class DataProtectionKey
{
    public Guid Id { get; private set; } = Guid.NewGuid();

    public string FriendlyName { get; private set; } = string.Empty;

    public string Xml { get; private set; } = string.Empty;

    private DataProtectionKey()
    {
    }

    public DataProtectionKey(string friendlyName, string xml)
    {
        FriendlyName = friendlyName;
        Xml = xml;
    }
}
