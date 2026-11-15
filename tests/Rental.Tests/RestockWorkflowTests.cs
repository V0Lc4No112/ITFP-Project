using System.IO;
using Rental.App.Workflows;
using Rental.Domain.Results;

namespace Rental.Tests;

public class RestockWorkflowTests
{
    [Fact]
    public void Execute_WithoutSave_ReturnsRestockOutcome()
    {
        using var temp = new TempFolder();

        string stockPath = temp.Write("stock-input.csv",
            "ItemCode,Available",
            "BIKE-01,3",
            "SKI-02,1",
            "TENT-01,2");

        string deliveriesPath = temp.Write("deliveries.csv",
            "ItemCode,Added",
            "BIKE-01,0",
            "SKI-02,2",
            "KAYAK-01,1",
            "TENT-01,1");

        var result = RestockWorkflow.Execute(stockPath, deliveriesPath);

        var success = Assert.IsType<Result<RestockOutcome>.Ok>(result);

        Assert.Equal(2, success.Value.Errors.Count);
        Assert.Null(success.Value.SavedTo);
    }

    [Fact]
    public void Execute_WithSave_SavesUpdatedStock()
    {
        using var temp = new TempFolder();

        string stockPath = temp.Write("stock-input.csv",
            "ItemCode,Available",
            "BIKE-01,3",
            "SKI-02,1",
            "TENT-01,2");

        string deliveriesPath = temp.Write("deliveries.csv",
            "ItemCode,Added",
            "SKI-02,2",
            "TENT-01,1");

        var result = RestockWorkflow.Execute(
            stockPath,
            deliveriesPath,
            temp.Path);

        var success = Assert.IsType<Result<RestockOutcome>.Ok>(result);

        Assert.Equal(temp.Path, success.Value.SavedTo);

        string[] savedLines = File.ReadAllLines(
            Path.Combine(temp.Path, "stock.csv"));

        Assert.Equal(
            [
                "ItemCode,Available",
                "BIKE-01,3",
                "SKI-02,3",
                "TENT-01,3"
            ],
            savedLines);
    }

    [Fact]
    public void Execute_MissingDeliveriesFile_ReturnsFailure()
    {
        using var temp = new TempFolder();

        string stockPath = temp.Write("stock-input.csv",
            "ItemCode,Available",
            "BIKE-01,3");

        var result = RestockWorkflow.Execute(
            stockPath,
            temp.PathOf("missing.csv"));

        var failure = Assert.IsType<Result<RestockOutcome>.Fail>(result);

        Assert.Equal(ErrorCode.FileMissing, failure.Error.Code);
    }
}
