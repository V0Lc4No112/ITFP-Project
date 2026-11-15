using Rental.Domain.Models;
using Rental.Domain.Pipeline;
using Rental.Domain.Results;
using Rental.Domain.States;

namespace Rental.Tests;

public class RestockingTests
{
    private static Stock CreateStock() =>
        Stock.Create([
            ("BIKE-01", 3),
            ("SKI-02", 1),
            ("TENT-01", 2)
        ]) switch
        {
            Validation<Stock>.Valid valid => valid.Value,
            Validation<Stock>.Invalid invalid =>
                throw new InvalidOperationException(string.Join("; ", invalid.Reasons)),
            _ => throw new InvalidOperationException("Unknown validation result")
        };

    [Fact]
    public void ApplyOne_ValidDelivery_IncreasesAvailableStock()
    {
        var stock = CreateStock();

        var result = Restocking.ApplyOne(stock, new Delivery("SKI-02", 2));

        var updatedStock = result switch
        {
            Result<Stock>.Ok ok => ok.Value,
            Result<Stock>.Fail fail => throw new InvalidOperationException(fail.Error.Message),
            _ => throw new InvalidOperationException("Unknown result")
        };

        Assert.Equal(3, updatedStock.Items.Single(x => x.Key == "SKI-02").Value);
    }

    [Fact]
    public void ApplyOne_ZeroQuantity_ReturnsInvalidEvent()
    {
        var result = Restocking.ApplyOne(CreateStock(), new Delivery("BIKE-01", 0));

        Assert.IsType<Result<Stock>.Fail>(result);
        Assert.Equal(ErrorCode.InvalidEvent, ((Result<Stock>.Fail)result).Error.Code);
    }

    [Fact]
    public void ApplyOne_UnknownItem_ReturnsUnknownItem()
    {
        var result = Restocking.ApplyOne(CreateStock(), new Delivery("KAYAK-01", 1));

        Assert.IsType<Result<Stock>.Fail>(result);
        Assert.Equal(ErrorCode.UnknownItem, ((Result<Stock>.Fail)result).Error.Code);
    }

    [Fact]
    public void Apply_InvalidDelivery_DoesNotStopLaterValidDeliveries()
    {
        var stock = CreateStock();

        var deliveries = new[]
        {
            new Delivery("BIKE-01", 0),
            new Delivery("SKI-02", 2),
            new Delivery("KAYAK-01", 1),
            new Delivery("TENT-01", 1)
        };

        var result = Restocking.Apply(stock, deliveries);

        var updatedStock = result.Stock.Items.ToDictionary(x => x.Key, x => x.Value);

        Assert.Equal(3, updatedStock["BIKE-01"]);
        Assert.Equal(3, updatedStock["SKI-02"]);
        Assert.Equal(3, updatedStock["TENT-01"]);
        Assert.Equal(2, result.Errors.Count);
        Assert.Equal(ErrorCode.InvalidEvent, result.Errors[0].Code);
        Assert.Equal(ErrorCode.UnknownItem, result.Errors[1].Code);
    }
}
