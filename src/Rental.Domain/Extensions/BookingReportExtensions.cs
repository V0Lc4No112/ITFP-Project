using Rental.Domain.Models;
using Rental.Domain.Pipeline;
using Rental.Domain.Results;
using Rental.Domain.States;

namespace Rental.Domain.Extensions;

public static class BookingReportExtensions
{
    public static IEnumerable<PricedRental> ConfirmedQuotes(this BookingReport report) =>
        report.Lines
            .Where(line => line.Result is Result<Reservation>.Ok)
            .Select(line => line.Quote);
}
