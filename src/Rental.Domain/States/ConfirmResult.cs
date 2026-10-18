namespace Rental.Domain.States;

public abstract record ConfirmResult
{
    private ConfirmResult() { }

    public sealed record Accepted(BookingState.Confirmed Booking, Stock Stock) : ConfirmResult;

    public sealed record Rejected(string Reason) : ConfirmResult;
}
