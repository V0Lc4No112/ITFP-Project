using Rental.Domain.Models;
using Rental.Domain.Results;
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

        var accepted = Assert.IsType<Result<Reservation>.Ok>(result).Value;
        Assert.Equal("B-R1", accepted.Booking.BookingId);
        Assert.Equal(Option.Some(1), accepted.Stock.Available("BIKE-01"));
    }

    [Fact]
    public void Confirm_QuantityEqualsAvailable_AcceptsAndLeavesZero()
    {
        var result = BookingTransitions.Confirm(Quoted(SampleData.R1, 15000), StockOf(("BIKE-01", 2)));

        var accepted = Assert.IsType<Result<Reservation>.Ok>(result).Value;
        Assert.Equal(Option.Some(0), accepted.Stock.Available("BIKE-01"));
    }

    [Fact]
    public void Confirm_NotEnough_RejectsAndKeepsStock()
    {
        var stock = StockOf(("TENT-01", 1));
        var result = BookingTransitions.Confirm(Quoted(SampleData.R5, 5000), stock);

        var error = Assert.IsType<Result<Reservation>.Fail>(result).Error;
        Assert.Equal(ErrorCode.NotEnoughStock, error.Code);
        Assert.Equal("R5: not enough TENT-01 (requested 2, available 1)", error.Message);
        Assert.Equal(Option.Some(1), stock.Available("TENT-01"));
    }

    [Fact]
    public void Confirm_NoStockRecord_Rejects()
    {
        var result = BookingTransitions.Confirm(Quoted(SampleData.R2, 12000), StockOf(("BIKE-01", 3)));

        var error = Assert.IsType<Result<Reservation>.Fail>(result).Error;
        Assert.Equal(ErrorCode.NoStockRecord, error.Code);
        Assert.Equal("R2: no stock record for SKI-02", error.Message);
    }

    [Fact]
    public void Confirm_Twice_ReservesOnlyOnce()
    {
        var first = Assert.IsType<Result<Reservation>.Ok>(
            BookingTransitions.Confirm(Quoted(SampleData.R1, 15000), StockOf(("BIKE-01", 3)))).Value;

        var second = Assert.IsType<Result<Reservation>.Ok>(
            BookingTransitions.Confirm(first.Booking, first.Stock)).Value;

        Assert.Same(first.Booking, second.Booking);
        Assert.Same(first.Stock, second.Stock);
        Assert.Equal(Option.Some(1), second.Stock.Available("BIKE-01"));
    }
}
