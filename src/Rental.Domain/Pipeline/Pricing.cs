using System.Collections.Immutable;
using Rental.Domain.Models;

namespace Rental.Domain.Pipeline;

public static class Pricing
{
    /// <summary>
    /// Stage 2 (Member C): the higher-order function. Calls <paramref name="policy"/>
    /// once per request and returns the priced rentals.
    /// </summary>
    public static ImmutableArray<PricedRental> PriceAll(ImmutableArray<RentalRequest> eligible, Func<RentalRequest, long> policy) =>
        throw new NotImplementedException();
}
