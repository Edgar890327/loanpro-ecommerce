namespace Store.Api.Orders;

public sealed record ShippingAddressResponse(
    string RecipientName,
    string Line1,
    string? Line2,
    string City,
    string Region,
    string PostalCode,
    string Country);
