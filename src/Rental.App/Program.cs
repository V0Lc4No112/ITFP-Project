// Member D wires the workflow here:
//   dotnet run --project src/Rental.App -- data/requests.csv standard|discount
// Read the file, parse it (RequestParser), pick the policy (PricingPolicies.FromName),
// call RentalWorkflow.Run, and print only after Run returns.

if (args.Length != 2)
{
    Console.Error.WriteLine("Usage: Rental.App <requests.csv> <standard|discount>");
    return 1;
}

Console.Error.WriteLine("The A5 workflow is not wired up yet.");
return 1;
