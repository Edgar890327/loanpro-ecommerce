namespace Store.Application;

public interface IPasswordHasher
{
    string DummyHash { get; }

    string Hash(string password);

    bool Verify(string passwordHash, string password);
}
