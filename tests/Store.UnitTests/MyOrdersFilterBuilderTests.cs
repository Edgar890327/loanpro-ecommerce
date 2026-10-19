using Store.Application;

namespace Store.UnitTests;

public sealed class MyOrdersFilterBuilderTests
{
    private static readonly Guid Customer = Guid.NewGuid();

    private static MyOrdersFilterResult Build(
        string? q = null,
        string? status = null,
        string? from = null,
        string? to = null,
        string? sort = null,
        int page = 1,
        int pageSize = 10) =>
        MyOrdersFilterBuilder.Build(Customer, new MyOrdersQuery(q, status, from, to, sort, page, pageSize));

    [Fact]
    public void Build_EscapesPercentUnderscoreAndBackslashSoTheyMatchLiterally()
    {
        var result = Build(q: "50%_off\\");

        Assert.Equal("%50\\%\\_off\\\\%", result.Filter!.QueryPattern);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Build_TreatsEmptyOrWhitespaceTextAsNull(string? text)
    {
        Assert.Null(Build(q: text).Filter!.QueryPattern);
    }

    [Fact]
    public void Build_TrimsTextAndLimitsItsLength()
    {
        var result = Build(q: "  " + new string('a', 300) + "  ");

        Assert.Equal(MyOrdersFilterBuilder.MaxQueryLength + 2, result.Filter!.QueryPattern!.Length);
    }

    [Fact]
    public void Build_UsesAnExclusiveUpperBoundOnTheDayAfterTheToDate()
    {
        var filter = Build(from: "2026-03-01", to: "2026-03-31").Filter!;

        Assert.Equal(new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc), filter.FromUtc);
        Assert.Equal(new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc), filter.ToUtc);
        Assert.Equal(DateTimeKind.Utc, filter.ToUtc!.Value.Kind);
    }

    [Theory]
    [InlineData("date_desc", MyOrdersSort.DateDesc)]
    [InlineData("date_asc", MyOrdersSort.DateAsc)]
    [InlineData("total_desc", MyOrdersSort.TotalDesc)]
    [InlineData(null, MyOrdersSort.DateDesc)]
    public void Build_AcceptsOnlyWhitelistedSortValues(string? sort, MyOrdersSort expected)
    {
        Assert.Equal(expected, Build(sort: sort).Filter!.Sort);
    }

    [Theory]
    [InlineData("Total")]
    [InlineData("date_desc; DROP TABLE Orders")]
    [InlineData("total_asc")]
    public void Build_RejectsUnknownSortValues(string sort)
    {
        var result = Build(sort: sort);

        Assert.False(result.IsValid);
        Assert.Contains("sort", result.Errors.Keys);
    }

    [Fact]
    public void Build_RejectsUnknownStatusesAndMalformedDates()
    {
        var result = Build(status: "Shipped", from: "01/03/2026", to: "tomorrow");

        Assert.Contains("status", result.Errors.Keys);
        Assert.Contains("from", result.Errors.Keys);
        Assert.Contains("to", result.Errors.Keys);
    }

    [Theory]
    [InlineData("paid", "Paid")]
    [InlineData("Reserved", "Reserved")]
    public void Build_NormalizesTheStatusName(string input, string expected)
    {
        Assert.Equal(expected, Build(status: input).Filter!.Status);
    }

    [Theory]
    [InlineData(1000, 50)]
    [InlineData(0, 1)]
    [InlineData(-5, 1)]
    [InlineData(25, 25)]
    public void Build_BoundsThePageSize(int requested, int expected)
    {
        Assert.Equal(expected, Build(pageSize: requested).Filter!.PageSize);
    }

    [Fact]
    public void Build_ComputesTheOffsetAndNeverGoesBelowPageOne()
    {
        Assert.Equal(20, Build(page: 3, pageSize: 10).Filter!.Offset);
        Assert.Equal(1, Build(page: -4).Filter!.Page);
    }

    [Fact]
    public void Build_AlwaysCarriesTheCustomerFromTheCaller()
    {
        Assert.Equal(Customer, Build().Filter!.CustomerId);
    }
}
