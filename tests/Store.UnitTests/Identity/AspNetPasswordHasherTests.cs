using Store.Infrastructure.Identity;

namespace Store.UnitTests.Identity;

public sealed class AspNetPasswordHasherTests
{
    private readonly AspNetPasswordHasher _hasher = new();

    [Fact]
    public void Hash_ProducesAVerifiableNonReversibleHash()
    {
        var hash = _hasher.Hash("correct horse");

        Assert.DoesNotContain("correct horse", hash);
        Assert.True(_hasher.Verify(hash, "correct horse"));
        Assert.False(_hasher.Verify(hash, "wrong horse"));
    }

    [Fact]
    public void Hash_UsesADifferentSaltEachTime()
    {
        Assert.NotEqual(_hasher.Hash("same"), _hasher.Hash("same"));
    }

    [Fact]
    public void DummyHash_IsAValidHashThatRejectsArbitraryPasswords()
    {
        Assert.False(_hasher.Verify(_hasher.DummyHash, "anything"));
    }
}
