namespace Rental.Domain.Models;

/// <summary>A validated rental request. Money is in integer cents.</summary>
public sealed record RentalRequest(
    string Id,
    string Customer,
    string ItemCode,
    Category Category,
    long DailyRateCents,
    int Quantity,
    int Days,
    bool IsMember);
