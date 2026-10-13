# Equipment Rental Shop

Introduction to Functional Programming, 2026/2027. A group project built up across Assignments 5 to 8 and the final project.

The shop rents bikes, skis and camping gear. The program reads rental requests from a CSV file, keeps the eligible ones, prices them with a selectable pricing policy, and prints the quotes.

> **Status:** Assignment 5 in progress. The parser (A), Stages 1 and 3 (B), Stage 2 and the policies (C) are merged, and Member D's composition, `Program.cs`, sample data and workflow tests run against them: all 16 tests pass. Start with [docs/README.md](docs/README.md): the [shared contracts](docs/contracts.md) and your [A5 task file](docs/a5/README.md).

## Intended users

Counter staff at a small rental shop who need a quick, repeatable quote for a batch of requests, and the team members who extend the pipeline in later assignments.

## Team

| Member | Name / group | Area | A5 responsibility |
|---|---|---|---|
| A | _TBD_ | Boundary & I/O | CSV parsing and validation |
| B | _TBD_ | Functional core | Stage 1 and Stage 3 |
| C | _TBD_ | Rules & states | Stage 2 (higher-order function) and pricing policies |
| D | _TBD_ | Integration & docs | Composition, `Program.cs`, dataset, README |

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

_Member A: validation rules and the missing / empty / invalid distinction go here._

## Pipeline

`Parse` (boundary) → `SelectEligible` → `PriceAll` → `BuildSummary` → print (boundary).

The composition is in `RentalWorkflow.Run` ([src/Rental.Domain/Pipeline/RentalWorkflow.cs](src/Rental.Domain/Pipeline/RentalWorkflow.cs)): each stage's output is the next stage's input, in one visible place. `Program` reads and parses the file, picks the policy from the command line, calls `Run`, and prints only after `Run` returns, so the core never touches the console or the file system and can be tested with plain records.

_Member B: Stage 1 and Stage 3 (input type, output type, rule, worked example) go here._

_Member C: Stage 2, the higher-order function and the policies go here._

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
