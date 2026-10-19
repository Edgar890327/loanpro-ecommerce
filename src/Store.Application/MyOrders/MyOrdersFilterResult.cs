namespace Store.Application;

public sealed record MyOrdersFilterResult(
    MyOrdersFilter? Filter,
    IReadOnlyDictionary<string, string[]> Errors)
{
    public bool IsValid => Filter is not null;
}
