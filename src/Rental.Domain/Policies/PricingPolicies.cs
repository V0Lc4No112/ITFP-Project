using Rental.Domain.Models;
using Rental.Domain.Results;

namespace Rental.Domain.Policies;

public static class PricingPolicies
{
    public static readonly Func<RentalRequest, long> Standard =
        static request => request.DailyRateCents * request.Quantity * request.Days;

    public static readonly Func<RentalRequest, long> LongRentalDiscount =
        static request =>
        {
            long basePrice = Standard(request);
            return request.Days >= 5 ? basePrice * 85 / 100 : basePrice;
        };

    public static Option<Func<RentalRequest, long>> FromName(string name) =>
        name.ToLowerInvariant() switch
        {
            "standard" => Option.Some(Standard),
            "discount" => Option.Some(LongRentalDiscount),
            _ => Option.None<Func<RentalRequest, long>>(),
        };
}
