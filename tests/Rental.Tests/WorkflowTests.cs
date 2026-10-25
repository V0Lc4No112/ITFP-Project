using Rental.Domain.Models;
using Rental.Domain.Pipeline;
using Rental.Domain.Policies;
using Rental.Domain.States;

namespace Rental.Tests;

public class WorkflowTests
{
    [Fact]
    public void NormalWorkflow_Standard_MatchesDocumentedResult()
    {
        var summary = RentalWorkflow.Run(SampleData.ValidatedAll, PricingPolicies.Standard, RentalWorkflow.DefaultMaxDays);

        Assert.Equal(new[] { "R3", "R1", "R2", "R6", "R5" }, summary.Quotes.Select(q => q.Request.Id));
        Assert.Equal(new[] { 35000L, 15000L, 12000L, 12000L, 5000L }, summary.Quotes.Select(q => q.TotalCents));
        Assert.Equal(79000, summary.GrandTotalCents);
        Assert.Equal(5, summary.Count);
    }

    [Fact]
    public void NormalWorkflow_Discount_MatchesDocumentedResult()
    {
        var summary = RentalWorkflow.Run(SampleData.ValidatedAll, PricingPolicies.LongRentalDiscount, RentalWorkflow.DefaultMaxDays);

        Assert.Equal(new[] { "R3", "R1", "R2", "R6", "R5" }, summary.Quotes.Select(q => q.Request.Id));
        Assert.Equal(new[] { 29750L, 12750L, 12000L, 10200L, 5000L }, summary.Quotes.Select(q => q.TotalCents));
        Assert.Equal(69700, summary.GrandTotalCents);
        Assert.Equal(5, summary.Count);
    }

    [Fact]
    public void EmptyDataset_ReturnsEmptySummary()
    {
        var summary = RentalWorkflow.Run(Array.Empty<ValidatedRental>(), PricingPolicies.Standard, RentalWorkflow.DefaultMaxDays);

        Assert.Empty(summary.Quotes);
        Assert.Equal(0, summary.GrandTotalCents);
        Assert.Equal(0, summary.Count);
    }
}
