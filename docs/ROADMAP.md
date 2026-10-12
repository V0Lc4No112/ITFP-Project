# ISFP Group Roadmap: Assignments 5 to 8 and the Final Project

Introduction to Functional Programming, 2026/2027. Team of 4 (Member A to D, rename once agreed).

## 1. What the files actually require

All four assignments say the same thing: **keep one runnable C# project across Assignments 5 to 8**, and it becomes the final project. Each assignment adds a layer to that same project. Every assignment is 15 points (8 implementation + 7 individual defense), each member is graded individually, and **a member who cannot explain their own code gets 0**.

| Assignment | Adds to the project | Min. cumulative tests |
|---|---|---|
| A5 Pipelining | Immutable models, boundary validation, 3+ pure stages composed, a higher-order function with 2 policies | 6 |
| A6 Temporal coupling | Validated factory (2+ checks, no bypass), union-style states, pattern matching in a real path, safe transitions, repeated-operation rule | 8 |
| A7 Errors in FP | Result type, 3+ domain failures, Maybe/Option lookup, Map + Bind chain, JSON/CSV loading with missing/malformed file handling | 10 |
| A8 Extensions & LINQ | 2 extension methods, 2 LINQ reports, currying + 2 partial applications, `yield` deferred workflow, `using` disposal, resource note, **native C library via P/Invoke** | 12 |
| Final | Same project hosted on GitHub/GitLab; 50 pts project + 50 pts individual defense; group project report + individual contribution report | n/a |

No separate final project brief was in the project files; the final project facts above come from the "Use in the final project" sections of A5 to A8. If there is a separate final brief, add it to the project files and this roadmap gets updated.

### How the assignments depend on each other

```
A5 models + validation ─┬─> A6 factory wraps the A5 validation
                        │   A6 states wrap the A5 pipeline result
A5 policy (Func) ───────┼─> A8 curried function generalises the policy
A6 Valid/Invalid ───────┼─> A7 Result (or kept as a separate domain state)
A7 JSON/CSV loading ────┼─> A8 reports must run on loaded data
A7 Result errors ───────┴─> A8 native adapter + reports report failures as Result
```

Consequence: **A5 decisions are the expensive ones.** The topic, the record fields, and the folder split must leave room for states (A6), a lookup by key (A7), categories to group by (A8) and a numeric calculation the C library can do (A8).

## 2. Week 0: decide before writing code

1. **Topic: DECIDED (2026-10-12): Equipment rental shop** (bikes, skis, tents). See [contracts.md](contracts.md). Original selection criteria: It must have: records with 5+ fields, a numeric calculation with two possible rules, a lifecycle with 2+ states (e.g. Pending → Confirmed), a lookup by id/code, and a category to group by. The course's own example fits all of this: *delivery requests → quotes → confirmed bookings*. Others: task planner, budget tracker, course enrolment.
2. **Fix tooling:** one .NET SDK version pinned in `global.json`, xUnit for tests, JSON for data (`System.Text.Json`), one OS/arch declared for the native build (A8).
3. **Create the repo** (one person creates it, invites the other three, protects `main`).
4. **Agree roles** (section 4) and write them into README.md now, because each README must list every member's logic, tests and docs.

## 3. Proposed repository layout

One solution, one evolving codebase. The pure core lives in its own project so "no Console, no files in the core" is enforced by project references, not discipline.

```
isfp-project/
├─ .gitignore                 # bin/ obj/ .vs/ .idea/ *.user native/build/ secrets
├─ global.json                # pins the .NET SDK version
├─ README.md                  # always describes the CURRENT stage (what the ZIP needs)
├─ IsfpProject.sln
├─ src/
│  ├─ Project.Domain/         # pure core: NO Console, NO File, NO DateTime.Now
│  │  ├─ Models/              # A5 records
│  │  ├─ Pipeline/            # A5 stages + composition
│  │  ├─ Policies/            # A5 policies, A8 curried configs
│  │  ├─ States/              # A6 factory, union cases, transitions
│  │  ├─ Results/             # A7 Result, Maybe, Map/Bind
│  │  ├─ Reports/             # A8 LINQ reports
│  │  └─ Extensions/          # A8 extension methods
│  └─ Project.App/            # console app = the boundary
│     ├─ Program.cs           # wires everything, picks policies, prints
│     ├─ Io/                  # A5 parsing, A7 file loading, A8 export, using/yield readers
│     └─ Native/              # A8 P/Invoke binding + adapter
├─ native/                    # A8 C library
│  ├─ calc.c, calc.h
│  └─ build.sh / build.ps1    # exact compiler command, output copied to the app
├─ tests/Project.Tests/       # xUnit; mirrors the src folders
│  └─ TestData/               # controlled sample + malformed files
├─ data/                      # sample input used by the demos
└─ docs/
   ├─ ROADMAP.md              # this file
   ├─ a5.md a6.md a7.md a8.md # frozen per-stage notes: requirement → file/method map, test table
   ├─ final-report.md         # group project report
   └─ contributions/          # member-a.md … member-d.md (individual contribution reports)
```

## 4. Roles

Each member owns one **area** for the whole course, so they build depth they can defend, and each assignment gives every member a real piece of logic plus its tests (required by every brief).

| Member | Area | Why it stays coherent |
|---|---|---|
| **A** | Boundary & I/O: parsing, file loading, native interop | A5 parsing → A7 files → A8 native adapter + `using` |
| **B** | Functional core: pipeline stages, Result/Map/Bind, reports | A5 stages → A7 Result → A8 LINQ reports |
| **C** | Rules & states: policies, factory, transitions, currying | A5 policies → A6 transitions → A8 curried configs |
| **D** | Integration, lazy processing, tests & docs lead | Wires each stage into `Program.cs`, owns README/test tables, A8 `yield` |

D also owns the README and repo hygiene, but still implements and tests logic every time (D is not "the docs person").

## 5. Per-assignment plan

Each assignment runs in the same 4 steps: **(1) contract day** — agree the types and method signatures in one short PR, so everyone codes against them; **(2) parallel feature branches**; **(3) integration + README**; **(4) defense rehearsal** where each member runs their part and does a "live modification" drill.

### Assignment 5: Pipelining (min. 6 tests)

| Member | Implements | Tests owned |
|---|---|---|
| A | Raw input → typed immutable models; boundary validation (missing vs empty vs zero/false, clear messages, no `!`) | Invalid + missing input |
| B | Stage 1 (select/normalise) and Stage 3 (result model) | Boundary value; single-stage purity (caller's list unchanged); tie ordering |
| C | Stage 2 = the higher-order function taking a `Func` policy; two policies chosen outside the pipeline | Two policies differ on the same input; delegate really invoked |
| D | Repo + solution setup, composition in one visible workflow, `Program.cs`, dataset of 5+ records with expected results written first, README | Normal workflow; empty dataset |

Contract day output: record types, the three stage signatures, the policy delegate type. Shared contract: [contracts.md](contracts.md). Per-member task files: [a5/](a5/README.md).

### Assignment 6: Temporal coupling (min. 8 tests)

| Member | Implements | Tests owned |
|---|---|---|
| A | Factory with 2+ real checks; private/internal constructor so it cannot be bypassed; reuses A5 validation | Factory success; each rejection rule; boundary value |
| B | Union-style model (closed record hierarchy), each case with its own payload | Every case constructible + matched |
| C | Safe transitions taking validated values/states; documented repeated-operation rule; no partial updates | Missing prerequisite; repeated operation; state preservation |
| D | Pattern matching (`switch` expression) in the real app path; the "unsafe before" example in README | Normal operation through the real workflow |

### Assignment 7: Errors in FP (min. 10 tests)

| Member | Implements | Tests owned |
|---|---|---|
| A | JSON/CSV loading at the boundary; catch file/parse exceptions → clear failure; no partial update | Missing file; malformed file (temp paths, cleanup) |
| B | `Result<T,E>` that cannot be contradictory; `Map` and `Bind` | Map success; Bind success; failure propagation with a call counter |
| C | Maybe/Option lookup (zero/false ≠ absent); 3+ distinct domain failures; missing required item → Result failure | Each domain failure; lookup present/absent |
| D | One real chain using both Map and Bind; single case handler at the boundary; README outcome table | Complete success chain |

Decide on contract day whether A6's Valid/Invalid becomes the Result or stays a domain state, and write why in `docs/a7.md` (the brief asks for this).

### Assignment 8: Extensions, LINQ, native C (min. 12 tests; 8 pure, 2 workflow, 2 file)

| Member | Implements | Tests owned |
|---|---|---|
| A | C library (deterministic calculation used by the app), build script, `LibraryImport`/`DllImport` binding, adapter that validates input and turns load failures into Result; native README section | Real native call vs hand-calculated value; injected failing adapter |
| B | Report 1 and Report 2 (Where, Select, OrderBy + ThenBy, GroupBy, Sum/Count; one multi-stage); extension method #1 | Normal, empty, no matches, ties, grouping totals; extension #1 |
| C | Curried function + 2 partial applications used in a report or calculation; extension method #2 | Both configured functions on the same input; extension #2 |
| D | `yield return` workflow over project data + deliberate `ToList` point; `using` ownership incl. failure/early stop; export of a report; resource note; full integration | Deferred execution probe; disposal; 2 complete workflow tests |

A8 is the heaviest stage and the native part has the most setup risk. **Member A should get the C library building and called from a test in the first days of A8**, before the reports are finished.

### Final project

- Freeze features after A8; spend the time on polish, coverage and the reports.
- Write `docs/final-report.md` together and each member writes `docs/contributions/member-x.md`.
- Because the final defense is 50 of 100 points, run **cross-defense rehearsals**: each member explains one area they did not write.
- Check the repo is public or shared with the instructor before the deadline.

## 6. How each assignment gets committed separately

Same codebase, but each assignment is a separate, identifiable unit in Git history:

1. **Branches per feature, prefixed by assignment:** `a5/validation-member-a`, `a6/factory-member-a`, `a8/native-member-a`. Each member commits from their own Git identity, which doubles as contribution evidence.
2. **Commit messages tagged:** `[A5] Add stage 2 cost calculation and boundary test`.
3. **Pull request into `main`** for every feature; at least one other member reviews it and runs `dotnet test` before merging.
4. **Close the stage with a tag and release:** when an assignment is complete and tests pass, tag it (`git tag a5-submission && git push --tags`) and create a GitHub Release. The ZIP for Moodle then comes straight from that tag: `git archive --format=zip -o A5.zip a5-submission` (already excludes bin/obj since they are not committed).
5. **Freeze the stage docs:** copy the README's requirement map and test table into `docs/aN.md` before tagging; the root README then moves on to the next stage.
6. Optionally use a GitHub Milestone per assignment (A5 to A8, Final) and attach the issues/PRs to it.

Each member checks the ZIP from a **clean extraction** (`dotnet build`, `dotnet test`, `dotnet run`) before uploading, as every brief requires.

## 7. Checklist per submission

- [ ] Every requirement mapped to file + method in README
- [ ] Test table with expected results and latest outcome
- [ ] Each member listed by name and group with their logic, tests, docs
- [ ] .NET SDK version and exact build/run/test commands, working directory
- [ ] Normal and failure demos documented and runnable
- [ ] Tag created, ZIP built from the tag and checked from a clean extraction
- [ ] Each member rehearsed explaining their own code and a small live change
