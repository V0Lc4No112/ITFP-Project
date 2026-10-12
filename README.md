# Equipment Rental Shop

Introduction to Functional Programming, 2026/2027. A group project built up across Assignments 5 to 8 and the final project.

The shop rents bikes, skis and camping gear. The program reads rental requests from a CSV file, keeps the eligible ones, prices them with a selectable pricing policy, and prints the quotes.

> **Status:** Assignment 5 skeleton. The types and stage signatures are in place; the logic is being implemented. Start with [docs/README.md](docs/README.md): the [shared contracts](docs/contracts.md) and your [A5 task file](docs/a5/README.md).

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
