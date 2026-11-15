using Rental.App.Io;
using Rental.Domain.Pipeline;
using Rental.Domain.Results;

namespace Rental.App.Workflows;

public sealed record ReturnOutcome(ReturnReport Report, Option<string> SavedTo);

/// <summary>
/// W5 return (Member C): bookings, returns and stock files loaded with Bind (a file failure skips the rest),
/// the pure <see cref="Returns.ApplyAll"/> with Map, then the optional save with Bind.
/// </summary>
public static class ReturnWorkflow
{
    public static Result<ReturnOutcome> Run(
        string bookingsPath,
        string returnsPath,
        string stockPath,
        Option<string> saveDir) =>
        BookingFile.Load(bookingsPath)
            .Bind(bookings => ReturnParser.Load(returnsPath)
                .Bind(events => StockParser.Load(stockPath)
                    .Map(stock => Returns.ApplyAll(bookings, stock, events))))
            .Bind(report => saveDir switch
            {
                Option<string>.Some dir => SaveFolder.SaveAll(dir.Value, report.Bookings, report.Stock)
                    .Map(saved => new ReturnOutcome(report, Option.Some(saved))),
                _ => Result.Ok(new ReturnOutcome(report, Option.None<string>())),
            });
}
