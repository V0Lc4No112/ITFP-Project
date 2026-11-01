using System.Collections.Immutable;
using Rental.App;
using Rental.Domain.Models;
using Rental.Domain.Pipeline;
using Rental.Domain.Policies;
using Rental.Domain.Reports;

namespace Rental.Tests;

/// <summary>
/// Deferred execution (A8 Task 4): <see cref="Pricing.PriceEach"/> runs nothing until enumerated,
/// prices one request per step, and <see cref="Pricing.PriceAll"/> is the materialisation point.
/// The counting policy is a test probe; the real policies have no counter.
/// </summary>
public class DeferredTests
{
    private static readonly ImmutableArray<RentalRequest> Requests = [SampleData.R1, SampleData.R2, SampleData.R3];

    private sealed class CountingPolicy
    {
        public int Calls { get; private set; }

        public long Price(RentalRequest request)
        {
            Calls++;
            return PricingPolicies.Standard(request);
        }
    }

    [Fact]
    public void PriceEach_Created_RunsNothingUntilEnumerated()
    {
        var probe = new CountingPolicy();

        var quotes = Pricing.PriceEach(Requests, probe.Price);
        Assert.Equal(0, probe.Calls);

        Assert.Equal(15000L, quotes.First().TotalCents);
        Assert.Equal(1, probe.Calls);
    }

    [Fact]
    public void PriceEach_Iteration_PricesOneRequestPerStep()
    {
        var probe = new CountingPolicy();
        using var steps = Pricing.PriceEach(Requests, probe.Price).GetEnumerator();

        Assert.True(steps.MoveNext());
        Assert.Equal(1, probe.Calls);

        Assert.True(steps.MoveNext());
        Assert.Equal(2, probe.Calls);
        Assert.Equal(SampleData.R2, steps.Current.Request);
    }

    [Fact]
    public void PriceEach_RepeatedEnumeration_RepricesButSnapshotDoesNot()
    {
        var probe = new CountingPolicy();

        var lazy = Pricing.PriceEach(Requests, probe.Price);
        _ = lazy.Sum(quote => quote.TotalCents);
        _ = lazy.Count();
        Assert.Equal(6, probe.Calls);

        var snapshot = Pricing.PriceAll(Requests, probe.Price);
        Assert.Equal(9, probe.Calls);

        long sum = snapshot.Sum(quote => quote.TotalCents);
        Assert.Equal(3, snapshot.Length);
        Assert.Equal(9, probe.Calls);
        Assert.Equal(62000L, sum);
    }

    [Fact]
    public void ReportCsv_Revenue_HeaderThenOneLinePerCategoryInCents()
    {
        var lines = ReportCsv.Revenue([new CategoryRevenue(Category.Bike, 2, 3, 22950)]);
        Assert.Equal(new[] { ReportCsv.RevenueHeader, "Bike,2,3,22950" }, lines);

        Assert.Equal(new[] { "Category,Bookings,Items,RevenueCents" }, ReportCsv.Revenue([]));
    }
}
