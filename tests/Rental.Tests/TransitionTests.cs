using Rental.Domain.Models;
using Rental.Domain.States;

namespace Rental.Tests;

public class TransitionTests
{
    private static Stock StockOf(params (string ItemCode, int Available)[] rows) =>
        Assert.IsType<Validation<Stock>.Valid>(Stock.Create(rows)).Value;

    private static BookingState.Quoted Quoted(RentalRequest request, long totalCents) =>
        new(new PricedRental(request, totalCents));

    [Fact]
    public void Confirm_EnoughStock_AcceptsAndReserves()
    {
        var result = BookingTransitions.Confirm(Quoted(SampleData.R1, 15000), StockOf(("BIKE-01", 3)));

        var accepted = Assert.IsType<ConfirmResult.Accepted>(result);
        Assert.Equal("B-R1", accepted.Booking.BookingId);
        Assert.Equal(1, accepted.Stock.Available("BIKE-01"));
    }

    [Fact]
    public void Confirm_QuantityEqualsAvailable_AcceptsAndLeavesZero()
    {
        var result = BookingTransitions.Confirm(Quoted(SampleData.R1, 15000), StockOf(("BIKE-01", 2)));

        Assert.Equal(0, Assert.IsType<ConfirmResult.Accepted>(result).Stock.Available("BIKE-01"));
    }

    [Fact]
    public void Confirm_NotEnough_RejectsAndKeepsStock()
    {
        var stock = StockOf(("TENT-01", 1));

        var result = BookingTransitions.Confirm(Quoted(SampleData.R5, 5000), stock);

        Assert.Equal("R5: not enough TENT-01 (requested 2, available 1)", Assert.IsType<ConfirmResult.Rejected>(result).Reason);
        Assert.Equal(1, stock.Available("TENT-01"));
    }

    [Fact]
    public void Confirm_NoStockRecord_Rejects()
    {
        var result = BookingTransitions.Confirm(Quoted(SampleData.R2, 12000), StockOf(("BIKE-01", 3)));

        Assert.Equal("R2: no stock record for SKI-02", Assert.IsType<ConfirmResult.Rejected>(result).Reason);
    }

    [Fact]
    public void Confirm_Twice_ReservesOnlyOnce()
    {
        var first = Assert.IsType<ConfirmResult.Accepted>(
            BookingTransitions.Confirm(Quoted(SampleData.R1, 15000), StockOf(("BIKE-01", 3))));

        var second = Assert.IsType<ConfirmResult.Accepted>(BookingTransitions.Confirm(first.Booking, first.Stock));

        Assert.Same(first.Booking, second.Booking);
        Assert.Same(first.Stock, second.Stock);
        Assert.Equal(1, second.Stock.Available("BIKE-01"));
    }
}
