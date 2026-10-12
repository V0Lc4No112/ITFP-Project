# Assignment 5: Member D, Composition, Program, Dataset and README

**Area for the whole course:** Integration, lazy processing, tests and docs lead.
**Read first:** [../contracts.md](../contracts.md). You own the dataset table and the run commands in it.

## Already done

The repo skeleton is committed on `main`: the solution (`Rental.slnx`), the three projects, the models and all stage signatures with empty bodies. You don't need to redo it.

## What you build

**1. Composition:** file `src/Rental.Domain/Pipeline/RentalWorkflow.cs`

```csharp
public static QuoteSummary Run(IReadOnlyList<RentalRequest> requests, Func<RentalRequest, long> policy, int maxDays) =>
    Stages.BuildSummary(Pricing.PriceAll(Stages.SelectEligible(requests, maxDays), policy));
```
The chain must be visible in one place. You can write it as nested calls or as three named local variables.

**2. Program:** file `src/Rental.App/Program.cs`

Usage: `dotnet run --project src/Rental.App -- <csv path> <standard|discount>`
1. Check the arguments and print the usage if they are wrong.
2. If the file doesn't exist, print a clear error. Otherwise read it with `File.ReadAllLines` (A7 will harden this).
3. `RequestParser.Parse(lines)`. If there are errors, print them all and exit with code 1. Do not run the pipeline.
4. `PricingPolicies.FromName(args[1])`. If the result is `null`, print `Unknown policy` and exit with code 1.
5. `RentalWorkflow.Run(valid, policy, RentalWorkflow.DefaultMaxDays)`.
6. Only now print the quotes (Id, customer, total as `150.00`), the grand total and the count.

**3. Data files** in `data/`, exactly matching the contract's dataset table:
- `requests.csv` (R1 to R6)
- `requests-invalid.csv` (negative days, non-numeric quantity, empty customer, unknown category)
- `requests-empty.csv` (the header only)

## Your tests

File: `tests/Rental.Tests/WorkflowTests.cs`

| Test | Asserts |
|---|---|
| `NormalWorkflow_Standard_MatchesDocumentedResult` | Order R3, R1, R2, R6, R5; total 79000; count 5 (the **normal workflow** case) |
| `NormalWorkflow_Discount_MatchesDocumentedResult` | Total 69700 |
| `EmptyDataset_ReturnsEmptySummary` | Count 0, total 0, no exception (the **empty dataset** case) |

Build the R1 to R6 records in a shared test helper (`tests/Rental.Tests/SampleData.cs`) so the other members can reuse them.

## The README (you own it)

Collect the sections from A, B and C, and add:
- The topic, intended users, team (names and groups) and input fields.
- The business rules and the expected results table (copy it from the contract).
- The .NET SDK version and the exact build, run and test commands, run from the repository root.
- Demo commands for the normal, invalid and empty cases.
- The **requirement map**: each assignment task mapped to its file and method.
- The **test table**: each test, its expected result, and the latest `dotnet test` outcome.
- The **members section**: each member's logic, tests and docs.

## Git

```
git switch main && git pull
git switch -c a5/workflow-member-d
# implement + tests + data
dotnet test
git add src/Rental.Domain/Pipeline/RentalWorkflow.cs src/Rental.App/Program.cs data tests/Rental.Tests
git commit -m "[A5] Compose workflow, wire Program and add sample data"
git push -u origin a5/workflow-member-d
```
Open a PR into `main`. Merge after A, B and C, then do one final `[A5] Complete README` PR.

**Closing the assignment (you run this):** once everything is merged and `dotnet test` is green, tag the release and build the ZIP:
```
git tag a5-submission && git push origin a5-submission
git archive --format=zip -o A5.zip a5-submission
```
Then check the ZIP from a clean extraction before everyone uploads it.

## Defense prep

- Run both policies, the invalid file and the empty file.
- Trace R1 through all three stages, naming each input and output type.
- Explain why all printing happens after `Run` returns.
- **Practice live change:** add a seventh dataset row and its expected result, and update the workflow assertion.
