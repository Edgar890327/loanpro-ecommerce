using Store.Application;

namespace Store.UnitTests.Identity;

public sealed class RegistrationValidatorTests
{
    [Theory]
    [InlineData(7, false)]
    [InlineData(8, true)]
    [InlineData(128, true)]
    [InlineData(129, false)]
    public void Validate_EnforcesPasswordLengthLimits(int length, bool valid)
    {
        var errors = RegistrationValidator.Validate(
            new RegisterInput("ana@example.com", new string('a', length), "Ana", null));

        Assert.Equal(valid, !errors.ContainsKey("password"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-an-email")]
    [InlineData("Ana <ana@example.com>")]
    [InlineData("a@")]
    public void Validate_RejectsInvalidEmails(string email)
    {
        var errors = RegistrationValidator.Validate(new RegisterInput(email, "password-1", "Ana", null));

        Assert.Contains("email", errors.Keys);
    }

    [Fact]
    public void Validate_RejectsBlankAndTooLongNames()
    {
        Assert.Contains("fullName", RegistrationValidator.Validate(
            new RegisterInput("ana@example.com", "password-1", "   ", null)).Keys);
        Assert.Contains("fullName", RegistrationValidator.Validate(
            new RegisterInput("ana@example.com", "password-1", new string('a', 201), null)).Keys);
    }

    [Fact]
    public void Validate_AcceptsPasswordsWithoutCompositionRules()
    {
        Assert.Empty(RegistrationValidator.Validate(
            new RegisterInput("ana@example.com", "aaaaaaaa", "Ana", null)));
    }

    [Theory]
    [InlineData("  Ana@Example.COM ", "ana@example.com")]
    [InlineData("USER@EXAMPLE.COM", "user@example.com")]
    public void Normalize_TrimsAndLowercases(string input, string expected)
    {
        Assert.Equal(expected, EmailNormalizer.Normalize(input));
    }
}
