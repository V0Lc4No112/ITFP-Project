using System.Collections.Immutable;
using Rental.Domain.Extensions;
using Rental.Domain.Models;
using Rental.Domain.Results;
using Rental.Domain.States;

namespace Rental.Domain.Pipeline;

public sealed record ReturnLine(ReturnEvent Event, Result<ReturnResult> Result);

public sealed record ReturnReport(
    ImmutableArray<ReturnLine> Lines,
    ImmutableArray<BookingState> Bookings,
    Stock Stock,
    long ExtraChargesCents);

public static class Returns
{
    public const long DamageFeeCents = 2000;

    /// <summary>Late days cost 1.5 × the daily rate per item; every damaged item costs 20.00.</summary>
    public static long Fee(RentalRequest request, int lateDays, int damaged) =>
        (lateDays, damaged) switch
        {
            (0, 0) => 0,
            (> 0, 0) => LateFee(request, lateDays),
            (0, > 0) => damaged * DamageFeeCents,
            _ => LateFee(request, lateDays) + damaged * DamageFeeCents,
        };

    private static long LateFee(RentalRequest request, int lateDays) =>
        lateDays * request.DailyRateCents * request.Quantity * 150 / 100;

    /// <summary>
    /// W5 (Member C): applies every return in file order. Each event is Find (Option → UnknownBooking)
    /// → Bind Return (Result) → Bind the stock record check. On Ok the booking becomes Returned and the
    /// undamaged items go back to stock (damaged ones go to repair); on Fail nothing changes and the line
    /// is kept. The booking list is carried forward, so a second return of the same booking is rejected.
    /// The caller's bookings and stock are never changed.
    /// </summary>
    public static ReturnReport ApplyAll(
        ImmutableArray<BookingState> bookings,
        Stock stock,
        IReadOnlyList<ReturnEvent> events) =>
        events.Aggregate(
            new ReturnReport([], bookings, stock, 0),
            (report, ev) => Apply(report, ev));

    private static ReturnReport Apply(ReturnReport report, ReturnEvent ev)
    {
        var result = BookingStateExtensions.Find(report.Bookings, ev.BookingId)
            .ToResult(() => new Error(ErrorCode.UnknownBooking, $"{ev.BookingId}: no such booking"))
            .Bind(booking => BookingTransitions.Return(booking, ev)
                .Map(returned => (Old: booking, Returned: returned)))
            .Bind(found => report.Stock.Available(found.Returned.Booking.Quote.Request.ItemCode)
                .ToResult(() => new Error(
                    ErrorCode.UnknownItem,
                    $"{found.Returned.Booking.Quote.Request.ItemCode}: not in the stock list, cannot put the items back"))
                .Map(_ => found));

        return result switch
        {
            Result<(BookingState Old, ReturnResult Returned)>.Ok ok => new ReturnReport(
                report.Lines.Add(new ReturnLine(ev, Result.Ok(ok.Value.Returned))),
                report.Bookings.Replace(ok.Value.Old, ok.Value.Returned.Booking),
                report.Stock.Release(
                    ok.Value.Returned.Booking.Quote.Request.ItemCode,
                    ok.Value.Returned.Booking.Quote.Request.Quantity - ok.Value.Returned.Damaged),
                report.ExtraChargesCents + ok.Value.Returned.FeeCents),
            Result<(BookingState Old, ReturnResult Returned)>.Fail fail => report with
            {
                Lines = report.Lines.Add(new ReturnLine(ev, Result.Fail<ReturnResult>(fail.Error))),
            },
            _ => throw new InvalidOperationException("Unknown result"),
        };
    }
}
