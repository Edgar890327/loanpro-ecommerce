using Store.Domain;

namespace Store.Application;

public interface IUserRepository
{
    Task<User?> FindByEmailAsync(string emailNormalized, CancellationToken cancellationToken);

    Task<User?> FindByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<bool> TryAddAsync(User user, CancellationToken cancellationToken);

    Task RecordLoginAsync(Guid id, DateTime atUtc, CancellationToken cancellationToken);
}
