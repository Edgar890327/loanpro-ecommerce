using Store.Application;
using Store.Domain;

namespace Store.UnitTests.Identity;

public sealed class InMemoryUserRepository : IUserRepository
{
    public List<User> Users { get; } = [];

    public Task<User?> FindByEmailAsync(string emailNormalized, CancellationToken cancellationToken) =>
        Task.FromResult(Users.FirstOrDefault(user => user.EmailNormalized == emailNormalized));

    public Task<User?> FindByIdAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Users.FirstOrDefault(user => user.Id == id));

    public Task<bool> TryAddAsync(User user, CancellationToken cancellationToken)
    {
        if (Users.Any(existing => existing.EmailNormalized == user.EmailNormalized))
        {
            return Task.FromResult(false);
        }

        Users.Add(user);
        return Task.FromResult(true);
    }

    public Task RecordLoginAsync(Guid id, DateTime atUtc, CancellationToken cancellationToken)
    {
        Users.First(user => user.Id == id).RecordLogin(atUtc);
        return Task.CompletedTask;
    }
}
