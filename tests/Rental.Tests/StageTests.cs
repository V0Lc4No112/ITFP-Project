using System.Collections.Immutable;
using Rental.Domain.Models;
using Rental.Domain.Pipeline;

namespace Rental.Tests;

public class StageTests
{
    private static readonly RentalRequest R1 = new("R1", "Anna", "BIKE-01", Category.Bike, 1500, 2, 5, true);
    private static readonly RentalRequest R2 = new("R2", "Ben", "SKI-02", Category.Ski, 4000, 1, 3, false);
    private static readonly RentalRequest R3 = new("R3", "Cara", "TENT-01", Category.Camping, 2500, 1, 14, false);
    private static readonly RentalRequest R4 = new("R4", "Dan", "BIKE-01", Category.Bike, 1500, 1, 15, true);
    private static readonly RentalRequest R5 = new("R5", "Eve", "TENT-01", Category.Camping, 2500, 2, 1, false);
    private static readonly RentalRequest R6 = new("R6", "Finn", "BIKE-01", Category.Bike, 1500, 1, 8, false);

    [Fact]
    public void SelectEligible_ExactlyMaxDays_IsIncluded_OneMore_IsExcluded()
    {
        var eligible = Stages.SelectEligible([R3, R4], 14);

        Assert.Equal(new[] { "R3" }, eligible.Select(r => r.Id));
    }

    [Fact]
    public void SelectEligible_DoesNotModifyCallerList()
    {
        var untrimmed = R1 with { Customer = "  Anna " };
        var requests = new List<RentalRequest> { untrimmed, R4 };
        var before = requests.ToList();

        Stages.SelectEligible(requests, 14);

        Assert.Equal(before, requests);
        Assert.Equal("  Anna ", requests[0].Customer);
    }

    [Fact]
    public void SelectEligible_TrimsCustomer()
    {
        var eligible = Stages.SelectEligible([R1 with { Customer = "  Anna " }], 14);

        Assert.Equal("Anna", eligible.Single().Customer);
    }

    [Fact]
    public void BuildSummary_TiedTotals_OrderedById()
    {
        ImmutableArray<PricedRental> priced =
        [
            new(R6, 12000),
            new(R5, 5000),
            new(R2, 12000),
            new(R1, 15000),
            new(R3, 35000),
        ];

        var summary = Stages.BuildSummary(priced);

        Assert.Equal(new[] { "R3", "R1", "R2", "R6", "R5" }, summary.Quotes.Select(q => q.Request.Id));
        Assert.Equal(79000, summary.GrandTotalCents);
        Assert.Equal(5, summary.Count);
    }
}
