using Rental.Domain.Models;

namespace Rental.Domain.Policies;

/// <summary>
/// Member C: the two pricing policies. Selected in Program and passed as an
/// argument; there is no mutable "current policy" field.
/// </summary>
public static class PricingPolicies
{
    /// <summary>DailyRateCents × Quantity × Days.</summary>
    public static readonly Func<RentalRequest, long> Standard =
        _ => throw new NotImplementedException();

    /// <summary>Standard, then 15% off (rounded down) when Days &gt;= 5.</summary>
    public static readonly Func<RentalRequest, long> LongRentalDiscount =
        _ => throw new NotImplementedException();

    /// <summary>"standard" or "discount"; null for an unknown name.</summary>
    public static Func<RentalRequest, long>? FromName(string name) =>
        throw new NotImplementedException();
}
