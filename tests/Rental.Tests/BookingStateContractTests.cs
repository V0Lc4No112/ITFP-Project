using Rental.Domain.Extensions;
using Rental.Domain.Models;
using Rental.Domain.Results;
using Rental.Domain.States;
using Xunit;

namespace Rental.Tests;

public class BookingStateContractTests
{
    private static PricedRental CreateQuote() =>
        new(SampleData.R1, 15000);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Confirm_ReturnedOrCancelled_FailsWithInvalidTransition(
        bool cancelled)
    {
        var quote = CreateQuote();

        BookingState state = cancelled
            ? new BookingState.Cancelled(quote, "B-R1")
            : new BookingState.Returned(quote, "B-R1");

        var stock = Assert.IsType<Validation<Stock>.Valid>(
            Stock.Create([("BIKE-01", 3)])).Value;

        var result = BookingTransitions.Confirm(state, stock);

        var failure =
            Assert.IsType<Result<Reservation>.Fail>(result);

        Assert.Equal(
            ErrorCode.InvalidTransition,
            failure.Error.Code);

        Assert.Contains("B-R1", failure.Error.Message);

        Assert.Equal(
            Option.Some(3),
            stock.Available("BIKE-01"));
    }

    [Fact]
    public void QuoteOf_AllStates_ReturnsOriginalQuote()
    {
        var quote = CreateQuote();

        BookingState[] states =
        [
            new BookingState.Quoted(quote),
            new BookingState.Confirmed(quote, "B-R1"),
            new BookingState.Returned(quote, "B-R1"),
            new BookingState.Cancelled(quote, "B-R1"),
        ];

        foreach (var state in states)
        {
            Assert.Same(quote, state.QuoteOf());
        }
    }

    [Fact]
    public void IdOf_QuotedIsEmpty_OtherStatesReturnBookingId()
    {
        var quote = CreateQuote();

        BookingState[] states =
        [
            new BookingState.Quoted(quote),
            new BookingState.Confirmed(quote, "B-R1"),
            new BookingState.Returned(quote, "B-R2"),
            new BookingState.Cancelled(quote, "B-R3"),
        ];

        Assert.Equal(
            new[] { "", "B-R1", "B-R2", "B-R3" },
            states.Select(state => state.IdOf()));
    }

    [Fact]
    public void Find_Present_ReturnsSomeForEachSavedState()
    {
        var quote = CreateQuote();

        IReadOnlyList<BookingState> bookings =
            new BookingState[]
            {
                new BookingState.Confirmed(quote, "B-R1"),
                new BookingState.Returned(quote, "B-R2"),
                new BookingState.Cancelled(quote, "B-R3"),
            };

        foreach (var booking in bookings)
        {
            var result = bookings.Find(booking.IdOf());

            var found =
                Assert.IsType<Option<BookingState>.Some>(result);

            Assert.Same(booking, found.Value);
        }
    }

    [Theory]
    [InlineData("B-UNKNOWN")]
    [InlineData("b-r1")]
    [InlineData("")]
    [InlineData(" ")]
    public void Find_AbsentOrInvalidId_ReturnsNone(
        string bookingId)
    {
        var quote = CreateQuote();

        IReadOnlyList<BookingState> bookings =
            new BookingState[]
            {
                new BookingState.Quoted(quote),
                new BookingState.Confirmed(quote, "B-R1"),
            };

        var result = bookings.Find(bookingId);

        Assert.IsType<Option<BookingState>.None>(result);
    }

    [Fact]
    public void Find_EmptyCollection_ReturnsNone()
    {
        IReadOnlyList<BookingState> bookings =
            Array.Empty<BookingState>();

        var result = bookings.Find("B-R1");

        Assert.IsType<Option<BookingState>.None>(result);
    }
}