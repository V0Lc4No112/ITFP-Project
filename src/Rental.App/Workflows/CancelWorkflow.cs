using Rental.App.Io;
using Rental.Domain.Pipeline;
using Rental.Domain.Results;

namespace Rental.App.Workflows;

public sealed record CancelOutcome(CancelReport Report, Option<string> SavedTo);

/// <summary>
/// W6 cancel (Member D). One chain: bookings file, cancellations file and stock file are each loaded
/// with <c>Bind</c> (any file failure skips everything after it), the pure <see cref="Cancellations.ApplyAll"/>
/// runs with <c>Map</c>, then the optional save with <c>Bind</c>. Rejected lines are part of the report,
/// not a failure of the run.
/// </summary>
public static class CancelWorkflow
{
    public static Result<CancelOutcome> Run(
        string bookingsPath,
        string cancellationsPath,
        string stockPath,
        Option<string> saveDir) =>
        BookingFile.Load(bookingsPath)
            .Bind(bookings => CancellationParser.Load(cancellationsPath)
                .Bind(events => StockParser.Load(stockPath)
                    .Map(stock => Cancellations.ApplyAll(bookings, stock, events))))
            .Bind(report => saveDir switch
            {
                Option<string>.Some dir => SaveFolder.SaveAll(dir.Value, report.Bookings, report.Stock)
                    .Map(saved => new CancelOutcome(report, Option.Some(saved))),
                _ => Result.Ok(new CancelOutcome(report, Option.None<string>())),
            });
}
