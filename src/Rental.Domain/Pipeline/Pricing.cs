using System.Collections.Immutable;
using Rental.Domain.Models;

namespace Rental.Domain.Pipeline;

public static class Pricing
{
    /// <summary>
    /// Stage 2 (A5), now over a lazy core. <c>ToImmutableArray</c> is the deliberate materialisation point:
    /// <c>BuildSummary</c> reads the quotes three times (OrderBy, Sum, Length), and the snapshot makes sure
    /// each request is priced exactly once.
    /// </summary>
    public static ImmutableArray<PricedRental> PriceAll(
        ImmutableArray<RentalRequest> eligible,
        Func<RentalRequest, long> policy) =>
        PriceEach(eligible, policy).ToImmutableArray();

    /// <summary>
    /// Deferred pricing: calling this runs nothing. Each <c>MoveNext</c> prices one request with the policy
    /// and yields it. Enumerating twice prices everything twice (pure, so same values, but repeated work).
    /// </summary>
    public static IEnumerable<PricedRental> PriceEach(
        IEnumerable<RentalRequest> requests,
        Func<RentalRequest, long> policy)
    {
        foreach (var request in requests)
        {
            yield return new PricedRental(request, policy(request));
        }
    }
}
