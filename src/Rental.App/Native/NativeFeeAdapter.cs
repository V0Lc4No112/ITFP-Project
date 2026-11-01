
using System;
using Rental.Domain.Results;

namespace Rental.App.Native;

public static class NativeFeeAdapter
{
    public static Result<long> Calculate(
        int quantity,
        long feePerItemCents)
    {
        return CalculateWith(
            quantity,
            feePerItemCents,
            NativeMethods.CalculateServiceFee);
    }

    public static Result<long> CalculateWith(
        int quantity,
        long feePerItemCents,
        Func<int, long, long> nativeCall)
    {
        if (quantity < 0 || feePerItemCents < 0)
        {
            return Result.Fail<long>(
                new Error(
                    ErrorCode.InvalidRequest,
                    "Quantity and fee must not be negative"));
        }

        if (quantity > 0 &&
            feePerItemCents > long.MaxValue / quantity)
        {
            return Result.Fail<long>(
                new Error(
                    ErrorCode.InvalidRequest,
                    "Service fee calculation would overflow"));
        }

        try
        {
            long fee = nativeCall(quantity, feePerItemCents);

            if (fee < 0)
            {
                return Result.Fail<long>(
                    new Error(
                        ErrorCode.InvalidRequest,
                        "Native service fee calculation failed"));
            }

            return Result.Ok(fee);
        }
        catch (DllNotFoundException ex)
        {
            return Result.Fail<long>(
                new Error(
                    ErrorCode.FileMissing,
                    $"Native library could not be loaded: {ex.Message}"));
        }
        catch (EntryPointNotFoundException ex)
        {
            return Result.Fail<long>(
                new Error(
                    ErrorCode.FileUnreadable,
                    $"Native function could not be found: {ex.Message}"));
        }
        catch (BadImageFormatException ex)
        {
            return Result.Fail<long>(
                new Error(
                    ErrorCode.FileUnreadable,
                    $"Native library has an incompatible format: {ex.Message}"));
        }
    }
}
