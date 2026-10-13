using System;
using Rental.App.Io;
using Xunit;

namespace Rental.Tests;

public class ParserTests
{
    [Fact]
    public void Parse_Null_ReturnsMissingInputError()
    {
        var result = RequestParser.Parse(null);

        Assert.Empty(result.Valid);
        Assert.Single(result.Errors);
        Assert.Contains("missing", result.Errors[0],
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parse_HeaderOnly_ReturnsValidEmptyDataset()
    {
        var result = RequestParser.Parse(
            new[] { RequestParser.Header });

        Assert.Empty(result.Valid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Parse_InvalidFields_RejectsEachWithMessage()
    {
        var lines = new[]
        {
            RequestParser.Header,
            "R1,Anna,BIKE-01,Bike,1500,1,-2,true",
            "R2,Ben,SKI-02,Ski,4000,abc,3,false",
            "R3,,BIKE-01,Bike,1500,1,5,true",
            "R4,Dan,BIKE-01,Unknown,1500,1,2,false"
        };

        var result = RequestParser.Parse(lines);

        Assert.Empty(result.Valid);
        Assert.Equal(4, result.Errors.Length);
        Assert.Contains(result.Errors, e => e.Contains("Days"));
        Assert.Contains(result.Errors, e => e.Contains("Quantity"));
        Assert.Contains(result.Errors, e => e.Contains("Customer"));
        Assert.Contains(result.Errors, e => e.Contains("Category"));
    }

    [Fact]
    public void Parse_IsMemberFalse_IsAcceptedAsValue()
    {
        var lines = new[]
        {
            RequestParser.Header,
            "R1,Anna,BIKE-01,Bike,1500,2,5,false"
        };

        var result = RequestParser.Parse(lines);

        Assert.Empty(result.Errors);
        Assert.Single(result.Valid);
        Assert.False(result.Valid[0].IsMember);
    }
}
