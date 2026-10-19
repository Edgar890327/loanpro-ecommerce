using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;
using Store.Application;

namespace Store.Infrastructure.MyOrders;

public sealed class MyOrdersReader(IDbContextFactory<StoreDbContext> contextFactory) : IMyOrdersReader
{
    private const string Filters = """
        o."CustomerId" = @customerId
        AND (@status::text IS NULL OR o."State" = @status)
        AND (@from::timestamptz IS NULL OR o."CreatedAtUtc" >= @from)
        AND (@to::timestamptz IS NULL OR o."CreatedAtUtc" < @to)
        AND (@qLike::text IS NULL OR o."OrderNumber" ILIKE @qLike ESCAPE '\'
             OR EXISTS (SELECT 1 FROM "OrderItems" i WHERE i."OrderId" = o."Id"
                        AND (i."ProductName" ILIKE @qLike ESCAPE '\' OR i."ProductSku" ILIKE @qLike ESCAPE '\')))
        """;

    public async Task<MyOrdersPage> ListAsync(MyOrdersFilter filter, CancellationToken cancellationToken)
    {
        await using var database = await contextFactory.CreateDbContextAsync(cancellationToken);
        var connection = (NpgsqlConnection)database.Database.GetDbConnection();
        await connection.OpenAsync(cancellationToken);

        var pageSql = $"""
            SELECT o."Id", o."OrderNumber", o."State", o."Total", o."CreatedAtUtc", o."ReservedUntilUtc", p."ProviderKey"
            FROM "Orders" o
            LEFT JOIN LATERAL (
                SELECT "ProviderKey" FROM "Payments" WHERE "OrderId" = o."Id" ORDER BY "CreatedAtUtc" DESC LIMIT 1
            ) p ON true
            WHERE {Filters}
            ORDER BY {OrderBy(filter.Sort)}
            LIMIT @pageSize OFFSET @offset
            """;
        var orders = new List<OrderRow>();
        await using (var command = CreateCommand(connection, pageSql, filter))
        {
            command.Parameters.AddWithValue("pageSize", filter.PageSize);
            command.Parameters.AddWithValue("offset", filter.Offset);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                orders.Add(new OrderRow(
                    reader.GetGuid(0),
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetDecimal(3),
                    reader.GetDateTime(4),
                    reader.GetDateTime(5),
                    reader.IsDBNull(6) ? null : reader.GetString(6)));
            }
        }

        var countSql = $"""SELECT COUNT(*) FROM "Orders" o WHERE {Filters}""";
        int total;
        await using (var command = CreateCommand(connection, countSql, filter))
        {
            total = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
        }

        var items = await LoadItemsAsync(connection, orders.Select(order => order.Id).ToArray(), cancellationToken);
        var summaries = orders.Select(order => new MyOrderSummary(
            order.Id,
            order.OrderNumber,
            order.State,
            order.Total,
            order.CreatedAtUtc,
            order.ReservedUntilUtc,
            order.ProviderKey,
            items.TryGetValue(order.Id, out var lines) ? lines : [])).ToArray();
        return new MyOrdersPage(summaries, filter.Page, filter.PageSize, total);
    }

    public async Task<MyOrderDetail?> GetAsync(Guid customerId, Guid orderId, CancellationToken cancellationToken)
    {
        await using var database = await contextFactory.CreateDbContextAsync(cancellationToken);
        var connection = (NpgsqlConnection)database.Database.GetDbConnection();
        await connection.OpenAsync(cancellationToken);

        const string sql = """
            SELECT o."Id", o."OrderNumber", o."State", o."Subtotal", o."Shipping", o."Total", o."CreatedAtUtc",
                   o."ReservedUntilUtc", o."ContactEmail", o."ShipRecipientName", o."ShipLine1", o."ShipLine2",
                   o."ShipCity", o."ShipRegion", o."ShipPostalCode", o."ShipCountry", p."ProviderKey", p."ExternalReference"
            FROM "Orders" o
            LEFT JOIN LATERAL (
                SELECT "ProviderKey", "ExternalReference" FROM "Payments"
                WHERE "OrderId" = o."Id" ORDER BY "CreatedAtUtc" DESC LIMIT 1
            ) p ON true
            WHERE o."Id" = @id AND o."CustomerId" = @customerId
            """;
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", orderId);
        command.Parameters.AddWithValue("customerId", customerId);
        MyOrderDetail? detail = null;
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            if (await reader.ReadAsync(cancellationToken))
            {
                detail = new MyOrderDetail(
                    reader.GetGuid(0),
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetDecimal(3),
                    reader.GetDecimal(4),
                    reader.GetDecimal(5),
                    reader.GetDateTime(6),
                    reader.GetDateTime(7),
                    reader.GetString(8),
                    new ShippingAddressView(
                        reader.GetString(9),
                        reader.GetString(10),
                        reader.IsDBNull(11) ? null : reader.GetString(11),
                        reader.GetString(12),
                        reader.GetString(13),
                        reader.GetString(14),
                        reader.GetString(15)),
                    reader.IsDBNull(16) ? null : reader.GetString(16),
                    reader.IsDBNull(17) ? null : reader.GetString(17),
                    []);
            }
        }

        if (detail is null)
        {
            return null;
        }

        var items = await LoadItemsAsync(connection, [detail.Id], cancellationToken);
        return detail with { Items = items.TryGetValue(detail.Id, out var lines) ? lines : [] };
    }

    private static string OrderBy(MyOrdersSort sort) => sort switch
    {
        MyOrdersSort.DateAsc => "o.\"CreatedAtUtc\" ASC, o.\"Id\" ASC",
        MyOrdersSort.TotalDesc => "o.\"Total\" DESC, o.\"CreatedAtUtc\" DESC, o.\"Id\" DESC",
        _ => "o.\"CreatedAtUtc\" DESC, o.\"Id\" DESC"
    };

    private static NpgsqlCommand CreateCommand(NpgsqlConnection connection, string sql, MyOrdersFilter filter)
    {
        var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("customerId", filter.CustomerId);
        command.Parameters.Add(new NpgsqlParameter("status", NpgsqlDbType.Text) { Value = (object?)filter.Status ?? DBNull.Value });
        command.Parameters.Add(new NpgsqlParameter("from", NpgsqlDbType.TimestampTz) { Value = (object?)filter.FromUtc ?? DBNull.Value });
        command.Parameters.Add(new NpgsqlParameter("to", NpgsqlDbType.TimestampTz) { Value = (object?)filter.ToUtc ?? DBNull.Value });
        command.Parameters.Add(new NpgsqlParameter("qLike", NpgsqlDbType.Text) { Value = (object?)filter.QueryPattern ?? DBNull.Value });
        return command;
    }

    private static async Task<Dictionary<Guid, List<MyOrderItemView>>> LoadItemsAsync(
        NpgsqlConnection connection,
        Guid[] orderIds,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<Guid, List<MyOrderItemView>>();
        if (orderIds.Length == 0)
        {
            return result;
        }

        const string sql = """
            SELECT "OrderId", "ProductName", "ProductSku", "Quantity", "UnitPrice"
            FROM "OrderItems" WHERE "OrderId" = ANY(@orderIds) ORDER BY "ProductName", "Id"
            """;
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("orderIds", orderIds);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var quantity = reader.GetInt32(3);
            var unitPrice = reader.GetDecimal(4);
            var orderId = reader.GetGuid(0);
            if (!result.TryGetValue(orderId, out var lines))
            {
                lines = [];
                result[orderId] = lines;
            }

            lines.Add(new MyOrderItemView(reader.GetString(1), reader.GetString(2), quantity, unitPrice, quantity * unitPrice));
        }

        return result;
    }

    private sealed record OrderRow(
        Guid Id,
        string OrderNumber,
        string State,
        decimal Total,
        DateTime CreatedAtUtc,
        DateTime ReservedUntilUtc,
        string? ProviderKey);
}
