namespace Rental.Domain.States;

public sealed record Reservation(BookingState.Confirmed Booking, Stock Stock);
