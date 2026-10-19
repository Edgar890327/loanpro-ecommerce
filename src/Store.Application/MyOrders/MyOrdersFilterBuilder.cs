using System.Globalization;
using Store.Domain;

namespace Store.Application;

public static class MyOrdersFilterBuilder
{
    public const int DefaultPageSize = 10;
    public const int MaxPageSize = 50;
    public const int MaxQueryLength = 100;

    public static MyOrdersFilterResult Build(Guid customerId, MyOrdersQuery query)
    {
        var errors = new Dictionary<string, string[]>();
        var status = ParseStatus(query.Status, errors);
        var sort = ParseSort(query.Sort, errors);
        var from = ParseDate(query.From, "from", errors);
        var to = ParseDate(query.To, "to", errors);
        if (errors.Count > 0)
        {
            return new MyOrdersFilterResult(null, errors);
        }

        var text = query.Q?.Trim();
        if (text is { Length: > MaxQueryLength })
        {
            text = text[..MaxQueryLength];
        }

        var filter = new MyOrdersFilter(
            customerId,
            status,
            string.IsNullOrEmpty(text) ? null : $"%{EscapeLike(text)}%",
            from,
            to?.AddDays(1),
            sort,
            Math.Max(1, query.Page),
            Math.Clamp(query.PageSize, 1, MaxPageSize));
        return new MyOrdersFilterResult(filter, errors);
    }

    public static string EscapeLike(string text) =>
        text.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);

    private static string? ParseStatus(string? value, Dictionary<string, string[]> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (Enum.TryParse<OrderState>(value.Trim(), true, out var state) && Enum.IsDefined(state))
        {
            return state.ToString();
        }

        errors["status"] = ["Unknown order status."];
        return null;
    }

    private static MyOrdersSort ParseSort(string? value, Dictionary<string, string[]> errors)
    {
        switch (value?.Trim())
        {
            case null or "" or "date_desc":
                return MyOrdersSort.DateDesc;
            case "date_asc":
                return MyOrdersSort.DateAsc;
            case "total_desc":
                return MyOrdersSort.TotalDesc;
            default:
                errors["sort"] = ["Sort must be date_desc, date_asc or total_desc."];
                return MyOrdersSort.DateDesc;
        }
    }

    private static DateTime? ParseDate(string? value, string name, Dictionary<string, string[]> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (DateOnly.TryParseExact(value.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            return date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        }

        errors[name] = ["Dates must use the yyyy-MM-dd format."];
        return null;
    }
}
