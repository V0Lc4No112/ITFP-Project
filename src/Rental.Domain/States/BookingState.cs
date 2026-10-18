using Rental.Domain.Models;

namespace Rental.Domain.States;

public abstract record BookingState
{
    private BookingState() { }

    public sealed record Quoted(PricedRental Quote) : BookingState;

    public sealed record Confirmed(PricedRental Quote, string BookingId) : BookingState;
}
