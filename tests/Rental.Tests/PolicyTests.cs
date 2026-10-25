using System.Collections.Immutable;
using Rental.Domain.Models;
using Rental.Domain.Pipeline;
using Rental.Domain.Policies;
using Rental.Domain.Results;

namespace Rental.Tests;

public class PolicyTests
{
    private static RentalRequest MakeRequest(
        string id, long rate, int quantity, int days) =>
        new(id, "Anna", "BIKE-01", Category.Bike, rate, quantity, days, true);

    [Fact]
    public void TwoPolicies_SameInput_DifferForLongRental()
    {
        var request = MakeRequest("R1", 1500, 2, 5);
        Assert.Equal(15000L, PricingPolicies.Standard(request));
        Assert.Equal(12750L, PricingPolicies.LongRentalDiscount(request));
    }

    [Fact]
    public void TwoPolicies_ShortRental_Equal()
    {
        var request = MakeRequest("R2", 4000, 1, 3);
        Assert.Equal(12000L, PricingPolicies.Standard(request));
        Assert.Equal(12000L, PricingPolicies.LongRentalDiscount(request));
    }

    [Fact]
    public void PriceAll_InvokesPolicyOncePerRequest()
    {
        var requests = ImmutableArray.Create(
            MakeRequest("R1", 1500, 2, 5),
            MakeRequest("R2", 4000, 1, 3));
        int calls = 0;
        Func<RentalRequest, long> policy = request =>
        {
            calls++;
            return PricingPolicies.Standard(request);
        };

        Pricing.PriceAll(requests, policy);
        Assert.Equal(requests.Length, calls);
    }

    [Fact]
    public void Discount_ExactlyFiveDays_Applies()
    {
        Assert.Equal(12750L, PricingPolicies.LongRentalDiscount(MakeRequest("R1", 1500, 2, 5)));
        Assert.Equal(12000L, PricingPolicies.LongRentalDiscount(MakeRequest("R1", 1500, 2, 4)));
    }

    [Fact]
    public void FromName_Unknown_IsNone()
    {
        Assert.IsType<Option<Func<RentalRequest, long>>.None>(PricingPolicies.FromName("vip"));
    }
}
