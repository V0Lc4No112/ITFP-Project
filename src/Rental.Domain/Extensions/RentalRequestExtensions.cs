
using Rental.Domain.Models;

namespace Rental.Domain.Extensions;

public static class RentalRequestExtensions
{
    public static long BaseCents(this RentalRequest request) =>
        request.DailyRateCents * request.Quantity * request.Days;
}
