using Rental.Domain.Models;
using Rental.Domain.Results;
using Rental.Domain.States;

namespace Rental.Domain.Extensions;

public static class BookingStateExtensions
{
    public static PricedRental QuoteOf(this BookingState state) =>
        state switch
        {
            BookingState.Quoted quoted => quoted.Quote,
            BookingState.Confirmed confirmed => confirmed.Quote,
            BookingState.Returned returned => returned.Quote,
            BookingState.Cancelled cancelled => cancelled.Quote,

            _ => throw new InvalidOperationException(
                "Unknown booking state"),
        };

    public static string IdOf(this BookingState state) =>
        state switch
        {
            BookingState.Quoted => "",
            BookingState.Confirmed confirmed => confirmed.BookingId,
            BookingState.Returned returned => returned.BookingId,
            BookingState.Cancelled cancelled => cancelled.BookingId,

            _ => throw new InvalidOperationException(
                "Unknown booking state"),
        };

    public static Option<BookingState> Find(
        this IReadOnlyList<BookingState> bookings,
        string bookingId)
    {
        if (string.IsNullOrWhiteSpace(bookingId))
        {
            return Option.None<BookingState>();
        }

        foreach (var booking in bookings)
        {
            if (booking is BookingState.Quoted)
            {
                continue;
            }

            if (string.Equals(
                booking.IdOf(),
                bookingId,
                StringComparison.Ordinal))
            {
                return Option.Some<BookingState>(booking);
            }
        }

        return Option.None<BookingState>();
    }
}