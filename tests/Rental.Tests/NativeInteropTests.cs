using System;
using Rental.App.Native;
using Rental.Domain.Results;
using Xunit;

namespace Rental.Tests;

public class NativeInteropTests
{
    
[Fact]
public void CalculateWith_NativeSignalsInvalidInput_ReturnsFailure()
{
    var result = NativeFeeAdapter.CalculateWith(
        3,
        100,
        (_, _) => -1);

    var fail = Assert.IsType<Result<long>.Fail>(result);

    Assert.Equal(ErrorCode.InvalidRequest, fail.Error.Code);
    Assert.Equal(
        "Native service fee calculation failed",
        fail.Error.Message);
}

    [Fact]
    public void Calculate_ValidInput_ReturnsCorrectFee()
    {
        var result = NativeFeeAdapter.Calculate(3, 100);

        var success = Assert.IsType<Result<long>.Ok>(result);

        Assert.Equal(300L, success.Value);
    }

    [Fact]
    public void Calculate_NegativeQuantity_ReturnsFailure()
    {
        var result = NativeFeeAdapter.Calculate(-1, 100);

        Assert.IsType<Result<long>.Fail>(result);
    }

    [Fact]
    public void Calculate_Overflow_ReturnsFailure()
    {
        var result = NativeFeeAdapter.Calculate(
            2, long.MaxValue);

        Assert.IsType<Result<long>.Fail>(result);
    }
    
[Fact]
public void Calculate_MissingNativeLibrary_ReturnsFailure()
{
    long MissingLibrary(int quantity, long fee)
    {
        throw new DllNotFoundException(
            "rental_native.dll was not found");
    }

    var result = NativeFeeAdapter.CalculateWith(
        3,
        100,
        MissingLibrary);

    var failure = Assert.IsType<Result<long>.Fail>(result);

   Assert.Equal(ErrorCode.NativeUnavailable, failure.Error.Code);
Assert.Equal(
    "Native library 'rental_native' could not be loaded",
    failure.Error.Message);
Assert.True(failure.Error.IsOperational);
Assert.NotEmpty(failure.Error.Details);
}

}
