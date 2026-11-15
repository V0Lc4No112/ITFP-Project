using Rental.App.Io;
using Rental.Domain.Reports;
using Rental.Domain.Results;

namespace Rental.App.Workflows;

public static class InvoiceWorkflow
{
    public static Result<Invoice> Run(string bookingsPath, string customer) =>
        BookingFile.Load(bookingsPath)
            .Bind(bookings => Invoicing.For(bookings, customer, Invoicing.DiscountFor));
}
