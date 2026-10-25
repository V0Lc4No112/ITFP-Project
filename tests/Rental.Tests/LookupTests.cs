using Rental.Domain.Models;
using Rental.Domain.Policies;
using Rental.Domain.Results;
using Rental.Domain.States;

namespace Rental.Tests;

public class LookupTests
{
    private static Stock StockOf(params (string ItemCode, int Available)[] rows) =>
        Assert.IsType<Validation<Stock>.Valid>(Stock.Create(rows)).Value;

    private static BookingState.Quoted Quoted(RentalRequest request) =>
        new(new PricedRental(request, PricingPolicies.Standard(request)));

    [Fact]
    public void Available_ZeroIsPresent_UnknownIsNone()
    {
        var stock = StockOf(("SKI-02", 0), ("BIKE-01", 3));
        Assert.Equal(0, Assert.IsType<Option<int>.Some>(stock.Available("SKI-02")).Value);
        Assert.Equal(3, Assert.IsType<Option<int>.Some>(stock.Available("BIKE-01")).Value);
        Assert.IsType<Option<int>.None>(stock.Available("BOAT-01"));
    }

    [Fact]
    public void Confirm_ZeroAvailable_IsNotEnoughStock_NotNoStockRecord()
    {
        var result = BookingTransitions.Confirm(Quoted(SampleData.R2), StockOf(("SKI-02", 0)));
        var error = Assert.IsType<Result<Reservation>.Fail>(result).Error;
        Assert.Equal(ErrorCode.NotEnoughStock, error.Code);
        Assert.Equal("R2: not enough SKI-02 (requested 1, available 0)", error.Message);
    }

    [Theory]
    [InlineData("R2", ErrorCode.NoStockRecord, "R2: no stock record for SKI-02")]
    [InlineData("R5", ErrorCode.NotEnoughStock, "R5: not enough TENT-01 (requested 2, available 1)")]
    public void Confirm_DomainFailure_HasCodeAndMessage(string id, ErrorCode code, string message)
    {
        var request = id switch
        {
            "R2" => SampleData.R2,
            "R5" => SampleData.R5,
            _ => throw new InvalidOperationException("Unknown sample request"),
        };
        var result = BookingTransitions.Confirm(Quoted(request), StockOf(("BIKE-01", 3), ("TENT-01", 1)));
        var error = Assert.IsType<Result<Reservation>.Fail>(result).Error;
        Assert.Equal(code, error.Code);
        Assert.Equal(message, error.Message);
        Assert.False(error.IsOperational);
    }

    [Theory]
    [InlineData("standard")]
    [InlineData("DISCOUNT")]
    public void FromName_Known_IsSome(string name)
    {
        Assert.IsType<Option<Func<RentalRequest, long>>.Some>(PricingPolicies.FromName(name));
    }
}
