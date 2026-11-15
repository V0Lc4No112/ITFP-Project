using Rental.Domain.Models;
using Rental.Domain.Results;

namespace Rental.Domain.States;

/// <summary>The result of one successful cancel: the new state, the refund tier and the money given back.</summary>
public sealed record CancelResult(BookingState.Cancelled Booking, int RefundPercent, long RefundCents);

/// <summary>
/// W6 (Member D): the Confirmed → Cancelled transition. Pure: no stock change here (see
/// <c>Cancellations.ApplyAll</c>), no Console, no files.
/// </summary>
public static class Cancellation
{
    /// <summary>
    /// Refund tier, decided with relational patterns: 7+ days before the start → 100%,
    /// 2 to 6 days → 50%, 0 or 1 day → 0%. A negative number is not a real cancellation request.
    /// </summary>
    public static Result<int> RefundPercent(string bookingId, int daysBeforeStart) =>
        daysBeforeStart switch
        {
            < 0 => Result.Fail<int>(new Error(
                ErrorCode.InvalidEvent,
                $"{bookingId}: days before start must be >= 0 (was {daysBeforeStart})")),
            >= 7 => Result.Ok(100),
            >= 2 => Result.Ok(50),
            _ => Result.Ok(0),
        };

    /// <summary>
    /// total × percent / 100 without the intermediate product: (total / 100) × percent plus the
    /// remainder part. Same result for every total >= 0, and it cannot overflow for percent <= 100.
    /// </summary>
    public static long Refund(long totalCents, int percent) =>
        totalCents / 100 * percent + totalCents % 100 * percent / 100;

    /// <summary>
    /// Only a <see cref="BookingState.Confirmed"/> booking can be cancelled. A returned or an already
    /// cancelled booking is an invalid sequence and is rejected, not ignored.
    /// </summary>
    public static Result<CancelResult> Cancel(BookingState state, CancelEvent ev) =>
        state switch
        {
            BookingState.Confirmed confirmed =>
                BookingTransitions.SameBooking(confirmed.BookingId, ev.BookingId)
                    .Bind(_ => BookingTransitions.ValidQuote(confirmed.Quote))
                    .Bind(_ => RefundPercent(confirmed.BookingId, ev.DaysBeforeStart))
                    .Map(percent => new CancelResult(
                        new BookingState.Cancelled(confirmed.Quote, confirmed.BookingId),
                        percent,
                        Refund(confirmed.Quote.TotalCents, percent))),
            BookingState.Returned returned => Result.Fail<CancelResult>(new Error(
                ErrorCode.InvalidTransition,
                $"{returned.BookingId}: already returned, cannot cancel")),
            BookingState.Cancelled cancelled => Result.Fail<CancelResult>(new Error(
                ErrorCode.InvalidTransition,
                $"{cancelled.BookingId}: already cancelled")),
            BookingState.Quoted quoted => Result.Fail<CancelResult>(new Error(
                ErrorCode.InvalidTransition,
                $"{quoted.Quote.Request.Id}: only a confirmed booking can be cancelled")),
            _ => throw new InvalidOperationException($"Unknown booking state: {state.GetType().Name}"),
        };
}
