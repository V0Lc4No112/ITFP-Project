# Shared Contracts

These are the agreements every member codes against. **Change them only by a PR that all four members approve**, because each change affects everyone's code and tests.

Topic: **Equipment rental shop** (bikes, skis, camping gear). Decided 2026-10-12.

## Project-wide conventions (all assignments)

| Item | Agreement |
|---|---|
| SDK | .NET 10, pinned in `global.json` (current LTS; .NET 8 and 9 reach end of support on 10 Nov 2026) |
| Language | C# with `Nullable` enabled. Never use the null-forgiving operator `!` to skip a check |
| Tests | xUnit, in `tests/Rental.Tests/`, one test file per area (`ParserTests.cs`, `StageTests.cs`, ...) |
| Money | Integer cents (`long`), never `double` or `decimal`. Rounding rules are stated in each rule |
| Pure core | `src/Rental.Domain` has no `Console`, no file access, no `DateTime.Now`, no `Random`, and no mutable static fields |
| Boundary | `src/Rental.App` owns arguments, file reading, parsing and printing. Printing happens only after the core returns |
| Data | Sample input in `data/`. Test-only files in `tests/Rental.Tests/TestData/` |

### Git workflow

- Branch per feature, prefixed by assignment: `a5/<feature>-member-<x>`, for example `a5/validation-member-a`.
- Commit from **your own GitHub account**; it is the evidence of your contribution at the defense.
- Commit messages start with the assignment: `[A5] Add stage 1 eligibility rule and boundary test`.
- Open a PR into `main`. One other member reviews it and runs `dotnet test` before merging. Never push directly to `main`.
- When an assignment is complete: tag it (`a5-submission`), then build the Moodle ZIP from the tag with `git archive --format=zip -o A5.zip a5-submission` and check it from a clean extraction.

## Assignment 5 contract

### Input file

`data/requests.csv`, with this header row (the constant `RequestParser.Header`):

```
Id,Customer,ItemCode,Category,DailyRateCents,Quantity,Days,IsMember
```

### Types (`src/Rental.Domain/Models/`)

```csharp
public enum Category { Bike, Ski, Camping }

public sealed record RentalRequest(
    string Id, string Customer, string ItemCode, Category Category,
    long DailyRateCents, int Quantity, int Days, bool IsMember);

public sealed record PricedRental(RentalRequest Request, long TotalCents);

public sealed record QuoteSummary(
    ImmutableArray<PricedRental> Quotes, long GrandTotalCents, int Count);
```

`ImmutableArray` uses reference equality inside a record, so tests compare `Quotes` with `SequenceEqual`.

### Signatures

| Owner | File | Signature |
|---|---|---|
| A | `src/Rental.App/Io/RequestParser.cs` | `static ParseOutcome Parse(IReadOnlyList<string>? lines)` with `record ParseOutcome(ImmutableArray<RentalRequest> Valid, ImmutableArray<string> Errors)` |
| B | `src/Rental.Domain/Pipeline/Stages.cs` | Stage 1: `static ImmutableArray<RentalRequest> SelectEligible(IReadOnlyList<RentalRequest> requests, int maxDays)` |
| C | `src/Rental.Domain/Pipeline/Pricing.cs` | Stage 2: `static ImmutableArray<PricedRental> PriceAll(ImmutableArray<RentalRequest> eligible, Func<RentalRequest, long> policy)` |
| B | `src/Rental.Domain/Pipeline/Stages.cs` | Stage 3: `static QuoteSummary BuildSummary(ImmutableArray<PricedRental> priced)` |
| C | `src/Rental.Domain/Policies/PricingPolicies.cs` | `Func<RentalRequest, long> Standard`, `Func<RentalRequest, long> LongRentalDiscount`, `static Func<RentalRequest, long>? FromName(string name)` |
| D | `src/Rental.Domain/Pipeline/RentalWorkflow.cs` | `static QuoteSummary Run(IReadOnlyList<RentalRequest> requests, Func<RentalRequest, long> policy, int maxDays)` and `const int DefaultMaxDays = 14` |

The pipeline is: `Parse` (boundary) → `SelectEligible` → `PriceAll` → `BuildSummary` → print (boundary).

### Business rules

| Rule | Owner | Definition |
|---|---|---|
| Validation | A | Every field present; `DailyRateCents > 0`; `Quantity >= 1`; `Days >= 1`; `Category` is a known value. Invalid rows get a clear error and are never silently fixed |
| Missing vs empty | A | `null` input is an error (`"Input is missing"`). Header only is a valid empty dataset |
| Errors stop the run | A + D | If `Errors` is not empty, `Program` prints them and does not run the pipeline |
| Eligibility (Stage 1) | B | `Days <= maxDays` (14). **14 is accepted, 15 is excluded.** `Customer` is trimmed |
| Standard policy | C | `DailyRateCents × Quantity × Days` |
| Long-rental discount | C | Standard, then 15% off when `Days >= 5`: `base * 85 / 100` (integer division, so it rounds down to whole cents). **5 days gets the discount** |
| Ordering (Stage 3) | B | `TotalCents` descending, then `Id` ascending |
| Policy selection | C + D | Chosen in `Program` from the command line (`standard` / `discount`) and passed as an argument. An unknown name is a clear error, not a fallback |

### Dataset and expected results

`data/requests.csv` (written by D **before** running the code):

| Id | Customer | Item | Category | Rate | Qty | Days | Member | Standard | LongRentalDiscount |
|---|---|---|---|---|---|---|---|---|---|
| R1 | Anna | BIKE-01 | Bike | 1500 | 2 | 5 | true | 15000 | **12750** |
| R2 | Ben | SKI-02 | Ski | 4000 | 1 | 3 | false | 12000 | 12000 |
| R3 | Cara | TENT-01 | Camping | 2500 | 1 | 14 | false | 35000 | **29750** (boundary: accepted) |
| R4 | Dan | BIKE-01 | Bike | 1500 | 1 | 15 | true | excluded | excluded |
| R5 | Eve | TENT-01 | Camping | 2500 | 2 | 1 | false | 5000 | 5000 |
| R6 | Finn | BIKE-01 | Bike | 1500 | 1 | 8 | false | 12000 | **10200** |

- Standard: order R3, R1, R2, R6, R5 (R2 and R6 tie at 12000, so Id decides). Grand total **79000**, count 5.
- Discount: order R3, R1, R2, R6, R5. Grand total **69700**, count 5.
- `data/requests-invalid.csv`: a negative `Days`, a non-numeric `Quantity`, an empty `Customer`, and an unknown `Category`.
- `data/requests-empty.csv`: the header row only (a **valid empty dataset**).

### Run commands

```
dotnet run --project src/Rental.App -- data/requests.csv standard
dotnet run --project src/Rental.App -- data/requests.csv discount
dotnet run --project src/Rental.App -- data/requests-invalid.csv standard
dotnet run --project src/Rental.App -- data/requests-empty.csv standard
```
