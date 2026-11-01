
using Rental.Domain.Extensions;
using Rental.Domain.Models;
using Rental.Domain.Pipeline;
using Rental.Domain.Policies;
using Xunit;

namespace Rental.Tests;

public class CurryTests
{
    private static RentalRequest Days(int days) =>
        new("T1", "Anna", "BIKE-01", Category.Bike, 1000, 1, days, false);

    private static readonly RentalRequest R1 =
        new("R1", "Anna", "BIKE-01", Category.Bike, 1500, 2, 5, true);

    private static readonly RentalRequest R2 =
        new("R2", "Ben", "SKI-02", Category.Ski, 4000, 1, 3, false);

    private static readonly RentalRequest R3 =
        new("R3", "Cara", "TENT-01", Category.Camping, 2500, 1, 14, false);

    [Fact]
    public void BaseCents_IsRateTimesQuantityTimesDays()
    {
        Assert.Equal(15000L, R1.BaseCents());
        Assert.Equal(35000L, R3.BaseCents());
        Assert.Equal(R3.BaseCents(), PricingPolicies.Standard(R3));
    }

    [Fact]
    public void DaysDiscount_ArgumentsArriveOneCallAtATime()
    {
        Func<int, Func<RentalRequest, long>> fromFiveDays =
            PricingPolicies.DaysDiscount(5);

        Func<RentalRequest, long> tenOff = fromFiveDays(10);

        Assert.Equal(9000L, tenOff(Days(10)));
        Assert.Equal(4000L, tenOff(Days(4)));
    }

    [Fact]
    public void ConfiguredPolicies_SameRequests_DifferentPrices()
    {
        Assert.Equal(12750L, PricingPolicies.LongRentalDiscount(R1));
        Assert.Equal(15000L, PricingPolicies.WeeklyDiscount(R1));

        Assert.Equal(29750L, PricingPolicies.LongRentalDiscount(R3));
        Assert.Equal(26250L, PricingPolicies.WeeklyDiscount(R3));

        Assert.Equal(12000L, PricingPolicies.LongRentalDiscount(R2));
        Assert.Equal(12000L, PricingPolicies.WeeklyDiscount(R2));
    }

    [Fact]
    public void ConfiguredPolicies_SampleDataset_DifferentGrandTotals()
    {
        var discount = RentalWorkflow.Run(
            SampleData.ValidatedAll,
            PricingPolicies.LongRentalDiscount,
            14);

        var weekly = RentalWorkflow.Run(
            SampleData.ValidatedAll,
            PricingPolicies.WeeklyDiscount,
            14);

        Assert.Equal(69700L, discount.GrandTotalCents);
        Assert.Equal(67250L, weekly.GrandTotalCents);
    }

    [Fact]
    public void WeeklyDiscount_SevenDayBoundary()
    {
        Assert.Equal(6000L, PricingPolicies.WeeklyDiscount(Days(6)));
        Assert.Equal(5250L, PricingPolicies.WeeklyDiscount(Days(7)));
    }

    [Fact]
    public void DaysDiscount_InvalidConfiguration_FailsWhenFixed()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => PricingPolicies.DaysDiscount(0)(15));

        Assert.Throws<ArgumentOutOfRangeException>(
            () => PricingPolicies.DaysDiscount(5)(101));
    }
}
