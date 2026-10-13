using System;
using Rental.Domain.Models;

namespace Rental.Domain.Policies;

public static class PricingPolicies
{
    public static readonly Func<RentalRequest, long> Standard =
        static request =>
            request.DailyRateCents * request.Quantity * request.Days;

    public static readonly Func<RentalRequest, long> LongRentalDiscount =
        static request =>
        {
            long basePrice = Standard(request);

            return request.Days >= 5
                ? basePrice * 85 / 100
                : basePrice;
        };

    public static Func<RentalRequest, long>? FromName(string name)
    {
        if (string.Equals(name, "standard", StringComparison.OrdinalIgnoreCase))
            return Standard;

        if (string.Equals(name, "discount", StringComparison.OrdinalIgnoreCase))
            return LongRentalDiscount;

        return null;
    }
}