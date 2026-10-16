using Microsoft.EntityFrameworkCore;

namespace Store.Infrastructure;

public sealed class StoreDbContext(DbContextOptions<StoreDbContext> options) : DbContext(options);
