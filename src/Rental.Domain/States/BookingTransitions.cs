using Rental.Domain.Models;
using Rental.Domain.Results;

namespace Rental.Domain.States;

/// <summary>The result of one successful return: the new state, the late days, damaged items and the extra fee.</summary>
public sealed record ReturnResult(BookingState.Returned Booking, int LateDays, int Damaged, long FeeCents);

public static class BookingTransitions
{
    public static Result<Reservation> Confirm(
        BookingState state,
        Stock stock) =>
        state switch
        {
            BookingState.Quoted quoted =>
                ConfirmQuoted(quoted, stock),

            BookingState.Confirmed confirmed =>
                Result.Ok(new Reservation(confirmed, stock)),

            BookingState.Returned returned =>
                Result.Fail<Reservation>(
                    new Error(
                        ErrorCode.InvalidTransition,
                        $"{returned.BookingId}: cannot confirm a returned booking")),

            BookingState.Cancelled cancelled =>
                Result.Fail<Reservation>(
                    new Error(
                        ErrorCode.InvalidTransition,
                        $"{cancelled.BookingId}: cannot confirm a cancelled booking")),

            _ => throw new InvalidOperationException(
                $"Unknown booking state: {state.GetType().Name}"),
        };

    private static Result<Reservation> ConfirmQuoted(
        BookingState.Quoted quoted,
        Stock stock)
    {
        var request = quoted.Quote.Request;

        // A Quoted state can be built by hand, so the transition re-checks the same rules as
        // ValidatedRental.Create (e.g. Quantity -2 would otherwise ADD stock).
        return Valid(request)
            .Bind(_ => stock.Available(request.ItemCode)
            .ToResult(() => new Error(
                ErrorCode.NoStockRecord,
                $"{request.Id}: no stock record for {request.ItemCode}"))
            .Bind(available => EnsureEnough(request, available)))
            .Map(_ => new Reservation(
                new BookingState.Confirmed(
                    quoted.Quote,
                    $"B-{request.Id}"),
                stock.Reserve(
                    request.ItemCode,
                    request.Quantity)));
    }

    private static Result<RentalRequest> Valid(RentalRequest request) =>
        ValidatedRental.Create(request) switch
        {
            Validation<ValidatedRental>.Valid ok => Result.Ok(ok.Value.Request),
            Validation<ValidatedRental>.Invalid bad => Result.Fail<RentalRequest>(new Error(
                ErrorCode.InvalidRequest,
                $"{request.Id}: request is not valid",
                bad.Reasons)),
            _ => throw new InvalidOperationException("Unexpected validation result"),
        };

    /// <summary>An event must name the booking it is applied to (shared by Return and Cancel).</summary>
    internal static Result<string> SameBooking(string bookingId, string eventBookingId) =>
        string.Equals(bookingId, eventBookingId, StringComparison.Ordinal)
            ? Result.Ok(bookingId)
            : Result.Fail<string>(new Error(
                ErrorCode.InvalidEvent,
                $"{bookingId}: event is for booking '{eventBookingId}', not this one"));

    private static Result<int> EnsureEnough(
        RentalRequest request,
        int available) =>
        available >= request.Quantity
            ? Result.Ok(available)
            : Result.Fail<int>(
                new Error(
                    ErrorCode.NotEnoughStock,
                    $"{request.Id}: not enough {request.ItemCode} (requested {request.Quantity}, available {available})"));

    /// <summary>
    /// W5 (Member C): only a <see cref="BookingState.Confirmed"/> booking can be returned.
    /// Returning it twice, or returning a cancelled booking, is an invalid sequence and is rejected.
    /// The fee comes from <c>Returns.Fee</c>; stock is changed by the caller, not here.
    /// </summary>
    public static Result<ReturnResult> Return(BookingState state, ReturnEvent ev) =>
        state switch
        {
            BookingState.Confirmed confirmed => SameBooking(confirmed.BookingId, ev.BookingId)
                .Bind(_ => CheckEvent(confirmed, ev))
                .Map(_ => new ReturnResult(
                    new BookingState.Returned(confirmed.Quote, confirmed.BookingId),
                    Math.Max(0, ev.DaysUsed - confirmed.Quote.Request.Days),
                    ev.Damaged,
                    Pipeline.Returns.Fee(
                        confirmed.Quote.Request,
                        Math.Max(0, ev.DaysUsed - confirmed.Quote.Request.Days),
                        ev.Damaged))),
            BookingState.Returned returned => Result.Fail<ReturnResult>(new Error(
                ErrorCode.InvalidTransition,
                $"{returned.BookingId}: already returned")),
            BookingState.Cancelled cancelled => Result.Fail<ReturnResult>(new Error(
                ErrorCode.InvalidTransition,
                $"{cancelled.BookingId}: was cancelled, nothing to return")),
            BookingState.Quoted quoted => Result.Fail<ReturnResult>(new Error(
                ErrorCode.InvalidTransition,
                $"{quoted.Quote.Request.Id}: only a confirmed booking can be returned")),
            _ => throw new InvalidOperationException($"Unknown booking state: {state.GetType().Name}"),
        };

    private static Result<ReturnEvent> CheckEvent(BookingState.Confirmed confirmed, ReturnEvent ev) =>
        ev.DaysUsed >= 1 && ev.Damaged >= 0 && ev.Damaged <= confirmed.Quote.Request.Quantity
            ? Result.Ok(ev)
            : Result.Fail<ReturnEvent>(new Error(
                ErrorCode.InvalidEvent,
                $"{confirmed.BookingId}: days used must be >= 1 and damaged between 0 and {confirmed.Quote.Request.Quantity} (was {ev.DaysUsed} days, {ev.Damaged} damaged)"));
}
