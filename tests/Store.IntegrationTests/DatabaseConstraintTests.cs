using System.Net;
using System.Net.Http.Json;
using Npgsql;

namespace Store.IntegrationTests;

public sealed class DatabaseConstraintTests(PostgreSqlFixture fixture) : IClassFixture<PostgreSqlFixture>
{
    [Theory]
    [InlineData("\"Price\" = 0", "CK_Products_Price_Positive")]
    [InlineData("\"Price\" = -1", "CK_Products_Price_Positive")]
    [InlineData("\"Reserved\" = \"Stock\" + 1", "CK_Products_Stock_Reserved")]
    [InlineData("\"Stock\" = -1, \"Reserved\" = 0", "CK_Products_Stock_Reserved")]
    [InlineData("\"WeightKg\" = -0.5", "CK_Products_Weight_NonNegative")]
    public async Task Database_RejectsInvalidProductRows(string assignment, string constraint)
    {
        using var factory = new TestApiFactory(fixture);
        using var client = factory.CreateClient();
        await AuthTestClient.SignInAsync(client, TestApiFactory.AdminEmail, factory.AdminPassword);
        var sku = $"CK-{Guid.NewGuid():N}"[..16];
        using var created = await client.PostAsJsonAsync("/api/admin/products", new
        {
            name = "Constraint probe",
            sku,
            description = "Probe",
            category = "Probes",
            price = 5.00m,
            stock = 3,
            weightKg = 1.0m,
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        await using var connection = new NpgsqlConnection(fixture.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            $"UPDATE \"Products\" SET {assignment} WHERE \"SkuNormalized\" = @sku",
            connection);
        command.Parameters.AddWithValue("sku", sku.ToUpperInvariant());

        var failure = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
        Assert.Equal(PostgresErrorCodes.CheckViolation, failure.SqlState);
        Assert.Equal(constraint, failure.ConstraintName);
    }

    [Theory]
    [InlineData("UPDATE \"OrderItems\" SET \"Quantity\" = 0", "CK_OrderItems_Quantity_Positive")]
    [InlineData("UPDATE \"Orders\" SET \"Total\" = \"Total\" + 1", "CK_Orders_Amounts")]
    [InlineData("UPDATE \"Orders\" SET \"State\" = 'Shipped'", "CK_Orders_State")]
    [InlineData("UPDATE \"Payments\" SET \"Amount\" = 0", "CK_Payments_Amount_Positive")]
    public async Task Database_RejectsInvalidOrderRows(string statement, string constraint)
    {
        using var factory = new TestApiFactory(fixture);
        using var admin = factory.CreateClient();
        await AuthTestClient.SignInAsync(admin, TestApiFactory.AdminEmail, factory.AdminPassword);
        var sku = $"OK-{Guid.NewGuid():N}"[..16];
        using var product = await admin.PostAsJsonAsync("/api/admin/products", new
        {
            name = "Order probe",
            sku,
            description = "Probe",
            category = "Probes",
            price = 9.00m,
            stock = 5,
            weightKg = 1.0m,
        });
        var productBody = await product.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var productId = productBody.GetProperty("id").GetGuid();

        using var customer = factory.CreateClient();
        await AuthTestClient.RegisterCustomerAsync(customer);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/orders")
        {
            Content = JsonContent.Create(new
            {
                shippingAddress = new
                {
                    recipientName = "Probe",
                    line1 = "1 Main St",
                    city = "Austin",
                    region = "TX",
                    postalCode = "73301",
                    country = "US",
                },
                items = new[] { new { productId, quantity = 1 } },
            }),
        };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        using var order = await customer.SendAsync(request);
        Assert.Equal(HttpStatusCode.Created, order.StatusCode);
        using var payment = await customer.PostAsync(
            $"/api/orders/{(await order.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>()).GetProperty("id").GetGuid()}/payment",
            null);
        Assert.True(payment.IsSuccessStatusCode);

        await using var connection = new NpgsqlConnection(fixture.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(statement, connection);

        var failure = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
        Assert.Equal(PostgresErrorCodes.CheckViolation, failure.SqlState);
        Assert.Equal(constraint, failure.ConstraintName);
    }
}
