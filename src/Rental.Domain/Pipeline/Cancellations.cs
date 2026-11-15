using System.Collections.Immutable;
using Rental.Domain.Extensions;
using Rental.Domain.Models;
using Rental.Domain.Results;
using Rental.Domain.States;

namespace Rental.Domain.Pipeline;

public sealed record CancelLine(CancelEvent Event, Result<CancelResult> Result);

public sealed record CancelReport(
    ImmutableArray<CancelLine> Lines,
    ImmutableArray<BookingState> Bookings,
    Stock Stock,
    long RefundCents);

public static class Cancellations
{
    /// <summary>
    /// W6 (Member D): applies every cancellation in file order. Each event is
    /// Find (Option → UnknownBooking) → Bind Cancel (Result). On Ok the booking is replaced by its
    /// Cancelled state and all its items go back to stock; on Fail nothing changes and the line is kept
    /// for the screen. The booking list is carried forward, so cancelling the same booking twice
    /// in one file is rejected the second time. A stock file without the booked item rejects the line
    /// (UnknownItem). The caller's bookings and stock are never changed.
    /// </summary>
    public static CancelReport ApplyAll(
        ImmutableArray<BookingState> bookings,
        Stock stock,
        IReadOnlyList<CancelEvent> events) =>
        events.Aggregate(
            new CancelReport([], bookings, stock, 0),
            (report, ev) => Apply(report, ev));

    private static CancelReport Apply(CancelReport report, CancelEvent ev)
    {
        var result = BookingStateExtensions.Find(report.Bookings, ev.BookingId)
            .ToResult(() => new Error(ErrorCode.UnknownBooking, $"{ev.BookingId}: no such booking"))
            .Bind(booking => Cancellation.Cancel(booking, ev)
                .Map(cancelled => (Old: booking, Cancelled: cancelled)))
            .Bind(found => EnsureStockRecord(report.Stock, found.Cancelled.Booking.Quote.Request)
                .Map(_ => found));

        return result switch
        {
            Result<(BookingState Old, CancelResult Cancelled)>.Ok ok => new CancelReport(
                report.Lines.Add(new CancelLine(ev, Result.Ok(ok.Value.Cancelled))),
                report.Bookings.Replace(ok.Value.Old, ok.Value.Cancelled.Booking),
                Release(report.Stock, ok.Value.Cancelled.Booking.Quote.Request),
                report.RefundCents + ok.Value.Cancelled.RefundCents),
            Result<(BookingState Old, CancelResult Cancelled)>.Fail fail => report with
            {
                Lines = report.Lines.Add(new CancelLine(ev, Result.Fail<CancelResult>(fail.Error))),
            },
            _ => throw new InvalidOperationException("Unknown result"),
        };
    }

    private static Stock Release(Stock stock, RentalRequest request) =>
        stock.Release(request.ItemCode, request.Quantity);

    // The items must go back somewhere and the stock must stay within its limit:
    // otherwise the cancellation is rejected and neither the booking nor the stock changes.
    private static Result<int> EnsureStockRecord(Stock stock, RentalRequest request) =>
        stock.CanAdd(request.ItemCode, request.Quantity);
}
