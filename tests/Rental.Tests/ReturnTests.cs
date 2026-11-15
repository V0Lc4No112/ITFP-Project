using System.Collections.Immutable;
using Rental.App;
using Rental.App.Io;
using Rental.App.Screens;
using Rental.App.Workflows;
using Rental.Domain.Models;
using Rental.Domain.Pipeline;
using Rental.Domain.Results;
using Rental.Domain.States;

namespace Rental.Tests;

/// <summary>Member C: W5 return. Bookings = data/bookings.csv, stock = data/stock-booked.csv.</summary>
public class ReturnTests
{
    private static readonly RentalRequest R7 = new("R7", "Anna", "SKI-02", Category.Ski, 4000, 1, 2, true);
    private static readonly RentalRequest R8 = new("R8", "Ben", "TENT-01", Category.Camping, 2500, 1, 3, false);

    private static readonly ImmutableArray<BookingState> Bookings =
    [
        new BookingState.Confirmed(new PricedRental(SampleData.R3, 35000), "B-R3"),
        new BookingState.Confirmed(new PricedRental(SampleData.R1, 15000), "B-R1"),
        new BookingState.Confirmed(new PricedRental(SampleData.R2, 12000), "B-R2"),
        new BookingState.Confirmed(new PricedRental(SampleData.R6, 12000), "B-R6"),
        new BookingState.Returned(new PricedRental(R7, 8000), "B-R7"),
        new BookingState.Cancelled(new PricedRental(R8, 7500), "B-R8"),
    ];

    private static readonly Stock Booked =
        Assert.IsType<Validation<Stock>.Valid>(Stock.Create([("BIKE-01", 0), ("SKI-02", 0), ("TENT-01", 1)])).Value;

    private static ReturnResult Returned(BookingState state, ReturnEvent ev) =>
        Assert.IsType<Result<ReturnResult>.Ok>(BookingTransitions.Return(state, ev)).Value;

    private static Error Rejected(BookingState state, ReturnEvent ev) =>
        Assert.IsType<Result<ReturnResult>.Fail>(BookingTransitions.Return(state, ev)).Error;

    [Fact]
    public void Return_OnTime_NoFee_BecomesReturned()
    {
        var result = Returned(Bookings[1], new ReturnEvent("B-R1", 5, 0));

        Assert.Equal(new BookingState.Returned(new PricedRental(SampleData.R1, 15000), "B-R1"), result.Booking);
        Assert.Equal(0, result.LateDays);
        Assert.Equal(0, result.FeeCents);
    }

    [Fact]
    public void Fee_Late_ChargesOneAndHalfDailyRatePerItem()
    {
        Assert.Equal(12000, Returns.Fee(SampleData.R2, lateDays: 2, damaged: 0)); // 2 × 40.00 × 1 × 1.5
        Assert.Equal(4500, Returns.Fee(SampleData.R1, lateDays: 1, damaged: 0));  // 1 × 15.00 × 2 × 1.5
    }

    [Fact]
    public void Fee_LateAndDamaged_AddsBoth()
    {
        Assert.Equal(2000, Returns.Fee(SampleData.R2, 0, 1));
        Assert.Equal(14000, Returned(Bookings[2], new ReturnEvent("B-R2", 5, 1)).FeeCents);
    }

    [Fact]
    public void Return_ReturnedOrCancelled_IsInvalidTransition()
    {
        var twice = Rejected(Bookings[4], new ReturnEvent("B-R7", 2, 0));
        Assert.Equal(ErrorCode.InvalidTransition, twice.Code);
        Assert.Equal("B-R7: already returned", twice.Message);

        var cancelled = Rejected(Bookings[5], new ReturnEvent("B-R8", 3, 0));
        Assert.Equal(ErrorCode.InvalidTransition, cancelled.Code);
        Assert.Equal("B-R8: was cancelled, nothing to return", cancelled.Message);
    }

    [Theory]
    [InlineData(0, 0, false)] // boundary: 0 days used is impossible
    [InlineData(1, 0, true)]  // boundary: 1 day is the minimum
    [InlineData(5, -1, false)]
    [InlineData(5, 2, true)]  // R1 has 2 items: both damaged is allowed
    [InlineData(5, 3, false)] // more damaged than rented
    public void Return_EventChecks(int daysUsed, int damaged, bool ok)
    {
        var result = BookingTransitions.Return(Bookings[1], new ReturnEvent("B-R1", daysUsed, damaged));

        if (ok)
        {
            Assert.IsType<Result<ReturnResult>.Ok>(result);
        }
        else
        {
            Assert.Equal(ErrorCode.InvalidEvent, Assert.IsType<Result<ReturnResult>.Fail>(result).Error.Code);
        }
    }

    [Fact]
    public void ApplyAll_SampleFile_ChargesReleasesAndRejectsBadOrdering()
    {
        ReturnEvent[] events = [new("B-R1", 5, 0), new("B-R2", 5, 1), new("B-R1", 5, 0), new("B-R8", 3, 0), new("B-R99", 2, 0)];

        var report = Returns.ApplyAll(Bookings, Booked, events);

        Assert.Equal(14000, report.ExtraChargesCents);
        Assert.Equal("BIKE-01=2, SKI-02=0, TENT-01=1", Money.FormatStock(report.Stock)); // damaged ski not released
        Assert.Equal(
            new[] { "Ok", "Ok", "InvalidTransition", "InvalidTransition", "UnknownBooking" },
            report.Lines.Select(line => line.Result switch
            {
                Result<ReturnResult>.Fail fail => fail.Error.Code.ToString(),
                _ => "Ok",
            }));
        Assert.Equal(
            new[] { "Confirmed", "Returned", "Returned", "Confirmed", "Returned", "Cancelled" },
            report.Bookings.Select(b => b.GetType().Name));
    }

    [Fact]
    public void ApplyAll_DoesNotChangeInputBookingsOrStock()
    {
        _ = Returns.ApplyAll(Bookings, Booked, [new("B-R1", 5, 0)]);

        Assert.IsType<BookingState.Confirmed>(Bookings[1]);
        Assert.Equal("BIKE-01=0, SKI-02=0, TENT-01=1", Money.FormatStock(Booked));
    }

    [Fact]
    public void ReturnWorkflow_MalformedReturnsFile_FailsWithEveryRow()
    {
        using var temp = new TempFolder();
        string bookings = temp.Write("bookings.csv", BookingFile.Lines(Bookings).ToArray());
        string returns = temp.Write("returns.csv", ReturnParser.Header, "B-R1,five,0", "B-R2,5");
        string stock = temp.Write("stock.csv", StockParser.Lines(Booked).ToArray());

        var fail = Assert.IsType<Result<ReturnOutcome>.Fail>(
            ReturnWorkflow.Run(bookings, returns, stock, Option.None<string>()));

        Assert.Equal(ErrorCode.MalformedFile, fail.Error.Code);
        Assert.Equal(
            new[] { "Row 2: DaysUsed must be an integer (was five)", "Row 3: Expected 3 columns, found 2" },
            fail.Error.Details);
    }

    [Fact]
    public void ReturnWorkflow_SampleFiles_RendersScreen()
    {
        using var temp = new TempFolder();
        string bookings = temp.Write("bookings.csv", BookingFile.Lines(Bookings).ToArray());
        string returns = temp.Write("returns.csv", ReturnParser.Header, "B-R2,5,1", "B-R8,3,0");
        string stock = temp.Write("stock.csv", StockParser.Lines(Booked).ToArray());

        var screen = ReturnWorkflow.Run(bookings, returns, stock, Option.None<string>()).Map(ReturnScreen.Render);

        Assert.Equal(new[]
        {
            "Returns:",
            "B-R2   returned   late 2 day(s), damaged 1, extra   140.00",
            "B-R8   REJECTED   [InvalidTransition] B-R8: was cancelled, nothing to return",
            "Extra charges: 140.00",
            "Stock now: BIKE-01=0, SKI-02=0, TENT-01=1",
        }, Assert.IsType<Result<ImmutableArray<string>>.Ok>(screen).Value);
    }
}
