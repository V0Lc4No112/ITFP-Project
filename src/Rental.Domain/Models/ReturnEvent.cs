namespace Rental.Domain.Models;

/// <summary>One row of returns.csv: which booking came back, after how many days, with how many damaged items.</summary>
public sealed record ReturnEvent(string BookingId, int DaysUsed, int Damaged);
