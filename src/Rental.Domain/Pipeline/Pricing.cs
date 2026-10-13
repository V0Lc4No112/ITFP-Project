using System;
using System.Collections.Immutable;
using Rental.Domain.Models;

namespace Rental.Domain.Pipeline;

public static class Pricing
{
    public static ImmutableArray<PricedRental> PriceAll(
        ImmutableArray<RentalRequest> eligible,
        Func<RentalRequest, long> policy)
    {
        var result = ImmutableArray.CreateBuilder<PricedRental>(eligible.Length);

        foreach (var request in eligible)
            result.Add(new PricedRental(request, policy(request)));

        return result.MoveToImmutable();
    }
}