namespace Rental.Domain.Models;

public sealed record PricedRental(RentalRequest Request, long TotalCents);
