namespace Rental.Domain.Models;

/// <summary>One row of cancellations.csv: which booking, and how many days before the rental starts.</summary>
public sealed record CancelEvent(string BookingId, int DaysBeforeStart);
