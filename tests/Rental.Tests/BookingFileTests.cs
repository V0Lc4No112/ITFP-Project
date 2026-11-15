using System.Linq;
using Rental.App.Io;
using Rental.Domain.States;
using Rental.Domain.Results;
using Xunit;

namespace Rental.Tests;

public class BookingFileTests
{
    private static readonly string[] SampleBookings =
    [
        BookingFile.Header,
        "B-R3,R3,Cara,TENT-01,Camping,2500,1,14,false,35000,Confirmed",
        "B-R1,R1,Anna,BIKE-01,Bike,1500,2,5,true,15000,Confirmed",
        "B-R2,R2,Ben,SKI-02,Ski,4000,1,3,false,12000,Confirmed",
        "B-R6,R6,Finn,BIKE-01,Bike,1500,1,8,false,12000,Confirmed",
        "B-R7,R7,Anna,SKI-02,Ski,4000,1,2,true,8000,Returned",
        "B-R8,R8,Ben,TENT-01,Camping,2500,1,3,false,7500,Cancelled"
    ];

    [Fact]
    public void BookingFile_SaveThenLoad_ReturnsSameStates()
    {
        var parsed = BookingFile.Parse(SampleBookings);

        var valid = Assert.IsType<
            Validation<System.Collections.Immutable.ImmutableArray<BookingState>>.Valid>(
                parsed);

        var savedLines = BookingFile.Lines(valid.Value);
        var loaded = BookingFile.Parse(savedLines);

        var loadedValid = Assert.IsType<
            Validation<System.Collections.Immutable.ImmutableArray<BookingState>>.Valid>(
                loaded);

        Assert.True(valid.Value.SequenceEqual(loadedValid.Value));
    }

    [Fact]
    public void BookingFile_UnknownStatus_IsMalformedWithRowNumber()
    {
        using var folder = new TempFolder();

        string path = folder.Write(
            "bookings.csv",
            BookingFile.Header,
            "B-1,R1,Anna,BIKE-01,Bike,1500,1,5,true,7500,Lost");

        var result = BookingFile.Load(path);

        var failure = Assert.IsType<
            Result<System.Collections.Immutable.ImmutableArray<BookingState>>.Fail>(
                result);

        Assert.Equal(ErrorCode.MalformedFile, failure.Error.Code);
        Assert.Contains(
            failure.Error.Details,
            detail => detail.Contains("Row 2") &&
                      detail.Contains("Unknown booking status"));
    }

    [Fact]
    public void BookingFile_MissingFile_FailsWithFileMissing()
    {
        using var folder = new TempFolder();

        var result = BookingFile.Load(
            folder.PathOf("missing.csv"));

        var failure = Assert.IsType<
            Result<System.Collections.Immutable.ImmutableArray<BookingState>>.Fail>(
                result);

        Assert.Equal(ErrorCode.FileMissing, failure.Error.Code);
    }

    [Fact]
    public void SaveFolder_MissingFolder_FailsWithFileUnwritable()
    {
        using var folder = new TempFolder();

        var stockResult = StockParser.Parse(
        [
            StockParser.Header,
            "BIKE-01,3"
        ]);

        var stock = Assert.IsType<Validation<Stock>.Valid>(
            stockResult).Value;

        string missingDirectory = folder.PathOf("missing");

        var result = SaveFolder.SaveStock(
            missingDirectory,
            stock);

        var failure = Assert.IsType<Result<string>.Fail>(result);

        Assert.Equal(ErrorCode.FileUnwritable, failure.Error.Code);
    }
}
