using System.Collections.Immutable;

namespace Rental.Domain.Models;

/// <remarks>
/// ImmutableArray uses reference equality inside a record, so compare
/// <see cref="Quotes"/> with SequenceEqual in tests.
/// </remarks>
public sealed record QuoteSummary(
    ImmutableArray<PricedRental> Quotes,
    long GrandTotalCents,
    int Count);
