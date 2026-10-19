using Microsoft.EntityFrameworkCore;
using Npgsql;
using Store.Application;
using Store.Domain;

namespace Store.Infrastructure.Identity;

public sealed class UserRepository(StoreDbContext database) : IUserRepository
{
    public Task<User?> FindByEmailAsync(string emailNormalized, CancellationToken cancellationToken) =>
        database.Users.AsNoTracking()
            .FirstOrDefaultAsync(user => user.EmailNormalized == emailNormalized, cancellationToken);

    public Task<User?> FindByIdAsync(Guid id, CancellationToken cancellationToken) =>
        database.Users.AsNoTracking().FirstOrDefaultAsync(user => user.Id == id, cancellationToken);

    public async Task<bool> TryAddAsync(User user, CancellationToken cancellationToken)
    {
        database.Users.Add(user);
        try
        {
            await database.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            database.Entry(user).State = EntityState.Detached;
            return false;
        }
    }

    public Task RecordLoginAsync(Guid id, DateTime atUtc, CancellationToken cancellationToken) =>
        database.Users
            .Where(user => user.Id == id)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(user => user.LastLoginAtUtc, atUtc),
                cancellationToken);
}
