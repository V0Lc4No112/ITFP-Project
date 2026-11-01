using System.Runtime.InteropServices;

namespace Rental.App.Native;

internal static class NativeMethods
{
    [DllImport(
        "rental_native",
        EntryPoint = "calculate_service_fee",
        CallingConvention = CallingConvention.Cdecl)]
    internal static extern long CalculateServiceFee(
        int quantity,
        long feePerItemCents);
}
