using System.Collections.Immutable;
using Rental.App;
using Rental.App.Io;
using Rental.Domain.Results;

namespace Rental.Tests;

/// <summary>
/// End-to-end tests of the one real chain, <see cref="AppWorkflow.Execute"/>.
/// Every file is written into a <see cref="TempFolder"/>; no test reads data/.
/// </summary>
public class AppWorkflowTests
{
    private static readonly string[] RequestLines =
    [
        RequestParser.Header,
        "R1,Anna,BIKE-01,Bike,1500,2,5,true",
        "R2,Ben,SKI-02,Ski,4000,1,3,false",
        "R3,Cara,TENT-01,Camping,2500,1,14,false",
        "R4,Dan,BIKE-01,Bike,1500,1,15,true",
        "R5,Eve,TENT-01,Camping,2500,2,1,false",
        "R6,Finn,BIKE-01,Bike,1500,1,8,false",
    ];

    private static readonly string[] StockLines =
    [
        StockParser.Header,
        "BIKE-01,3",
        "SKI-02,1",
        "TENT-01,2",
    ];

    [Fact]
    public void Execute_SampleFiles_ProducesExpectedScreen()
    {
        using var temp = new TempFolder();
        string requests = temp.Write("requests.csv", RequestLines);
        string stock = temp.Write("stock.csv", StockLines);

        var screen = AppWorkflow.Execute(requests, "standard", stock).Map(Display.Render);

        var ok = Assert.IsType<Result<ImmutableArray<string>>.Ok>(screen);
        Assert.Equal(new[]
        {
            "Policy: standard",
            "R3   Cara           350.00",
            "R1   Anna           150.00",
            "R2   Ben            120.00",
            "R6   Finn           120.00",
            "R5   Eve             50.00",
            "Grand total: 790.00",
            "Count: 5",
            "",
            "Bookings:",
            "B-R3   confirmed      350.00",
            "B-R1   confirmed      150.00",
            "B-R2   confirmed      120.00",
            "B-R6   confirmed      120.00",
            "R5     REJECTED   [NotEnoughStock] R5: not enough TENT-01 (requested 2, available 1)",
            "Confirmed total: 740.00",
            "Stock left: BIKE-01=0, SKI-02=0, TENT-01=1",
        }, ok.Value);
    }

    [Fact]
    public void Execute_MissingStockFile_FailsAndLeavesFilesUnchanged()
    {
        using var temp = new TempFolder();
        string requests = temp.Write("requests.csv", RequestLines);
        string stock = temp.PathOf("no-such-stock.csv");

        var result = AppWorkflow.Execute(requests, "standard", stock);

        var fail = Assert.IsType<Result<RunOutcome>.Fail>(result);
        Assert.Equal(ErrorCode.FileMissing, fail.Error.Code);
        Assert.Contains("stock", fail.Error.Message);
        Assert.Equal(RequestLines, File.ReadAllLines(requests));
        Assert.False(File.Exists(stock));
    }

    [Fact]
    public void Execute_MalformedStockFile_FailsWithoutBooking()
    {
        using var temp = new TempFolder();
        string requests = temp.Write("requests.csv", RequestLines);
        string[] malformed = [StockParser.Header, "BIKE-01,3", "SKI-02,two"];
        string stock = temp.Write("stock.csv", malformed);

        var result = AppWorkflow.Execute(requests, "standard", stock);

        var fail = Assert.IsType<Result<RunOutcome>.Fail>(result);
        Assert.Equal(ErrorCode.MalformedFile, fail.Error.Code);
        Assert.Equal(malformed, File.ReadAllLines(stock));
    }

    [Fact]
    public void Execute_UnknownPolicy_FailsBeforeStockIsRead()
    {
        using var temp = new TempFolder();
        string requests = temp.Write("requests.csv", RequestLines);
        string stock = temp.PathOf("no-such-stock.csv");

        var result = AppWorkflow.Execute(requests, "vip", stock);

        // The stock file is missing too, so FileMissing here would mean the stock step ran.
        var fail = Assert.IsType<Result<RunOutcome>.Fail>(result);
        Assert.Equal(ErrorCode.UnknownPolicy, fail.Error.Code);
        Assert.Equal("Unknown policy 'vip'. Use 'standard' or 'discount'", fail.Error.Message);
    }

    [Fact]
    public void Execute_InvalidRequests_StopsTheChainAtTheFirstFailure()
    {
        using var temp = new TempFolder();
        string requests = temp.Write("requests.csv", RequestParser.Header, "X1,Gus,BIKE-01,Bike,1500,1,-2,false");
        string stock = temp.PathOf("no-such-stock.csv");

        var result = AppWorkflow.Execute(requests, "vip", stock);

        // The policy and the stock are wrong as well; only the first failure is reported.
        var fail = Assert.IsType<Result<RunOutcome>.Fail>(result);
        Assert.Equal(ErrorCode.InvalidRequest, fail.Error.Code);
        Assert.Equal(new[] { "Row 2: Days must be >= 1 (was -2)" }, fail.Error.Details);
    }
}
