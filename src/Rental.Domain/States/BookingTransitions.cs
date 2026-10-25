namespace Rental.Domain.States;

public static class BookingTransitions
{
    /// <summary>
    /// Quoted -> Confirmed when the stock has enough of the item; otherwise Rejected with a reason.
    /// Confirming a booking that is already Confirmed returns the same booking and the same stock,
    /// so stock is reserved only once. All checks run before Reserve; the inputs are never changed.
    /// </summary>
    public static ConfirmResult Confirm(BookingState state, Stock stock) =>
        state switch
        {
            BookingState.Quoted quoted => ConfirmQuoted(quoted, stock),
            BookingState.Confirmed confirmed => new ConfirmResult.Accepted(confirmed, stock),
            _ => throw new InvalidOperationException($"Unknown booking state: {state.GetType().Name}"),
        };

    private static ConfirmResult ConfirmQuoted(BookingState.Quoted quoted, Stock stock)
    {
        var request = quoted.Quote.Request;

        return stock.Available(request.ItemCode) switch
        {
            null => new ConfirmResult.Rejected($"{request.Id}: no stock record for {request.ItemCode}"),
            int available when available < request.Quantity => new ConfirmResult.Rejected(
                $"{request.Id}: not enough {request.ItemCode} (requested {request.Quantity}, available {available})"),
            _ => new ConfirmResult.Accepted(
                new BookingState.Confirmed(quoted.Quote, $"B-{request.Id}"),
                stock.Reserve(request.ItemCode, request.Quantity)),
        };
    }
}
