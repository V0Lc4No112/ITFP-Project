using Rental.App.Io;
using Rental.Domain.Models;
using Rental.Domain.States;
using Xunit;

namespace Rental.Tests;

public class FactoryTests
{
    private static RentalRequest ValidRequest() =>
        new RentalRequest(
            "R1",
            "Alice",
            "BIKE-01",
            Category.Bike,
            1500,
            2,
            3,
            false
        );

    
    [Fact]
    public void Create_ValidRequest_ReturnsValidWithSameData()
    {
        var request = ValidRequest();

        var result = ValidatedRental.Create(request);

        var valid =
            Assert.IsType<Validation<ValidatedRental>.Valid>(result);

        Assert.Equal(request, valid.Value.Request);
    }

    
    [Theory]
    [InlineData("Id", "Id must not be blank")]
    [InlineData("Customer", "Customer must not be blank")]
    [InlineData("ItemCode", "ItemCode must not be blank")]
    [InlineData("Category", "Category is invalid (was 99)")]
    [InlineData("DailyRateCents", "DailyRateCents must be > 0 (was 0)")]
    [InlineData("Quantity", "Quantity must be >= 1 (was 0)")]
    [InlineData("Days", "Days must be >= 1 (was -2)")]
    public void Create_EachRule_RejectsWithReason(
        string field,
        string expectedReason)
    {
        var request = ValidRequest();

        request = field switch
        {
            "Id" => request with { Id = "" },
            "Customer" => request with { Customer = "" },
            "ItemCode" => request with { ItemCode = "" },
            "Category" => request with { Category = (Category)99 },
            "DailyRateCents" => request with { DailyRateCents = 0 },
            "Quantity" => request with { Quantity = 0 },
            "Days" => request with { Days = -2 },
            _ => throw new ArgumentException(
                $"Unknown field: {field}")
        };

        var result = ValidatedRental.Create(request);

        var invalid =
            Assert.IsType<Validation<ValidatedRental>.Invalid>(result);

        Assert.Equal(
            new[] { expectedReason },
            invalid.Reasons
        );
    }

   
    [Fact]
    public void Create_SeveralBadFields_ReportsAll()
    {
        var request = ValidRequest() with
        {
            Quantity = 0,
            Days = 0
        };

        var result = ValidatedRental.Create(request);

        var invalid =
            Assert.IsType<Validation<ValidatedRental>.Invalid>(result);

        Assert.Equal(2, invalid.Reasons.Length);

        Assert.Contains(
            "Quantity must be >= 1 (was 0)",
            invalid.Reasons
        );

        Assert.Contains(
            "Days must be >= 1 (was 0)",
            invalid.Reasons
        );
    }

    
    [Theory]
    [InlineData(1, 1, true)]
    [InlineData(0, 1, false)]
    [InlineData(1, 0, false)]
    public void Create_QuantityAndDaysOne_Accepted_Zero_Rejected(
        int quantity,
        int days,
        bool expectedValid)
    {
        var request = ValidRequest() with
        {
            Quantity = quantity,
            Days = days
        };

        var result = ValidatedRental.Create(request);

        if (expectedValid)
        {
            Assert.IsType<Validation<ValidatedRental>.Valid>(
                result);
        }
        else
        {
            Assert.IsType<Validation<ValidatedRental>.Invalid>(
                result);
        }
    }

    
    [Fact]
    public void StockParser_BadRows_ReturnsInvalidWithRowNumbers()
    {
        var lines = new[]
        {
            StockParser.Header,
            "BIKE-01,three"
        };

        var result = StockParser.Parse(lines);

        var invalid =
            Assert.IsType<Validation<Stock>.Invalid>(result);

        Assert.Equal(
            "Row 2: Available must be an integer (was three)",
            Assert.Single(invalid.Reasons)
        );
    }

    
    [Fact]
    public void Create_FifteenDays_ReturnsValid()
    {
        var request = ValidRequest() with
        {
            Days = 15
        };

        var result = ValidatedRental.Create(request);

        Assert.IsType<Validation<ValidatedRental>.Valid>(
            result);
    }
}
