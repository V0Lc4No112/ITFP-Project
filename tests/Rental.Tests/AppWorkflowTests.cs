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
            "Service fee (native C): 5.00",
            "Total due: 745.00",
            "Stock left: BIKE-01=0, SKI-02=0, TENT-01=1",
            "",
            "Revenue by category:",
            "Camping    1 booking(s)   1 item(s)     350.00",
            "Bike       2 booking(s)   3 item(s)     270.00",
            "Ski        1 booking(s)   1 item(s)     120.00",
            "",
            "Low stock (left <= 1):",
            "BIKE-01    0 left",
            "SKI-02     0 left",
            "TENT-01    1 left",
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

    
        var fail = Assert.IsType<Result<RunOutcome>.Fail>(result);
        Assert.Equal(ErrorCode.UnknownPolicy, fail.Error.Code);
        Assert.Equal("Unknown policy 'vip'. Use 'standard', 'discount' or 'weekly'", fail.Error.Message);
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

    [Fact]
    public void Execute_UsesNativeLibraryToCalculateServiceFee()
    {
        using var temp = new TempFolder();

        string requests = temp.Write("requests.csv", RequestLines);
        string stock = temp.Write("stock.csv", StockLines);

        var result = AppWorkflow.Execute(
            requests,
            "standard",
            stock);

        var success = Assert.IsType<Result<RunOutcome>.Ok>(result);

        Assert.Equal(500L, success.Value.ServiceFeeCents);
        Assert.Equal(74000L, success.Value.Booking.ConfirmedTotalCents);
    }

    [Fact]
    public void ExecuteAndExport_SampleFiles_ReportsNativeFeeAndSavesRevenueCsv()
    {
        using var temp = new TempFolder();
        string requests = temp.Write("requests.csv", RequestLines);
        string stock = temp.Write("stock.csv", StockLines);
        string export = temp.PathOf("revenue.csv");

        var result = AppWorkflow.ExecuteAndExport(requests, "standard", stock, export);

        var ok = Assert.IsType<Result<RunOutcome>.Ok>(result);
        Assert.Equal(500L, ok.Value.ServiceFeeCents);
        Assert.Equal(74500L, ok.Value.TotalDueCents);
        Assert.Equal(new[] { "BIKE-01", "SKI-02", "TENT-01" }, ok.Value.LowStock.Select(row => row.ItemCode));
        var saved = Assert.IsType<Option<string>.Some>(ok.Value.ExportedTo);
        Assert.Equal(export, saved.Value);
        Assert.Equal(new[]
        {
            "Category,Bookings,Items,RevenueCents",
            "Camping,1,1,35000",
            "Bike,2,3,27000",
            "Ski,1,1,12000",
        }, File.ReadAllLines(export));
    }

    [Fact]
    public void ExecuteAndExport_ExportFolderMissing_FailsAndLeavesNoReport()
    {
        using var temp = new TempFolder();
        string requests = temp.Write("requests.csv", RequestLines);
        string stock = temp.Write("stock.csv", StockLines);
        string export = Path.Combine(temp.Path, "no-folder", "revenue.csv");

        var result = AppWorkflow.ExecuteAndExport(requests, "standard", stock, export);

        var fail = Assert.IsType<Result<RunOutcome>.Fail>(result);
        Assert.Equal(ErrorCode.FileUnwritable, fail.Error.Code);
        Assert.Equal($"Cannot write report file '{export}': folder not found", fail.Error.Message);
        Assert.False(File.Exists(export));
        Assert.Equal(StockLines, File.ReadAllLines(stock));
        Assert.Equal(RequestLines, File.ReadAllLines(requests));
    }
}
