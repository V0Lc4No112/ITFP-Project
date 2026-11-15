using System.Collections.Immutable;
using Rental.App.Io;
using Rental.Domain.Models;
using Rental.Domain.States;

namespace Rental.Tests;

public class DeliveryParserTests
{
    [Fact]
    public void Parse_ValidCsv_ReturnsDeliveries()
    {
        string[] lines =
        [
            "ItemCode,Added",
            "BIKE-01,2",
            "SKI-02,3"
        ];

        var result = DeliveryParser.Parse(lines);

        var valid = Assert.IsType<Validation<ImmutableArray<Delivery>>.Valid>(result);

        Assert.Equal(2, valid.Value.Length);
        Assert.Equal(new Delivery("BIKE-01", 2), valid.Value[0]);
        Assert.Equal(new Delivery("SKI-02", 3), valid.Value[1]);
    }

    [Fact]
    public void Parse_InvalidHeader_ReturnsInvalid()
    {
        string[] lines =
        [
            "Code,Quantity",
            "BIKE-01,2"
        ];

        var result = DeliveryParser.Parse(lines);

        Assert.IsType<Validation<ImmutableArray<Delivery>>.Invalid>(result);
    }

    [Fact]
    public void Parse_InvalidRows_ReturnsAllErrors()
    {
        string[] lines =
        [
            "ItemCode,Added",
            "BIKE-01,abc",
            ",2",
            "SKI-02,1,extra"
        ];

        var result = DeliveryParser.Parse(lines);

        var invalid = Assert.IsType<Validation<ImmutableArray<Delivery>>.Invalid>(result);

        Assert.Equal(3, invalid.Reasons.Length);
    }

    [Fact]
    public void Parse_ZeroQuantity_IsAcceptedByParser()
    {
        string[] lines =
        [
            "ItemCode,Added",
            "BIKE-01,0"
        ];

        var result = DeliveryParser.Parse(lines);

        var valid = Assert.IsType<Validation<ImmutableArray<Delivery>>.Valid>(result);

        Assert.Equal(0, valid.Value[0].Added);
    }
}
