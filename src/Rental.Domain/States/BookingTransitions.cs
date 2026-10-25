using Rental.Domain.Models;
using Rental.Domain.Results;

namespace Rental.Domain.States;

public static class BookingTransitions
{
    public static Result<Reservation> Confirm(BookingState state, Stock stock) =>
        state switch
        {
            BookingState.Quoted quoted => ConfirmQuoted(quoted, stock),
            BookingState.Confirmed confirmed => Result.Ok(new Reservation(confirmed, stock)),
            _ => throw new InvalidOperationException($"Unknown booking state: {state.GetType().Name}"),
        };

    private static Result<Reservation> ConfirmQuoted(BookingState.Quoted quoted, Stock stock)
    {
        var request = quoted.Quote.Request;

        return stock.Available(request.ItemCode)
            .ToResult(() => new Error(
                ErrorCode.NoStockRecord,
                $"{request.Id}: no stock record for {request.ItemCode}"))
            .Bind(available => EnsureEnough(request, available))
            .Map(_ => new Reservation(
                new BookingState.Confirmed(quoted.Quote, $"B-{request.Id}"),
                stock.Reserve(request.ItemCode, request.Quantity)));
    }

    private static Result<int> EnsureEnough(RentalRequest request, int available) =>
        available >= request.Quantity
            ? Result.Ok(available)
            : Result.Fail<int>(new Error(
                ErrorCode.NotEnoughStock,
                $"{request.Id}: not enough {request.ItemCode} (requested {request.Quantity}, available {available})"));
}
