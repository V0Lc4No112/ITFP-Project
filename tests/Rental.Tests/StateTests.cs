using Rental.Domain.Results;
using Rental.Domain.States;

namespace Rental.Tests;

public class StateTests
{
    [Fact]
    public void Invalid_WithNoReasons_Throws()
    {
        Assert.Throws<ArgumentException>(() => new Validation<int>.Invalid([]));
    }

    [Fact]
    public void Create_DuplicateOrNegative_RejectsWithReason()
    {
        var duplicate = Stock.Create([("BIKE-01", 3), ("BIKE-01", 1)]);
        var negative = Stock.Create([("BIKE-01", -1)]);
        Assert.Equal(new[] { "ItemCode BIKE-01 is listed twice" }, Assert.IsType<Validation<Stock>.Invalid>(duplicate).Reasons);
        Assert.Equal(new[] { "Available for BIKE-01 must be >= 0 (was -1)" }, Assert.IsType<Validation<Stock>.Invalid>(negative).Reasons);
    }

    [Fact]
    public void Available_MissingItem_IsNone_ZeroIsSomeZero()
    {
        var stock = Assert.IsType<Validation<Stock>.Valid>(Stock.Create([("BIKE-01", 0)])).Value;
        Assert.IsType<Option<int>.None>(stock.Available("BOAT"));
        Assert.Equal(Option.Some(0), stock.Available("BIKE-01"));
    }

    [Fact]
    public void Create_DoesNotKeepCallerList()
    {
        var rows = new List<(string ItemCode, int Available)> { ("BIKE-01", 3) };
        var stock = Assert.IsType<Validation<Stock>.Valid>(Stock.Create(rows)).Value;

        rows[0] = ("BIKE-01", 99);
        rows.Add(("SKI-02", 1));
        Assert.Equal(Option.Some(3), stock.Available("BIKE-01"));
        Assert.IsType<Option<int>.None>(stock.Available("SKI-02"));
    }
}
