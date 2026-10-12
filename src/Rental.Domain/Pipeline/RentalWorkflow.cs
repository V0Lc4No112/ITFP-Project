using Rental.Domain.Models;

namespace Rental.Domain.Pipeline;

public static class RentalWorkflow
{
    public const int DefaultMaxDays = 14;

    /// <summary>
    /// Composition (Member D): SelectEligible, then PriceAll, then BuildSummary.
    /// </summary>
    public static QuoteSummary Run(IReadOnlyList<RentalRequest> requests, Func<RentalRequest, long> policy, int maxDays) =>
        throw new NotImplementedException();
}
