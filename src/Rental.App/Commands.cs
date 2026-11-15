using System.Collections.Immutable;
using Rental.Domain.Results;

namespace Rental.App;

/// <summary>
/// The command line as a closed union (Member D). Each case is one workflow and holds exactly the
/// inputs that workflow needs, so <c>Program</c> cannot start a workflow with a missing file path.
/// </summary>
public abstract record Command
{
    private Command() { }

    /// <summary>W1: price the requests with a policy; no stock is read.</summary>
    public sealed record Quote(string Requests, string Policy) : Command;

    /// <summary>W2: quote, confirm against stock, fee, reports; optional CSV export and save.</summary>
    public sealed record Book(
        string Requests, string Policy, string Stock, Option<string> ReportPath, Option<string> SaveDir) : Command;

    /// <summary>W3 (Member A): add delivered items to stock.</summary>
    public sealed record Restock(string Stock, string Deliveries, Option<string> SaveDir) : Command;

    /// <summary>W6: cancel confirmed bookings with a refund tier.</summary>
    public sealed record Cancel(
        string Bookings, string Cancellations, string Stock, Option<string> SaveDir) : Command;
}

public static class Commands
{
    public const string SaveFlag = "--save";

    public static readonly ImmutableArray<string> Usage =
    [
        "dotnet run --project src/Rental.App -- quote   <requests.csv> <standard|discount|weekly>",
        "dotnet run --project src/Rental.App -- book    <requests.csv> <policy> <stock.csv> [report.csv] [--save <folder>]",
        "dotnet run --project src/Rental.App -- restock <stock.csv> <deliveries.csv> [--save <folder>]",
        "dotnet run --project src/Rental.App -- cancel  <bookings.csv> <cancellations.csv> <stock.csv> [--save <folder>]",
        "dotnet run --project src/Rental.App -- <requests.csv> <policy> <stock.csv> [report.csv]   (A5-A8 form, same as book)",
    ];

    /// <summary>
    /// Pure: turns the raw arguments into one <see cref="Command"/> or a usage failure. An optional
    /// trailing <c>--save &lt;folder&gt;</c> pair is split off first; the rest is matched on
    /// (command name, number of arguments). Arguments that do not start with a command name keep the
    /// A5-A8 meaning (3 or 4 arguments = book), so every earlier demo still works.
    /// </summary>
    public static Result<Command> Parse(IReadOnlyList<string> args) =>
        SplitSave(args).Bind(split => Match(split.Args, split.SaveDir));

    private static Result<(ImmutableArray<string> Args, Option<string> SaveDir)> SplitSave(IReadOnlyList<string> args)
    {
        int flag = args.ToList().IndexOf(SaveFlag);
        return flag switch
        {
            -1 => Result.Ok((args.ToImmutableArray(), Option.None<string>())),
            _ when flag == args.Count - 2 && !string.IsNullOrWhiteSpace(args[^1]) =>
                Result.Ok((args.Take(flag).ToImmutableArray(), Option.Some(args[^1]))),
            _ => UsageError<(ImmutableArray<string>, Option<string>)>($"'{SaveFlag}' must be the last option and be followed by a folder"),
        };
    }

    private static Result<Command> Match(ImmutableArray<string> a, Option<string> save) =>
        (Name: a.Length > 0 ? a[0].ToLowerInvariant() : "", a.Length, Save: save is Option<string>.Some) switch
        {
            ("quote", 3, false) => Ok(new Command.Quote(a[1], a[2])),
            ("book", 4, _) => Ok(new Command.Book(a[1], a[2], a[3], Option.None<string>(), save)),
            ("book", 5, _) => Ok(new Command.Book(a[1], a[2], a[3], Option.Some(a[4]), save)),
            ("restock", 3, _) => Ok(new Command.Restock(a[1], a[2], save)),
            ("cancel", 4, _) => Ok(new Command.Cancel(a[1], a[2], a[3], save)),
            ("quote" or "book" or "restock" or "cancel", _, _) =>
                UsageError<Command>($"Wrong arguments for '{a[0]}'"),
            // Reserved for B (invoice) and C (return) so they are never read as an A5-A8 file name.
            ("invoice" or "return", _, _) =>
                UsageError<Command>($"Command '{a[0]}' is not in this build yet"),
            (_, 3, false) => Ok(new Command.Book(a[0], a[1], a[2], Option.None<string>(), save)),
            (_, 4, false) => Ok(new Command.Book(a[0], a[1], a[2], Option.Some(a[3]), save)),
            ("", _, _) => UsageError<Command>("No command given"),
            _ => UsageError<Command>($"Unknown command '{a[0]}'"),
        };

    private static Result<Command> Ok(Command command) => Result.Ok(command);

    private static Result<T> UsageError<T>(string message) =>
        Result.Fail<T>(new Error(ErrorCode.InvalidRequest, $"{message}. Usage:", Usage));
}
