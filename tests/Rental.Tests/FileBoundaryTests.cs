
using System;
using System.Linq;
using Rental.App.Io;
using Rental.Domain.Results;
using Xunit;

namespace Rental.Tests;

public class FileBoundaryTests
{
    [Fact]
    public void ReadLines_MissingFile_FailsWithFileMissingAndPath()
    {
        using var temp = new TempFolder();
        string path = temp.PathOf("no-such-stock.csv");

        var result = DataFile.ReadLines("stock", path);

        var failure = Assert.IsType<Result<IReadOnlyList<string>>.Fail>(result);
        Assert.Equal(ErrorCode.FileMissing, failure.Error.Code);
        Assert.Contains(path, failure.Error.Message);
        Assert.True(failure.Error.IsOperational);
    }

    [Fact]
    public void ReadLines_MissingFolder_FailsWithFileMissing()
    {
        using var temp = new TempFolder();
        string path = System.IO.Path.Combine(
            temp.Path, "no-folder", "stock.csv");

        var result = DataFile.ReadLines("stock", path);

        var failure = Assert.IsType<Result<IReadOnlyList<string>>.Fail>(result);
        Assert.Equal(ErrorCode.FileMissing, failure.Error.Code);
    }

    [Fact]
    public void StockLoad_ValidFile_ReturnsStock()
    {
        using var temp = new TempFolder();
        string path = temp.Write(
            "stock.csv",
            "ItemCode,Available",
            "BIKE-01,3",
            "SKI-02,0"
        );

        var result = StockParser.Load(path);

        var success = Assert.IsType<
            Result<Rental.Domain.States.Stock>.Ok>(result);

        var expected = new[]
        {
            KeyValuePair.Create("BIKE-01", 3),
            KeyValuePair.Create("SKI-02", 0)
        };

        Assert.Equal(
            expected,
            success.Value.Items.OrderBy(x => x.Key)
        );
    }

    [Fact]
    public void StockLoad_MalformedFile_FailsWithEveryRowAndNoStock()
    {
        using var temp = new TempFolder();
        string path = temp.Write(
            "stock.csv",
            "ItemCode,Available",
            "BIKE-01,3",
            "SKI-02,two",
            "TENT-01"
        );

        var result = StockParser.Load(path);

        var failure = Assert.IsType<
            Result<Rental.Domain.States.Stock>.Fail>(result);

        Assert.Equal(ErrorCode.MalformedFile, failure.Error.Code);
        Assert.Contains(path, failure.Error.Message);

        Assert.Equal(new[]
        {
            "Row 3: Available must be an integer (was two)",
            "Row 4: Expected 2 columns, found 1"
        }, failure.Error.Details);
    }

    [Fact]
    public void RequestsLoad_InvalidRow_FailsWithInvalidRequest()
    {
        using var temp = new TempFolder();
        string path = temp.Write(
            "requests.csv",
            RequestParser.Header,
            "R1,Anna,BIKE-01,Bike,1500,2,5,true",
            "X9,Zed,BIKE-01,Bike,1500,0,5,true"
        );

        var result = RequestParser.Load(path);

        var failure = Assert.IsType<
            Result<System.Collections.Immutable.ImmutableArray<
                Rental.Domain.States.ValidatedRental>>.Fail>(result);

        Assert.Equal(ErrorCode.InvalidRequest, failure.Error.Code);
        Assert.False(failure.Error.IsOperational);
        Assert.Equal(new[]
        {
            "Row 3: Quantity must be >= 1 (was 0)"
        }, failure.Error.Details);
    }
}
