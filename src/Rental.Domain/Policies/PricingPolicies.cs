
using Rental.Domain.Extensions;
using Rental.Domain.Models;
using Rental.Domain.Results;

namespace Rental.Domain.Policies;

public static class PricingPolicies
{
    public static readonly Func<RentalRequest, long> Standard =
        static request => request.BaseCents();

    public static readonly Func<int, Func<int, Func<RentalRequest, long>>> DaysDiscount =
        minDays => percentOff =>
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(minDays, 1);
            ArgumentOutOfRangeException.ThrowIfNegative(percentOff);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(percentOff, 100);

            return request =>
                request.Days >= minDays
                    ? request.BaseCents() * (100 - percentOff) / 100
                    : request.BaseCents();
        };

    public static readonly Func<RentalRequest, long> LongRentalDiscount =
        DaysDiscount(5)(15);

    public static readonly Func<RentalRequest, long> WeeklyDiscount =
        DaysDiscount(7)(25);

    public static Option<Func<RentalRequest, long>> FromName(string name) =>
        name.ToLowerInvariant() switch
        {
            "standard" => Option.Some(Standard),
            "discount" => Option.Some(LongRentalDiscount),
            "weekly" => Option.Some(WeeklyDiscount),
            _ => Option.None<Func<RentalRequest, long>>(),
        };
}
