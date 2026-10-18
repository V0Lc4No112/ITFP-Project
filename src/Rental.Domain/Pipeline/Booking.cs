using System.Collections.Immutable;
using Rental.Domain.Models;
using Rental.Domain.States;

namespace Rental.Domain.Pipeline;

/// <summary>One quote and what happened when it was confirmed.</summary>
public sealed record BookingLine(PricedRental Quote, ConfirmResult Result);

/// <remarks>
/// ImmutableArray uses reference equality inside a record, so compare
/// <see cref="Lines"/> with SequenceEqual in tests.
/// </remarks>
public sealed record BookingReport(
    ImmutableArray<BookingLine> Lines,
    Stock RemainingStock,
    long ConfirmedTotalCents);

public static class Booking
{
    /// <summary>
    /// Confirms every quote in summary order (total descending, then Id) against the stock.
    /// Each accepted booking passes its new stock to the next quote; a rejection changes nothing.
    /// The stock passed in is never changed. Pure: no Console, no files.
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
                case ConfirmResult.Accepted accepted:
                    current = accepted.Stock;
                    confirmedTotal += quote.TotalCents;
                    break;
                case ConfirmResult.Rejected:
                    break;
                default:
                    throw new InvalidOperationException($"Unknown confirm result: {result.GetType().Name}");
            }

            lines.Add(new BookingLine(quote, result));
        }

        return new BookingReport(lines.MoveToImmutable(), current, confirmedTotal);
    }
}
