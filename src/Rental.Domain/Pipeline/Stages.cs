using System.Collections.Immutable;
using Rental.Domain.Models;

namespace Rental.Domain.Pipeline;

public static class Stages
{
    public static ImmutableArray<RentalRequest> SelectEligible(IReadOnlyList<RentalRequest> requests, int maxDays) =>
        requests
            .Where(r => r.Days <= maxDays)
            .Select(r => r with { Customer = r.Customer.Trim() })
            .ToImmutableArray();

    public static QuoteSummary BuildSummary(ImmutableArray<PricedRental> priced) =>
        new(
            priced
                .OrderByDescending(p => p.TotalCents)
                .ThenBy(p => p.Request.Id, StringComparer.Ordinal)
                .ToImmutableArray(),
            priced.Sum(p => p.TotalCents),
            priced.Length);
}
