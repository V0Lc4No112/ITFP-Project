using System.Globalization;
using Rental.App.Io;
using Rental.Domain.Models;
using Rental.Domain.Pipeline;
using Rental.Domain.Policies;
using Rental.Domain.Results;
using Rental.Domain.States;

if (args.Length != 3)
{
    Console.Error.WriteLine("Usage: dotnet run --project src/Rental.App -- <requests.csv> <standard|discount> <stock.csv>");
    return 1;
}

var path = args[0];
var policyName = args[1];
var stockPath = args[2];
foreach (var file in new[] { path, stockPath })
{
    if (!File.Exists(file))
    {
        Console.Error.WriteLine($"Error: file not found: {file}");
        return 1;
    }
}

var lines = File.ReadAllLines(path);
var parsed = RequestParser.Parse(lines);
if (!parsed.Errors.IsEmpty)
{
    Console.Error.WriteLine($"Input has {parsed.Errors.Length} error(s); the pipeline was not run:");
    foreach (var error in parsed.Errors)
    {
        Console.Error.WriteLine($"  {error}");
    }
    return 1;
}

if (PricingPolicies.FromName(policyName) is not Option<Func<RentalRequest, long>>.Some { Value: var policy })
{
    Console.Error.WriteLine($"Unknown policy: '{policyName}'. Use 'standard' or 'discount'.");
    return 1;
}

Stock stock;
switch (StockParser.Parse(File.ReadAllLines(stockPath)))
{
    case Validation<Stock>.Valid valid:
        stock = valid.Value;
        break;
    case Validation<Stock>.Invalid invalid:
        Console.Error.WriteLine($"Stock file has {invalid.Reasons.Length} error(s); nothing was booked:");
        foreach (var reason in invalid.Reasons)
        {
            Console.Error.WriteLine($"  {reason}");
        }
        return 1;
    default:
        throw new InvalidOperationException("Unknown stock validation result");
}

var summary = RentalWorkflow.Run(parsed.Valid, policy, RentalWorkflow.DefaultMaxDays);
var report = Booking.ConfirmAll(summary, stock);
Console.WriteLine($"Policy: {policyName.ToLowerInvariant()}");
if (summary.Count == 0)
{
    Console.WriteLine("No eligible rentals.");
}
foreach (var quote in summary.Quotes)
{
    Console.WriteLine($"{quote.Request.Id,-4} {quote.Request.Customer,-10} {FormatCents(quote.TotalCents),10}");
}
Console.WriteLine($"Grand total: {FormatCents(summary.GrandTotalCents)}");
Console.WriteLine($"Count: {summary.Count}");
Console.WriteLine();
Console.WriteLine("Bookings:");
foreach (var line in report.Lines)
{
    var lineText = line.Result switch
    {
        Result<Reservation>.Ok ok =>
            $"{ok.Value.Booking.BookingId,-6} confirmed  {FormatCents(line.Quote.TotalCents),10}",
        Result<Reservation>.Fail fail =>
            $"{line.Quote.Request.Id,-6} REJECTED   {fail.Error.Message}",
        _ => throw new InvalidOperationException("Unknown confirm result"),
    };
    Console.WriteLine(lineText);
}
Console.WriteLine($"Confirmed total: {FormatCents(report.ConfirmedTotalCents)}");
Console.WriteLine($"Stock left: {FormatStock(report.RemainingStock)}");
return 0;

static string FormatCents(long cents) =>
    (cents / 100).ToString(CultureInfo.InvariantCulture) + "." +
    (Math.Abs(cents) % 100).ToString("00", CultureInfo.InvariantCulture);

static string FormatStock(Stock stock) =>
    string.Join(", ", stock.Items
        .OrderBy(item => item.Key, StringComparer.Ordinal)
        .Select(item => $"{item.Key}={item.Value.ToString(CultureInfo.InvariantCulture)}"));
