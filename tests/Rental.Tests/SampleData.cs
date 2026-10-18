using System.Collections.Immutable;
using Rental.Domain.Models;

namespace Rental.Tests;

/// <summary>
/// The R1 to R6 rows of data/requests.csv as records, shared by every test file.
/// Expected totals are in docs/contracts.md ("Dataset and expected results").
/// </summary>
public static class SampleData
{
    public static readonly RentalRequest R1 = new("R1", "Anna", "BIKE-01", Category.Bike, 1500, 2, 5, true);
    public static readonly RentalRequest R2 = new("R2", "Ben", "SKI-02", Category.Ski, 4000, 1, 3, false);
    public static readonly RentalRequest R3 = new("R3", "Cara", "TENT-01", Category.Camping, 2500, 1, 14, false);
    public static readonly RentalRequest R4 = new("R4", "Dan", "BIKE-01", Category.Bike, 1500, 1, 15, true);
    public static readonly RentalRequest R5 = new("R5", "Eve", "TENT-01", Category.Camping, 2500, 2, 1, false);
    public static readonly RentalRequest R6 = new("R6", "Finn", "BIKE-01", Category.Bike, 1500, 1, 8, false);

    /// <summary>All six rows in file order (R4 is the one Stage 1 excludes).</summary>
    public static readonly ImmutableArray<RentalRequest> All = [R1, R2, R3, R4, R5, R6];
}
