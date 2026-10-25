using Rental.Domain.Pipeline;
using Rental.Domain.Policies;
using Rental.Domain.States;

namespace Rental.Tests;

public class BookingTests
{
    /// <summary>The rows of data/stock.csv.</summary>
    private static readonly (string ItemCode, int Available)[] StockRows =
        [("BIKE-01", 3), ("SKI-02", 1), ("TENT-01", 2)];

    private static Stock StockOf(params (string ItemCode, int Available)[] rows) =>
        Stock.Create(rows) switch
        {
            Validation<Stock>.Valid valid => valid.Value,
            Validation<Stock>.Invalid invalid =>
                throw new InvalidOperationException($"Test stock is invalid: {string.Join("; ", invalid.Reasons)}"),
            _ => throw new InvalidOperationException("Unknown validation result"),
        };

    private static BookingReport ConfirmSample(Stock stock)
    {
        var summary = RentalWorkflow.Run(SampleData.ValidatedAll, PricingPolicies.Standard, RentalWorkflow.DefaultMaxDays);
        return Booking.ConfirmAll(summary, stock);
    }

    private static string? AcceptedId(BookingLine line) =>
        line.Result switch
        {
            ConfirmResult.Accepted accepted => accepted.Booking.BookingId,
            ConfirmResult.Rejected => null,
            _ => throw new InvalidOperationException("Unknown confirm result"),
        };

    private static string? RejectedReason(BookingLine line) =>
        line.Result switch
        {
            ConfirmResult.Accepted => null,
            ConfirmResult.Rejected rejected => rejected.Reason,
            _ => throw new InvalidOperationException("Unknown confirm result"),
        };

    [Fact]
    public void ConfirmAll_SampleData_ConfirmsFourRejectsR5()
    {
        var report = ConfirmSample(StockOf(StockRows));

        Assert.Equal(new[] { "R3", "R1", "R2", "R6", "R5" }, report.Lines.Select(l => l.Quote.Request.Id));
        Assert.Equal(new[] { "B-R3", "B-R1", "B-R2", "B-R6", null }, report.Lines.Select(AcceptedId));
        Assert.Equal("R5: not enough TENT-01 (requested 2, available 1)", RejectedReason(report.Lines[4]));
        Assert.Equal(74000, report.ConfirmedTotalCents);
        Assert.Equal(0, report.RemainingStock.Available("BIKE-01"));
        Assert.Equal(0, report.RemainingStock.Available("SKI-02"));
        Assert.Equal(1, report.RemainingStock.Available("TENT-01"));
    }

    [Fact]
    public void ConfirmAll_MissingStockRecord_RejectsOnlyThatItem()
    {
        var report = ConfirmSample(StockOf(("BIKE-01", 3), ("TENT-01", 2)));

        Assert.Equal(new[] { "B-R3", "B-R1", null, "B-R6", null }, report.Lines.Select(AcceptedId));
        Assert.Equal("R2: no stock record for SKI-02", RejectedReason(report.Lines[2]));
        Assert.Equal("R5: not enough TENT-01 (requested 2, available 1)", RejectedReason(report.Lines[4]));
        Assert.Equal(62000, report.ConfirmedTotalCents);
        Assert.Equal(0, report.RemainingStock.Available("BIKE-01"));
        Assert.Null(report.RemainingStock.Available("SKI-02"));
        Assert.Equal(1, report.RemainingStock.Available("TENT-01"));
    }

    [Fact]
    public void ConfirmAll_DoesNotChangeInputStock()
    {
        var stock = StockOf(StockRows);

        ConfirmSample(stock);

        Assert.Equal(3, stock.Available("BIKE-01"));
        Assert.Equal(1, stock.Available("SKI-02"));
        Assert.Equal(2, stock.Available("TENT-01"));
    }
}
