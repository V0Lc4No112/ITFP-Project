using System.Globalization;
using Rental.App.Io;
using Rental.Domain.Pipeline;
using Rental.Domain.Policies;

if (args.Length != 2)
{
    Console.Error.WriteLine("Usage: dotnet run --project src/Rental.App -- <requests.csv> <standard|discount>");
    return 1;
}

var path = args[0];
var policyName = args[1];

if (!File.Exists(path))
{
    Console.Error.WriteLine($"Error: file not found: {path}");
    return 1;
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

var policy = PricingPolicies.FromName(policyName);
if (policy is null)
{
    Console.Error.WriteLine($"Unknown policy: '{policyName}'. Use 'standard' or 'discount'.");
    return 1;
}

var summary = RentalWorkflow.Run(
    parsed.Valid.Select(v => v.Request).ToList(),
    policy,
    RentalWorkflow.DefaultMaxDays
);

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
return 0;

static string FormatCents(long cents) =>
    (cents / 100).ToString(CultureInfo.InvariantCulture) + "." + (Math.Abs(cents) % 100).ToString("00", CultureInfo.InvariantCulture);
