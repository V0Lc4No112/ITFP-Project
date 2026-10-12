using System.Collections.Immutable;
using Rental.Domain.Models;

namespace Rental.Domain.Pipeline;

public static class Stages
{
    /// <summary>
    /// Stage 1 (Member B): keeps requests with Days &lt;= maxDays and trims Customer.
    /// Returns a new array; never modifies the caller's list.
    /// </summary>
    public static ImmutableArray<RentalRequest> SelectEligible(IReadOnlyList<RentalRequest> requests, int maxDays) =>
        throw new NotImplementedException();

    /// <summary>
    /// Stage 3 (Member B): orders by TotalCents descending, then Id ascending,
    /// and calculates the grand total and count.
    /// </summary>
    public static QuoteSummary BuildSummary(ImmutableArray<PricedRental> priced) =>
        throw new NotImplementedException();
}
