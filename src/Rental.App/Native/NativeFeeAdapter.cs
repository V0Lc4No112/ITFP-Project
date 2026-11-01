
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
            return Unavailable(
                "Native library 'rental_native' could not be loaded",
                ex);
        }
        catch (EntryPointNotFoundException ex)
        {
            return Unavailable(
                "Native function 'calculate_service_fee' was not found",
                ex);
        }
        catch (BadImageFormatException ex)
        {
            return Unavailable(
                "Native library 'rental_native' does not match this process (x64 expected)",
                ex);
        }
    }

    private static Result<long> Unavailable(
        string message,
        Exception ex) =>
        Result.Fail<long>(
            new Error(
                ErrorCode.NativeUnavailable,
                message,
                [ex.Message]));
}
