using System.Collections.Immutable;
using Rental.App;
using Rental.App.Io;
using Rental.Domain.Results;

namespace Rental.Tests;

/// <summary>
/// Member D: whole workflows through the same path as <c>Program</c>:
/// <see cref="Commands.Parse"/> → <see cref="CommandRunner.Run"/>. Files live in a <see cref="TempFolder"/>.
/// </summary>
public class EndToEndTests
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

    private static Result<ImmutableArray<string>> Run(params string[] args) =>
        Commands.Parse(args).Bind(CommandRunner.Run);

    [Fact]
    public void Quote_Weekly_PricesWithoutReadingStock()
    {
        using var temp = new TempFolder();
        string requests = temp.Write("requests.csv", RequestLines);

        var ok = Assert.IsType<Result<ImmutableArray<string>>.Ok>(Run("quote", requests, "WEEKLY"));

        Assert.Equal(new[]
        {
            "Policy: weekly",
            "R3   Cara           262.50",
            "R1   Anna           150.00",
            "R2   Ben            120.00",
            "R6   Finn            90.00",
            "R5   Eve             50.00",
            "Grand total: 672.50",
            "Count: 5",
        }, ok.Value);
    }

    [Fact]
    public void BookSave_ThenCancelOnTheSavedFiles_RefundsAndSavesAgain()
    {
        using var temp = new TempFolder();
        string requests = temp.Write("requests.csv", RequestLines);
        string stock = temp.Write("stock.csv", StockParser.Header, "BIKE-01,3", "SKI-02,1", "TENT-01,2");
        string cancellations = temp.Write("cancellations.csv", CancellationParser.Header, "B-R3,10", "B-R5,10");
        Directory.CreateDirectory(temp.PathOf("after-book"));
        Directory.CreateDirectory(temp.PathOf("after-cancel"));

        // 1. book --save: four confirmed bookings and the stock left are written.
        var booked = Assert.IsType<Result<ImmutableArray<string>>.Ok>(
            Run("book", requests, "standard", stock, "--save", temp.PathOf("after-book")));
        Assert.Equal($"Bookings and stock saved to {temp.PathOf("after-book")}", booked.Value[^1]);
        Assert.Equal(
            ["ItemCode,Available", "BIKE-01,0", "SKI-02,0", "TENT-01,1"],
            File.ReadAllLines(temp.PathOf("after-book/stock.csv")));
        Assert.Equal(5, File.ReadAllLines(temp.PathOf("after-book/bookings.csv")).Length);

        // 2. cancel on those files: B-R3 refunded in full; R5 was rejected at booking, so it was never saved.
        var cancelled = Assert.IsType<Result<ImmutableArray<string>>.Ok>(Run(
            "cancel",
            temp.PathOf("after-book/bookings.csv"),
            cancellations,
            temp.PathOf("after-book/stock.csv"),
            "--save",
            temp.PathOf("after-cancel")));

        Assert.Equal(new[]
        {
            "Cancellations:",
            "B-R3   cancelled  refund 100%     350.00",
            "B-R5   REJECTED   [UnknownBooking] B-R5: no such booking",
            "Refund total: 350.00",
            "Stock now: BIKE-01=0, SKI-02=0, TENT-01=2",
            "",
            $"Bookings and stock saved to {temp.PathOf("after-cancel")}",
        }, cancelled.Value);
        Assert.EndsWith(",Cancelled", File.ReadAllLines(temp.PathOf("after-cancel/bookings.csv"))[1]);
    }

    [Fact]
    public void Cancel_MalformedStock_FailsAndSavesNothing()
    {
        using var temp = new TempFolder();
        string bookings = temp.Write("bookings.csv", BookingFile.Header,
            "B-R3,R3,Cara,TENT-01,Camping,2500,1,14,false,35000,Confirmed");
        string cancellations = temp.Write("cancellations.csv", CancellationParser.Header, "B-R3,10");
        string stock = temp.Write("stock.csv", StockParser.Header, "TENT-01,lots");
        Directory.CreateDirectory(temp.PathOf("out"));

        var fail = Assert.IsType<Result<ImmutableArray<string>>.Fail>(
            Run("cancel", bookings, cancellations, stock, "--save", temp.PathOf("out")));

        Assert.Equal(ErrorCode.MalformedFile, fail.Error.Code);
        Assert.Empty(Directory.GetFiles(temp.PathOf("out")));
    }
}
