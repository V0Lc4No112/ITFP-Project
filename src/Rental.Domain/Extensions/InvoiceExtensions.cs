using Rental.Domain.States;

namespace Rental.Domain.Extensions;

public static class InvoiceExtensions
{
    public static bool IsBillable(this BookingState state) =>
        state is BookingState.Confirmed or BookingState.Returned;
}
