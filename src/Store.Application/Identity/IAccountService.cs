namespace Store.Application;

public interface IAccountService
{
    Task<AccountResult> RegisterAsync(RegisterInput input, CancellationToken cancellationToken);

    Task<AccountResult> LoginAsync(LoginInput input, CancellationToken cancellationToken);

    Task<UserProfile?> GetProfileAsync(Guid userId, CancellationToken cancellationToken);
}
