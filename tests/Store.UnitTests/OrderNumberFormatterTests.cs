using Store.Domain;

namespace Store.UnitTests;

public sealed class OrderNumberFormatterTests
{
    [Theory]
    [InlineData(2026, 123, "ORD-2026-000123")]
    [InlineData(2027, 1, "ORD-2027-000001")]
    [InlineData(2026, 1234567, "ORD-2026-1234567")]
    public void Format_UsesTheYearAndAZeroPaddedSequence(int year, long sequence, string expected)
    {
        Assert.Equal(expected, OrderNumberFormatter.Format(new DateTime(year, 5, 1, 0, 0, 0, DateTimeKind.Utc), sequence));
    }

    [Fact]
    public void Format_ProducesDistinctNumbersForDistinctSequenceValues()
    {
        var createdAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var numbers = Enumerable.Range(1, 1000).Select(value => OrderNumberFormatter.Format(createdAt, value)).ToList();

        Assert.Equal(numbers.Count, numbers.Distinct().Count());
    }

    [Fact]
    public void Format_RejectsNonPositiveSequenceValues()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => OrderNumberFormatter.Format(DateTime.UtcNow, 0));
    }
}
