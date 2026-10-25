using Rental.Domain.Results;
using Rental.Domain.States;

namespace Rental.Tests;

public class ResultTests
{
    [Fact]
    public void Map_Ok_PassesValueAndReturnsNewValue()
    {
        long seen = 0;

        var result = Result.Ok(15000L).Map(cents =>
        {
            seen = cents;
            return $"{cents / 100}.00";
        });

        Assert.Equal(15000L, seen);
        Assert.Equal(Result.Ok("150.00"), result);
    }

    [Fact]
    public void Bind_Ok_PassesValueAndKeepsNextResult()
    {
        var boom = new Error(ErrorCode.NotEnoughStock, "boom");
        Result<int> TakeTwo(int n) => n >= 2 ? Result.Ok(n - 2) : Result.Fail<int>(boom);

        Assert.Equal(Result.Ok(1), Result.Ok(3).Bind(TakeTwo));
        Assert.Same(boom, Assert.IsType<Result<int>.Fail>(Result.Ok(1).Bind(TakeTwo)).Error);
    }

    [Fact]
    public void Map_Fail_SkipsFunctionAndKeepsError()
    {
        var boom = new Error(ErrorCode.InvalidRequest, "boom");
        var calls = 0;

        var result = Result.Fail<int>(boom).Map(n =>
        {
            calls++;
            return n * 2;
        });

        Assert.Equal(0, calls);
        Assert.Same(boom, Assert.IsType<Result<int>.Fail>(result).Error);
    }

    [Fact]
    public void Bind_Fail_SkipsFunctionAndKeepsError()
    {
        var boom = new Error(ErrorCode.InvalidRequest, "boom");
        var calls = 0;

        var result = Result.Fail<int>(boom).Bind(n =>
        {
            calls++;
            return Result.Ok(n * 2);
        });

        Assert.Equal(0, calls);
        Assert.Same(boom, Assert.IsType<Result<int>.Fail>(result).Error);
    }

    [Fact]
    public void ContradictoryStates_CannotBeBuilt()
    {
        Assert.Throws<ArgumentNullException>(() => Result.Ok<string>(null!));
        Assert.Throws<ArgumentNullException>(() => Result.Fail<int>(null!));
        Assert.Throws<ArgumentNullException>(() => Option.Some<string>(null!));
        Assert.Throws<ArgumentException>(() => new Error(ErrorCode.InvalidRequest, " "));
    }

    [Fact]
    public void OptionToResult_SomeZeroIsOk_NoneUsesError()
    {
        var calls = 0;
        Error Missing()
        {
            calls++;
            return new Error(ErrorCode.NoStockRecord, "no stock record for BOAT");
        }

        Assert.Equal(Result.Ok(0), Option.Some(0).ToResult(Missing));
        Assert.Equal(0, calls);

        var none = Option.None<int>().ToResult(Missing);

        Assert.Equal(ErrorCode.NoStockRecord, Assert.IsType<Result<int>.Fail>(none).Error.Code);
        Assert.Equal(1, calls);
    }

    [Fact]
    public void ValidationToResult_InvalidKeepsEveryReason()
    {
        var invalid = Stock.Create([("BIKE-01", -1), ("SKI-02", -2)]);

        var result = invalid.ToResult(reasons => new Error(ErrorCode.MalformedFile, "Stock file is malformed", reasons));

        Assert.Equal(
            new[] { "Available for BIKE-01 must be >= 0 (was -1)", "Available for SKI-02 must be >= 0 (was -2)" },
            Assert.IsType<Result<Stock>.Fail>(result).Error.Details);
    }
}
