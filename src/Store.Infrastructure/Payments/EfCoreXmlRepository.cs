using System.Xml.Linq;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.EntityFrameworkCore;
using Store.Domain;

namespace Store.Infrastructure.Payments;

public sealed class EfCoreXmlRepository(IDbContextFactory<StoreDbContext> contextFactory) : IXmlRepository
{
    public IReadOnlyCollection<XElement> GetAllElements()
    {
        using var database = contextFactory.CreateDbContext();
        return database.DataProtectionKeys
            .AsNoTracking()
            .OrderBy(key => key.FriendlyName)
            .Select(key => key.Xml)
            .AsEnumerable()
            .Select(XElement.Parse)
            .ToArray();
    }

    public void StoreElement(XElement element, string friendlyName)
    {
        using var database = contextFactory.CreateDbContext();
        database.DataProtectionKeys.Add(new DataProtectionKey(friendlyName, element.ToString(SaveOptions.DisableFormatting)));
        database.SaveChanges();
    }
}
