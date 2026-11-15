using System.Collections.Immutable;
using Rental.App;
using Rental.App.Io;
using Rental.App.Workflows;
using Rental.Domain.Models;
using Rental.Domain.Pipeline;
using Rental.Domain.Results;
using Rental.Domain.States;

namespace Rental.Tests;

/// <summary>Member D: W6 cancel. Expected numbers are in README ("cancel" demo).</summary>
public class CancelTests
{
    private static readonly RentalRequest R7 = new("R7", "Anna", "SKI-02", Category.Ski, 4000, 1, 2, true);
    private static readonly RentalRequest R8 = new("R8", "Ben", "TENT-01", Category.Camping, 2500, 1, 3, false);

    // The six rows of data/bookings.csv.
    private static readonly ImmutableArray<BookingState> Bookings =
    [
        new BookingState.Confirmed(new PricedRental(SampleData.R3, 35000), "B-R3"),
        new BookingState.Confirmed(new PricedRental(SampleData.R1, 15000), "B-R1"),
        new BookingState.Confirmed(new PricedRental(SampleData.R2, 12000), "B-R2"),
        new BookingState.Confirmed(new PricedRental(SampleData.R6, 12000), "B-R6"),
        new BookingState.Returned(new PricedRental(R7, 8000), "B-R7"),
        new BookingState.Cancelled(new PricedRental(R8, 7500), "B-R8"),
    ];

    // data/stock-booked.csv: the stock left after "book standard".
    private static readonly Stock Booked = StockOf(("BIKE-01", 0), ("SKI-02", 0), ("TENT-01", 1));

    private static Stock StockOf(params (string, int)[] rows) =>
        Assert.IsType<Validation<Stock>.Valid>(Stock.Create(rows)).Value;

    private static string Shelf(Stock stock) => Money.FormatStock(stock);

    [Theory]
    [InlineData(30, 100)]
    [InlineData(7, 100)]
    [InlineData(6, 50)]
    [InlineData(2, 50)]
    [InlineData(1, 0)]
    [InlineData(0, 0)]
    public void RefundPercent_TierBoundaries(int daysBeforeStart, int expected)
    {
        var ok = Assert.IsType<Result<int>.Ok>(Cancellation.RefundPercent("B-R1", daysBeforeStart));
        Assert.Equal(expected, ok.Value);
    }

    [Fact]
    public void RefundPercent_Negative_IsInvalidEvent()
    {
        var fail = Assert.IsType<Result<int>.Fail>(Cancellation.RefundPercent("B-R1", -1));
        Assert.Equal(ErrorCode.InvalidEvent, fail.Error.Code);
        Assert.Equal("B-R1: days before start must be >= 0 (was -1)", fail.Error.Message);
    }

    [Fact]
    public void Cancel_Confirmed_BecomesCancelled_WithRefund()
    {
        var ok = Assert.IsType<Result<CancelResult>.Ok>(
            Cancellation.Cancel(Bookings[3], new CancelEvent("B-R6", 3)));

        Assert.Equal(new BookingState.Cancelled(new PricedRental(SampleData.R6, 12000), "B-R6"), ok.Value.Booking);
        Assert.Equal(50, ok.Value.RefundPercent);
        Assert.Equal(6000, ok.Value.RefundCents);
    }

    [Fact]
    public void Cancel_ReturnedOrCancelledOrQuoted_IsInvalidTransition()
    {
        Assert.Equal(
            "B-R7: already returned, cannot cancel",
            Assert.IsType<Result<CancelResult>.Fail>(Cancellation.Cancel(Bookings[4], new CancelEvent("B-R7", 10))).Error.Message);
        Assert.Equal(
            "B-R8: already cancelled",
            Assert.IsType<Result<CancelResult>.Fail>(Cancellation.Cancel(Bookings[5], new CancelEvent("B-R8", 10))).Error.Message);

        var quoted = new BookingState.Quoted(new PricedRental(SampleData.R5, 5000));
        var fail = Assert.IsType<Result<CancelResult>.Fail>(Cancellation.Cancel(quoted, new CancelEvent("R5", 10)));
        Assert.Equal(ErrorCode.InvalidTransition, fail.Error.Code);
    }

    [Fact]
    public void ApplyAll_SampleFile_RefundsReleasesAndRejectsRepeats()
    {
        CancelEvent[] events = [new("B-R3", 10), new("B-R6", 3), new("B-R2", 0), new("B-R8", 5), new("B-R6", 9)];

        var report = Cancellations.ApplyAll(Bookings, Booked, events);

        Assert.Equal(41000, report.RefundCents);
        Assert.Equal("BIKE-01=1, SKI-02=1, TENT-01=2", Shelf(report.Stock));
        Assert.Equal(
            new[] { "Ok", "Ok", "Ok", "InvalidTransition", "InvalidTransition" },
            report.Lines.Select(line => line.Result switch
            {
                Result<CancelResult>.Fail fail => fail.Error.Code.ToString(),
                _ => "Ok",
            }));
        Assert.Equal(
            new[] { "Cancelled", "Confirmed", "Cancelled", "Cancelled", "Returned", "Cancelled" },
            report.Bookings.Select(b => b.GetType().Name));
    }

    [Fact]
    public void ApplyAll_UnknownBookingOrItem_IsRejected_AndInputsStayUnchanged()
    {
        var noTents = StockOf(("BIKE-01", 0), ("SKI-02", 0));

        var report = Cancellations.ApplyAll(Bookings, noTents, [new("B-R99", 10), new("B-R3", 10)]);

        Assert.Equal(
            new[] { ErrorCode.UnknownBooking, ErrorCode.UnknownItem },
            report.Lines.Select(line => Assert.IsType<Result<CancelResult>.Fail>(line.Result).Error.Code));
        Assert.Equal(0, report.RefundCents);
        Assert.True(report.Bookings.SequenceEqual(Bookings));
        Assert.IsType<BookingState.Confirmed>(Bookings[0]);
        Assert.Equal("BIKE-01=0, SKI-02=0, TENT-01=1", Shelf(Booked));
    }

    [Fact]
    public void ApplyAll_NoEvents_ChangesNothing()
    {
        var report = Cancellations.ApplyAll(Bookings, Booked, []);

        Assert.Empty(report.Lines);
        Assert.Equal(0, report.RefundCents);
        Assert.Same(Booked, report.Stock);
    }

    [Fact]
    public void CancellationParser_MalformedFile_FailsWithEveryRow()
    {
        using var temp = new TempFolder();
        string path = temp.Write("cancellations.csv", CancellationParser.Header, "B-R3,ten", "B-R6");

        var fail = Assert.IsType<Result<ImmutableArray<CancelEvent>>.Fail>(CancellationParser.Load(path));

        Assert.Equal(ErrorCode.MalformedFile, fail.Error.Code);
        Assert.Equal(
            new[] { "Row 2: DaysBeforeStart must be an integer (was ten)", "Row 3: Expected 2 columns, found 1" },
            fail.Error.Details);
    }

    [Fact]
    public void CancelWorkflow_MissingCancellationsFile_FailsBeforeStockIsRead()
    {
        using var temp = new TempFolder();
        string bookings = temp.Write("bookings.csv", BookingFile.Lines(Bookings).ToArray());

        var result = CancelWorkflow.Run(
            bookings, temp.PathOf("none.csv"), temp.PathOf("also-missing-stock.csv"), Option.None<string>());

        var fail = Assert.IsType<Result<CancelOutcome>.Fail>(result);
        Assert.Equal(ErrorCode.FileMissing, fail.Error.Code);
        Assert.Contains("cancellations", fail.Error.Message);
    }
}
