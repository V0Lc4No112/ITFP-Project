using System.Collections.Immutable;
using Rental.App.Screens;
using Rental.App.Workflows;
using Rental.Domain.Extensions;
using Rental.Domain.Models;
using Rental.Domain.Reports;
using Rental.Domain.Results;
using Rental.Domain.States;

namespace Rental.Tests;

public class InvoiceTests
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

    private static Invoice InvoiceOf(IReadOnlyList<BookingState> bookings, string customer) =>
        Assert.IsType<Result<Invoice>.Ok>(Invoicing.For(bookings, customer, Invoicing.DiscountFor)).Value;

    [Fact]
    public void For_Member_Gets10PercentOff_ReturnedBookingStillBilled()
    {
        var invoice = InvoiceOf(Bookings, "Anna");

        Assert.True(invoice.IsMember);
        Assert.Equal(new[] { "B-R1", "B-R7" }, invoice.Lines.Select(l => l.BookingId));
        Assert.Equal(23000, invoice.SubtotalCents);
        Assert.Equal(2300, invoice.DiscountCents);
        Assert.Equal(20700, invoice.TotalCents);
    }

    [Fact]
    public void For_NonMember_PaysSubtotal_AndCancelledBookingIsNotBilled()
    {
        var invoice = InvoiceOf(Bookings, "  ben ");

        Assert.Equal("Ben", invoice.Customer);
        Assert.Equal(new[] { "B-R2" }, invoice.Lines.Select(l => l.BookingId));
        Assert.Equal(0, invoice.DiscountCents);
        Assert.Equal(12000, invoice.TotalCents);
    }

    [Fact]
    public void For_UnknownCustomer_FailsWithUnknownCustomer()
    {
        var fail = Assert.IsType<Result<Invoice>.Fail>(Invoicing.For(Bookings, "Zoe", Invoicing.DiscountFor));

        Assert.Equal(ErrorCode.UnknownCustomer, fail.Error.Code);
        Assert.Equal("No bookings for customer 'Zoe'", fail.Error.Message);
    }

    [Fact]
    public void For_OnlyCancelledBookings_GivesZeroTotal_NotFailure()
    {
        var invoice = InvoiceOf([Bookings[5]], "Ben");

        Assert.Empty(invoice.Lines);
        Assert.Equal(0, invoice.TotalCents);
    }

    [Fact]
    public void For_EqualAmounts_AreOrderedByBookingId()
    {
        var renamed = SampleData.R2 with { Customer = "Finn" };
        ImmutableArray<BookingState> two =
        [
            Bookings[3],
            new BookingState.Confirmed(new PricedRental(renamed, 12000), "B-R2"),
        ];

        Assert.Equal(new[] { "B-R2", "B-R6" }, InvoiceOf(two, "Finn").Lines.Select(l => l.BookingId));
    }

    [Fact]
    public void For_DiscountFunc_ChangesTheRule()
    {
        var calls = new List<bool>();
        Func<bool, Func<long, long>> flatFive = isMember =>
        {
            calls.Add(isMember);
            return _ => 500;
        };

        var invoice = Assert.IsType<Result<Invoice>.Ok>(Invoicing.For(Bookings, "Ben", flatFive)).Value;

        Assert.Equal(new[] { false }, calls);
        Assert.Equal(11500, invoice.TotalCents);
        Assert.Equal(1150, Invoicing.MemberDiscount(11500));
        Assert.Equal(0, Invoicing.NoDiscount(11500));
    }

    [Fact]
    public void IsBillable_OnlyConfirmedAndReturned()
    {
        Assert.Equal(
            new[] { true, true, true, true, true, false },
            Bookings.Select(b => b.IsBillable()));
        Assert.False(new BookingState.Quoted(new PricedRental(SampleData.R5, 5000)).IsBillable());
    }

    [Fact]
    public void InvoiceWorkflow_SampleFile_RendersAnnasInvoice()
    {
        using var temp = new TempFolder();
        string bookings = temp.Write("bookings.csv", Rental.App.Io.BookingFile.Lines(Bookings).ToArray());

        var screen = InvoiceWorkflow.Run(bookings, "anna").Map(InvoiceScreen.Render);

        Assert.Equal(new[]
        {
            "Invoice: Anna (member)",
            "B-R1   BIKE-01  2 x  5 days     150.00",
            "B-R7   SKI-02   1 x  2 days      80.00",
            "Subtotal:            230.00",
            "Member discount:     -23.00",
            "Total:               207.00",
        }, Assert.IsType<Result<ImmutableArray<string>>.Ok>(screen).Value);
    }

    [Fact]
    public void InvoiceWorkflow_MissingFile_FailsWithFileMissing()
    {
        using var temp = new TempFolder();
        string missing = temp.PathOf("missing.csv");

        var fail = Assert.IsType<Result<Invoice>.Fail>(InvoiceWorkflow.Run(missing, "Anna"));

        Assert.Equal(ErrorCode.FileMissing, fail.Error.Code);
    }

    [Fact]
    public void Render_NonMember_ShowsNoMemberTagAndZeroDiscount()
    {
        var screen = InvoiceScreen.Render(InvoiceOf(Bookings, "Ben"));

        Assert.Equal(new[]
        {
            "Invoice: Ben",
            "B-R2   SKI-02   1 x  3 days     120.00",
            "Subtotal:            120.00",
            "Member discount:      -0.00",
            "Total:               120.00",
        }, screen);
    }
}
