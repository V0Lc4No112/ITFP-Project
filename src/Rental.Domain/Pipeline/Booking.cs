using System.Collections.Immutable;
using Rental.Domain.Models;
using Rental.Domain.Results;
using Rental.Domain.States;

namespace Rental.Domain.Pipeline;

public sealed record BookingLine(PricedRental Quote, Result<Reservation> Result);

public sealed record BookingReport(
    ImmutableArray<BookingLine> Lines,
    Stock RemainingStock,
    long ConfirmedTotalCents);

public static class Booking
{
    /// <summary>
    /// Confirms every quote in summary order (Member D). <c>Ok</c> passes its new stock on to the next quote
    /// and adds the quote total; <c>Fail</c> (NoStockRecord, NotEnoughStock) changes nothing and stays a line
    /// in the report. A rejected quote is not a failure of the whole run, so this returns a plain report,
    /// not a <see cref="Result{T}"/>. The stock passed in is never changed.
    /// </summary>
    public static BookingReport ConfirmAll(QuoteSummary summary, Stock stock)
    {
        var lines = ImmutableArray.CreateBuilder<BookingLine>(summary.Quotes.Length);
        var current = stock;
        long confirmedTotal = 0;

        foreach (var quote in summary.Quotes)
        {
            var result = BookingTransitions.Confirm(new BookingState.Quoted(quote), current);
            switch (result)
            {
                case Result<Reservation>.Ok ok:
                    current = ok.Value.Stock;
                    confirmedTotal += quote.TotalCents;
                    break;
                case Result<Reservation>.Fail:
                    break;
                default:
                    throw new InvalidOperationException($"Unknown confirm result: {result.GetType().Name}");
            }

            lines.Add(new BookingLine(quote, result));
        }

        return new BookingReport(lines.MoveToImmutable(), current, confirmedTotal);
    }
}
