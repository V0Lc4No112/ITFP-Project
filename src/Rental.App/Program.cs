using System.Collections.Immutable;
using Rental.App;
using Rental.Domain.Results;

if (args.Length is not (3 or 4))
{
    Console.Error.WriteLine("Usage: dotnet run --project src/Rental.App -- <requests.csv> <standard|discount|weekly> <stock.csv> [revenue-report.csv]");
    return 1;
}

// One chain, rendered with a pure Map. Nothing is printed until the whole chain (and the export) has finished.
var run = args.Length == 4
    ? AppWorkflow.ExecuteAndExport(args[0], args[1], args[2], args[3])
    : AppWorkflow.Execute(args[0], args[1], args[2]);
var outcome = run.Map(Display.Render);

// The only place the final outcome is handled.
switch (outcome)
{
    case Result<ImmutableArray<string>>.Ok ok:
        foreach (var line in ok.Value)
        {
            Console.WriteLine(line);
        }
        return 0;
    case Result<ImmutableArray<string>>.Fail fail:
        foreach (var line in Display.RenderError(fail.Error))
        {
            Console.Error.WriteLine(line);
        }
        return 1;
    default:
        throw new InvalidOperationException("Unknown result");
}
