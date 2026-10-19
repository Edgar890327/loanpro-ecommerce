namespace Store.Application;

public sealed record ShippingAddressView(
    string RecipientName,
    string Line1,
    string? Line2,
    string City,
    string Region,
    string PostalCode,
    string Country);
