using Store.Application;

namespace Store.UnitTests.Identity;

public sealed class FakePasswordHasher : IPasswordHasher
{
    public string DummyHash => "hash:dummy";

    public int VerifyCalls { get; private set; }

    public string Hash(string password) => $"hash:{password}";

    public bool Verify(string passwordHash, string password)
    {
        VerifyCalls++;
        return passwordHash == Hash(password);
    }
}
