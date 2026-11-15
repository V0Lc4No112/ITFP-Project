using System.Collections.Immutable;
using Rental.App;
using Rental.Domain.Results;

// Parse the command (pure), run its workflow, render it (pure Map). Nothing is printed until the whole
// chain, including any export or save, has finished.
var outcome = Commands.Parse(args).Bind(CommandRunner.Run);

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
