using System.Collections.Immutable;
using Rental.App.Io;
using Rental.App.Screens;
using Rental.App.Workflows;
using Rental.Domain.Results;

namespace Rental.App;

/// <summary>
/// Runs one parsed <see cref="Command"/> (Member D): pattern matching picks the owner's workflow,
/// and the owner's pure screen turns its outcome into lines with <c>Map</c>. Nothing is printed here;
/// <c>Program</c> prints once, after the whole chain has finished.
/// </summary>
public static class CommandRunner
{
    public static Result<ImmutableArray<string>> Run(Command command) =>
        command switch
        {
            Command.Quote q => AppWorkflow.Quote(q.Requests, q.Policy)
                .Map(summary => Display.RenderQuote(q.Policy.ToLowerInvariant(), summary)),
            Command.Book b => AppWorkflow.Book(b.Requests, b.Policy, b.Stock, b.ReportPath, b.SaveDir)
                .Map(Display.Render),
            Command.Restock r => RunRestock(r),
            Command.Invoice i => InvoiceWorkflow.Run(i.Bookings, i.Customer)
                .Map(InvoiceScreen.Render),
            Command.Return r => ReturnWorkflow.Run(r.Bookings, r.Returns, r.Stock, r.SaveDir)
                .Map(ReturnScreen.Render),
            Command.Cancel c => CancelWorkflow.Run(c.Bookings, c.Cancellations, c.Stock, c.SaveDir)
                .Map(CancelScreen.Render),
            _ => throw new InvalidOperationException($"Unknown command: {command.GetType().Name}"),
        };

    // A's screen needs the number of delivery lines, which RestockOutcome does not carry,
    // so the deliveries file is parsed here too (read-only; a missing file fails the same way).
    private static Result<ImmutableArray<string>> RunRestock(Command.Restock r) =>
        DeliveryParser.Load(r.Deliveries)
            .Bind(deliveries => RestockWorkflow.Execute(r.Stock, r.Deliveries, ToNullable(r.SaveDir))
                .Map(outcome => RestockScreen.Render(deliveries.Length, outcome)));

    private static string? ToNullable(Option<string> option) =>
        option is Option<string>.Some some ? some.Value : null;
}
