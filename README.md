# Equipment Rental Shop

Introduction to Functional Programming, 2026/2027. A group project built up across Assignments 5 to 8 and the final project.

The shop rents bikes, skis and camping gear. The program reads rental requests from a CSV file, keeps the eligible ones, prices them with a selectable pricing policy, and prints the quotes.

> **Status:** Assignment 5 in progress. The parser (A), Stages 1 and 3 (B), Stage 2 and the policies (C) are merged, and Member D's composition, `Program.cs`, sample data and workflow tests run against them: all 16 tests pass. Start with [docs/README.md](docs/README.md): the [shared contracts](docs/contracts.md) and your [A5 task file](docs/a5/README.md).

## Intended users

Counter staff at a small rental shop who need a quick, repeatable quote for a batch of requests, and the team members who extend the pipeline in later assignments.

## Team

| Member | Name / group | Area | A5 responsibility |
|---|---|---|---|
| A | Alimzhan Kabylkhanov, IT-2511 | Boundary & I/O | CSV parsing and validation |
| B | Daniyar Azizayev, IT-2511 | Functional core | Stage 1 and Stage 3 |
| C | Mukhamejan Abdikerim, IT-2511 | Rules & states | Stage 2 (higher-order function) and pricing policies |
| D | Mereikhan Sholanbayev, IT-2511 | Integration & docs | Composition, `Program.cs`, dataset, README |

## Requirements

- .NET SDK 10 (pinned in `global.json`, rolls forward to the latest 10.0.x feature band)

## Build, run, test

Run these from the repository root:

```
dotnet build
dotnet test
dotnet run --project src/Rental.App -- data/requests.csv standard
dotnet run --project src/Rental.App -- data/requests.csv discount
```

Usage: `dotnet run --project src/Rental.App -- <csv path> <standard|discount>`. Wrong arguments print the usage; a missing file, invalid rows or an unknown policy print a clear error and exit with code 1.

### Demo commands

| Case | Command | Expected |
|---|---|---|
| Normal, Standard | `dotnet run --project src/Rental.App -- data/requests.csv standard` | R3, R1, R2, R6, R5; grand total 790.00; count 5 |
| Normal, Discount | `dotnet run --project src/Rental.App -- data/requests.csv discount` | R3, R1, R2, R6, R5; grand total 697.00; count 5 |
| Invalid input | `dotnet run --project src/Rental.App -- data/requests-invalid.csv standard` | Every row error is printed, the pipeline does not run, exit code 1 |
| Empty dataset | `dotnet run --project src/Rental.App -- data/requests-empty.csv standard` | "No eligible rentals."; grand total 0.00; count 0 |
| Unknown policy | `dotnet run --project src/Rental.App -- data/requests.csv vip` | `Unknown policy`, exit code 1 |

## Input

`data/requests.csv`, header `Id,Customer,ItemCode,Category,DailyRateCents,Quantity,Days,IsMember`.

| Field | Type | Meaning |
|---|---|---|
| `Id` | string | Request id, for example `R1` |
| `Customer` | string | Customer name (trimmed in Stage 1) |
| `ItemCode` | string | Item rented, for example `BIKE-01` |
| `Category` | `Bike`, `Ski` or `Camping` | Kind of equipment |
| `DailyRateCents` | long | Price per item per day, in cents |
| `Quantity` | int | Number of items |
| `Days` | int | Rental length in days |
| `IsMember` | bool | Shop member flag (`true` / `false`) |

### Validation (Member A)

`RequestParser.Parse(IReadOnlyList<string>? lines)` ([src/Rental.App/Io/RequestParser.cs](src/Rental.App/Io/RequestParser.cs)) returns `ParseOutcome(Valid, Errors)`. It never throws and never fixes a value: each row is either fully valid or rejected with a message for every bad field. Rows are numbered with the header as row 1, so the first data row is row 2.

| Input | Result |
|---|---|
| `null` (missing) | Error `Input is missing`, no rows |
| No lines at all | Error `Row 1: Header is missing` |
| First line is not `RequestParser.Header` | Error `Row 1: Invalid header`, no rows parsed |
| Header only (empty) | No rows and no errors: a **valid empty dataset** |
| Rows with bad fields (invalid) | Each bad field reported, that row skipped; the other rows are still checked |

Checks per row (fields are trimmed first):

- Exactly 8 columns.
- `Id`, `Customer` and `ItemCode` are not blank.
- `Category` is `Bike`, `Ski` or `Camping` (case-insensitive).
- `DailyRateCents` is a number and `> 0`.
- `Quantity` and `Days` are integers and `>= 1`.
- `IsMember` is `true` or `false`.

Example, `data/requests-invalid.csv` prints:

```
Input has 4 error(s); the pipeline was not run:
  Row 2: Days must be >= 1 (was -2)
  Row 3: Quantity must be an integer (was two)
  Row 4: Customer must not be blank
  Row 5: Category is invalid (was Boat)
```

## Pipeline

`Parse` (boundary) → `SelectEligible` → `PriceAll` → `BuildSummary` → print (boundary).

The composition is in `RentalWorkflow.Run` ([src/Rental.Domain/Pipeline/RentalWorkflow.cs](src/Rental.Domain/Pipeline/RentalWorkflow.cs)): each stage's output is the next stage's input, in one visible place. `Program` reads and parses the file, picks the policy from the command line, calls `Run`, and prints only after `Run` returns, so the core never touches the console or the file system and can be tested with plain records.

### Stage 1: `SelectEligible` (Member B)

[src/Rental.Domain/Pipeline/Stages.cs](src/Rental.Domain/Pipeline/Stages.cs)

| | |
|---|---|
| Input | `IReadOnlyList<RentalRequest> requests`, `int maxDays` |
| Output | `ImmutableArray<RentalRequest>` |
| Rule | Keep requests with `Days <= maxDays`; trim `Customer` with `with`, which makes a new record, so the caller's list is never changed |

Worked example with `maxDays = 14`: R3 (14 days) is kept and R4 (15 days) is dropped, so R1, R2, R3, R5, R6 go on to Stage 2.

### Stage 2: `PriceAll` and the pricing policies (Member C)

[src/Rental.Domain/Pipeline/Pricing.cs](src/Rental.Domain/Pipeline/Pricing.cs), [src/Rental.Domain/Policies/PricingPolicies.cs](src/Rental.Domain/Policies/PricingPolicies.cs)

| | |
|---|---|
| Input | `ImmutableArray<RentalRequest> eligible`, `Func<RentalRequest, long> policy` |
| Output | `ImmutableArray<PricedRental>`, in the same order as the input |
| Rule | Call `policy` exactly once per request and pair the request with its total |

`PriceAll` is the **higher-order function**: it takes the pricing rule as a parameter and does not know which policy it was given. The two policies are `static readonly Func<RentalRequest, long>` values:

- `Standard`: `DailyRateCents × Quantity × Days`.
- `LongRentalDiscount`: Standard, then `base * 85 / 100` when `Days >= 5` (integer division rounds down to whole cents).

`PricingPolicies.FromName` maps `standard` and `discount` (case-insensitive) to these functions and returns `null` for anything else, which `Program` reports as `Unknown policy`.

Worked example: R1 (1500 × 2 × 5) is 15000 under Standard. It has 5 days, so the discount applies: 15000 × 85 / 100 = 12750. R2 has 3 days, so it is 12000 under both policies.

### Stage 3: `BuildSummary` (Member B)

[src/Rental.Domain/Pipeline/Stages.cs](src/Rental.Domain/Pipeline/Stages.cs)

| | |
|---|---|
| Input | `ImmutableArray<PricedRental> priced` |
| Output | `QuoteSummary(Quotes, GrandTotalCents, Count)` |
| Rule | Order by `TotalCents` descending, then `Id` ascending (ordinal); grand total is the sum of `TotalCents`; count is the number of quotes |

Worked example (Standard): R3 35000, R1 15000, R2 12000, R6 12000, R5 5000. R2 and R6 tie, so `Id` puts R2 first. Grand total 79000, count 5.

## Business rules

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

## Dataset and expected results

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
- `data/requests-invalid.csv`: a negative `Days` (X1), a non-numeric `Quantity` (X2), an empty `Customer` (X3), and an unknown `Category` (X4).
- `data/requests-empty.csv`: the header row only (a **valid empty dataset**).

## Requirement map

| Assignment task | File | Method |
|---|---|---|
| Read input at the boundary | `src/Rental.App/Program.cs` | top-level statements (`File.ReadAllLines`) |
| Parse and validate | `src/Rental.App/Io/RequestParser.cs` | `RequestParser.Parse` |
| Stage 1: filter | `src/Rental.Domain/Pipeline/Stages.cs` | `Stages.SelectEligible` |
| Stage 2: higher-order function | `src/Rental.Domain/Pipeline/Pricing.cs` | `Pricing.PriceAll` |
| Two policies passed as functions | `src/Rental.Domain/Policies/PricingPolicies.cs` | `Standard`, `LongRentalDiscount`, `FromName` |
| Stage 3: aggregate | `src/Rental.Domain/Pipeline/Stages.cs` | `Stages.BuildSummary` |
| Compose the pipeline | `src/Rental.Domain/Pipeline/RentalWorkflow.cs` | `RentalWorkflow.Run` |
| Print after the core returns | `src/Rental.App/Program.cs` | top-level statements |
| Sample data | `data/` | `requests.csv`, `requests-invalid.csv`, `requests-empty.csv` |
| Shared test records | `tests/Rental.Tests/SampleData.cs` | `SampleData.R1` to `R6`, `SampleData.All` |

## Tests

| Test | File | Expected | Latest `dotnet test` |
|---|---|---|---|
| `NormalWorkflow_Standard_MatchesDocumentedResult` | `WorkflowTests.cs` | Order R3, R1, R2, R6, R5; total 79000; count 5 | Passed (2026-10-13) |
| `NormalWorkflow_Discount_MatchesDocumentedResult` | `WorkflowTests.cs` | Order R3, R1, R2, R6, R5; total 69700; count 5 | Passed (2026-10-13) |
| `EmptyDataset_ReturnsEmptySummary` | `WorkflowTests.cs` | Count 0, total 0, no exception | Passed (2026-10-13) |
| `Parse_Null_ReturnsMissingInputError` | `ParserTests.cs` | One "missing" error, no valid rows | Passed (2026-10-13) |
| `Parse_HeaderOnly_ReturnsValidEmptyDataset` | `ParserTests.cs` | No rows and no errors | Passed (2026-10-13) |
| `Parse_InvalidFields_RejectsEachWithMessage` | `ParserTests.cs` | Each bad field named; none valid | Passed (2026-10-13) |
| `Parse_IsMemberFalse_IsAcceptedAsValue` | `ParserTests.cs` | `IsMember == false` | Passed (2026-10-13) |
| `SelectEligible_ExactlyMaxDays_IsIncluded_OneMore_IsExcluded` | `StageTests.cs` | 14 kept, 15 dropped | Passed (2026-10-13) |
| `SelectEligible_DoesNotModifyCallerList` | `StageTests.cs` | Caller's list unchanged | Passed (2026-10-13) |
| `SelectEligible_TrimsCustomer` | `StageTests.cs` | `"  Anna "` becomes `"Anna"` | Passed (2026-10-13) |
| `BuildSummary_TiedTotals_OrderedById` | `StageTests.cs` | R2 before R6 | Passed (2026-10-13) |
| `TwoPolicies_SameInput_DifferForLongRental` | `PolicyTests.cs` | R1: 15000 vs 12750 | Passed (2026-10-13) |
| `TwoPolicies_ShortRental_Equal` | `PolicyTests.cs` | R2: 12000 under both | Passed (2026-10-13) |
| `PriceAll_InvokesPolicyOncePerRequest` | `PolicyTests.cs` | Policy called exactly N times | Passed (2026-10-13) |
| `Discount_ExactlyFiveDays_Applies` | `PolicyTests.cs` | 5 days discounted, 4 days not | Passed (2026-10-13) |
| `FromName_Unknown_ReturnsNull` | `PolicyTests.cs` | `"vip"` gives `null` | Passed (2026-10-13) |

## Members

| Member | Logic | Tests | Docs |
|---|---|---|---|
| A | `RequestParser.Parse` | `ParserTests.cs` | Input and validation section |
| B | `Stages.SelectEligible`, `Stages.BuildSummary` | `StageTests.cs` | Stage 1 and Stage 3 section |
| C | `Pricing.PriceAll`, `PricingPolicies` | `PolicyTests.cs` | Stage 2 and policies section |
| D | `RentalWorkflow.Run`, `Program.cs`, `data/` | `WorkflowTests.cs`, `SampleData.cs` | This README, requirement map, test table |

## Layout

```
src/Rental.Domain/   pure core: models, pipeline stages, policies (no Console, no files)
src/Rental.App/      console app = the boundary: arguments, file reading, parsing, printing
tests/Rental.Tests/  xUnit tests (TestData/ for controlled sample files)
data/                sample input used by the demos
native/              C library for Assignment 8
docs/                roadmap, per-assignment notes, reports, contributions
```

## Git workflow

- Branch per feature, prefixed by assignment: `a5/validation-member-a`.
- Commit messages start with the assignment: `[A5] Add stage 2 pricing`.
- Open a pull request into `main`; another member reviews it and runs `dotnet test` before merging.
- When an assignment is complete, tag it (`a5-submission`) and build the Moodle ZIP from the tag:
  `git archive --format=zip -o A5.zip a5-submission`

Full plan: [docs/ROADMAP.md](docs/ROADMAP.md). Shared contracts: [docs/contracts.md](docs/contracts.md).
