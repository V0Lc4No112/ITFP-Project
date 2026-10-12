# Assignment 5: Composing Functions Using Pipelining

Each member works from the shared contract plus their own task file.

| Member | Task file | Builds | Branch |
|---|---|---|---|
| A | [member-a.md](member-a.md) | CSV parsing and boundary validation | `a5/validation-member-a` |
| B | [member-b.md](member-b.md) | Stage 1 (eligibility) and Stage 3 (summary) | `a5/stages-member-b` |
| C | [member-c.md](member-c.md) | Stage 2 (higher-order function) and two pricing policies | `a5/pricing-member-c` |
| D | [member-d.md](member-d.md) | Composition, `Program.cs`, sample data, README, closing tag | `a5/workflow-member-d` |

Shared types, signatures, rules and the expected results: [../contracts.md](../contracts.md).

## Order of work

1. ✓ Skeleton and contracts are on `main`.
2. **A, B, C and D in parallel**, each on their own branch with `[A5]` commits. They open PRs into `main`; another member reviews and runs `dotnet test` before merging.
3. **D** merges last, runs both policies against the expected table, and completes the README with everyone's sections.
4. **Everyone** runs `dotnet test` (about 16 tests, against the 6 required). D tags `a5-submission` and builds the ZIP; each member checks it from a clean extraction.
5. **Defense rehearsal:** each member traces R1 through all three stages, explains their own part, and does the practice live change from their task file.

## Required test cases → owner

| Case from the assignment | Covered by |
|---|---|
| Normal workflow | D |
| Empty dataset | D, A |
| Boundary value | B, C |
| Invalid and missing input | A |
| Two policies | C |
| Purity and stages | B |
