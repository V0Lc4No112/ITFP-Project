using System.Collections.Immutable;
using Rental.App;
using Rental.App.Io;
using Rental.Domain.Models;
using Rental.Domain.Pipeline;
using Rental.Domain.Reports;
using Rental.Domain.Results;
using Rental.Domain.States;

namespace Rental.Tests;

/// <summary>
/// Regression tests for the six problems found in the final review of tag final-submission (dd7dd88).
/// Each test failed (or threw) on that version and passes after the fix.
/// </summary>
public class RegressionTests
{
    private static RentalRequest Request(string id, int quantity, bool member = false) =>
        new(id, "Anna", "BIKE-01", Category.Bike, 1000, quantity, 1, member);

    private static Stock StockOf(int bikes) =>
        ((Validation<Stock>.Valid)Stock.Create([("BIKE-01", bikes)])).Value;

    private static string TempDir() => Directory.CreateTempSubdirectory("rental-review-").FullName;

    // 1. Duplicate request ids -------------------------------------------------------------

    [Fact]
    public void RequestParser_DuplicateId_IsRejected()
    {
        var outcome = RequestParser.Parse([
            RequestParser.Header,
            "R1,Anna,BIKE-01,Bike,1500,1,2,true",
            "R1,Anna,BIKE-01,Bike,1500,1,2,true",
        ]);

        Assert.Single(outcome.Valid);
        Assert.Equal(new[] { "Row 3: Id R1 is listed twice" }, outcome.Errors.ToArray());
    }

    // 2. Coordinated save ------------------------------------------------------------------

    [Fact]
    public void SaveAll_StockTempWriteFails_OldBookingsUnchanged()
    {
        string dir = TempDir();
        string bookings = Path.Combine(dir, "bookings.csv");
        File.WriteAllText(bookings, "OLD");
        Directory.CreateDirectory(Path.Combine(dir, "stock.csv.tmp")); // forces the 2nd write to fail

        var result = SaveFolder.SaveAll(dir, [], StockOf(3));

        var fail = Assert.IsType<Result<string>.Fail>(result);
        Assert.Equal(ErrorCode.FileUnwritable, fail.Error.Code);
        Assert.Equal("OLD", File.ReadAllText(bookings));
    }

    [Fact]
    public void SaveAll_StockReplaceFails_OldBookingsRestored()
    {
        string dir = TempDir();
        string bookings = Path.Combine(dir, "bookings.csv");
        File.WriteAllText(bookings, "OLD");
        Directory.CreateDirectory(Path.Combine(dir, "stock.csv")); // the final move of stock.csv fails

        var result = SaveFolder.SaveAll(dir, [], StockOf(3));

        Assert.IsType<Result<string>.Fail>(result);
        Assert.Equal("OLD", File.ReadAllText(bookings));
        Assert.False(File.Exists(bookings + ".bak"));
        Assert.False(File.Exists(bookings + ".tmp"));
    }

    // 3. Empty paths -----------------------------------------------------------------------

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void DataFile_EmptyPath_FailsInsteadOfThrowing(string path)
    {
        var read = Assert.IsType<Result<IReadOnlyList<string>>.Fail>(DataFile.ReadLines("requests", path));
        Assert.Equal(ErrorCode.FileMissing, read.Error.Code);

        var write = Assert.IsType<Result<int>.Fail>(DataFile.WriteLines("stock", path, ["x"]));
        Assert.Equal(ErrorCode.FileUnwritable, write.Error.Code);
    }

    [Fact]
    public void QuoteCommand_EmptyPath_FailsWithFileMissing()
    {
        var command = Assert.IsType<Result<Command>.Ok>(Commands.Parse(["quote", "", "standard"])).Value;

        var fail = Assert.IsType<Result<ImmutableArray<string>>.Fail>(CommandRunner.Run(command));
        Assert.Equal(ErrorCode.FileMissing, fail.Error.Code);
    }

    // 4. Transitions enforce their own invariants --------------------------------------------

    [Fact]
    public void Confirm_HandBuiltNegativeQuantity_FailsAndStockUnchanged()
    {
        var stock = StockOf(3);
        var quoted = new BookingState.Quoted(new PricedRental(Request("R9", -2), 100));

        var fail = Assert.IsType<Result<Reservation>.Fail>(BookingTransitions.Confirm(quoted, stock));

        Assert.Equal(ErrorCode.InvalidRequest, fail.Error.Code);
        Assert.Equal(Option.Some(3), stock.Available("BIKE-01"));
    }

    [Fact]
    public void ReturnAndCancel_EventForAnotherBooking_IsInvalidEvent()
    {
        var booking = new BookingState.Confirmed(new PricedRental(Request("R1", 1), 1000), "B-R1");

        var returned = Assert.IsType<Result<ReturnResult>.Fail>(
            BookingTransitions.Return(booking, new ReturnEvent("B-R2", 1, 0)));
        var cancelled = Assert.IsType<Result<CancelResult>.Fail>(
            Cancellation.Cancel(booking, new CancelEvent("B-R2", 10)));

        Assert.Equal(ErrorCode.InvalidEvent, returned.Error.Code);
        Assert.Equal(ErrorCode.InvalidEvent, cancelled.Error.Code);
    }

    // 5. Limits and overflow ----------------------------------------------------------------

    [Fact]
    public void Restock_AboveMaxPerItem_IsInvalidEvent()
    {
        var fail = Assert.IsType<Result<Stock>.Fail>(
            Restocking.ApplyOne(StockOf(Stock.MaxPerItem), new Delivery("BIKE-01", 1)));

        Assert.Equal(ErrorCode.InvalidEvent, fail.Error.Code);
        Assert.IsType<Validation<Stock>.Invalid>(Stock.Create([("BIKE-01", int.MaxValue)]));
    }

    [Theory]
    [InlineData(ValidatedRental.MaxDailyRateCents, ValidatedRental.MaxQuantity, ValidatedRental.MaxDays, true)]
    [InlineData(ValidatedRental.MaxDailyRateCents + 1, 1, 1, false)]
    [InlineData(1000, ValidatedRental.MaxQuantity + 1, 1, false)]
    [InlineData(1000, 1, ValidatedRental.MaxDays + 1, false)]
    [InlineData(long.MaxValue / 2, 3, 1, false)]
    public void ValidatedRental_BusinessLimits(long rate, int quantity, int days, bool valid)
    {
        var candidate = new RentalRequest("R1", "Anna", "BIKE-01", Category.Bike, rate, quantity, days, false);

        Assert.Equal(valid, ValidatedRental.Create(candidate) is Validation<ValidatedRental>.Valid);
    }

    [Theory]
    [InlineData(-2, "-0.02")]
    [InlineData(-150, "-1.50")]
    [InlineData(0, "0.00")]
    [InlineData(74500, "745.00")]
    [InlineData(long.MinValue, "-92233720368547758.08")]
    public void MoneyFormat_KeepsTheSign(long cents, string expected) =>
        Assert.Equal(expected, Money.Format(cents));

    // 6. Membership must not depend on row order ----------------------------------------------

    [Fact]
    public void Invoice_ConflictingMembership_FailsInEitherOrder()
    {
        BookingState member = new BookingState.Confirmed(new PricedRental(Request("R1", 1, true), 1000), "B-R1");
        BookingState other = new BookingState.Confirmed(new PricedRental(Request("R2", 1, false), 1000), "B-R2");

        foreach (var rows in new[] { new[] { member, other }, new[] { other, member } })
        {
            var fail = Assert.IsType<Result<Invoice>.Fail>(Invoicing.For(rows, "Anna", Invoicing.DiscountFor));
            Assert.Equal(ErrorCode.InvalidRequest, fail.Error.Code);
        }
    }
}
