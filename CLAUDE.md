# Atria (project notes for Claude Code)

Offline indoor navigation for VIT Pearl Research Park. Unity 6000.6.6f1, C#, Android.
Read `docs/STATUS.md` first (current milestone, verified vs. unverified), then `docs/ARCHITECTURE.md`.

- `Assets/Atria/Scripts/Core` (Atria.Core, no UnityEngine): data, walls, graph, router, camera path. Test with `dotnet test Tools/CoreTests`.
- `Assets/Atria/Scripts/Runtime` (Atria.App): Unity presentation only, no routing logic.
- Building data is SAMPLE/placeholder. Never invent PRP rooms, corridors or entrances. Label provenance.
- Never claim a Unity compile, an APK build or device testing unless it actually ran. Cloud sessions have no Unity Editor.
- Commit and push to `claude/sleepy-davinci-jszjm6` after each task (see Git rules below). Don't switch branches without the user's OK.
- Graphify and Ponytail are dev-only tools (`docs/DEV_TOOLS.md`) and must never become app dependencies.

## Workflow rules (standing user instructions)

Quality comes first. Token efficiency means cutting redundant exploration and output. It never
means skipping reasoning, tests or verification.

1. **Ponytail first.** Invoke `/ponytail` before exploring or implementing, and follow its
   instructions. If it is unavailable, say so briefly and continue.
2. **Graphify for navigation.**
   * Read `graphify-out/GRAPH_REPORT.md` first, if it exists and is current.
   * Use `graphify query`, `explain` and `path` to find the relevant files and symbols, then
     confirm the details in the source.
   * If the graph is stale, run `graphify update .` (AST only). Never rebuild the whole graph
     for a small task.
3. **Explore by task.**
   * Read only the relevant functions, call sites and tests, using targeted search and line
     ranges.
   * Reuse existing patterns.
   * Widen the search only when the evidence is incomplete.
4. **Minimal context.**
   * Don't dump whole files, logs or reports into context; filter output.
   * Don't re-read unchanged files or restate requests and decisions.
   * Keep plans short. For large tasks, work in phases with a compact progress summary.
5. **Smallest complete solution.**
   * Avoid speculative abstractions, unrelated refactors, reformatting and new dependencies.
   * Never weaken validation, error handling, security or accessibility.
6. **Testing.**
   * Run the narrowest relevant tests first, then the broader checks.
   * Investigate failures instead of guessing.
   * Report only the commands that actually ran and their real results.
7. **Git.**
   * Check `git status` and diffs before editing, and preserve unrelated changes.
   * No reset, clean, force-push or history rewrite without authorization.
   * **Standing authorization (from the user):** after each task's changes, run the tests,
     inspect the diff, commit only the intended files and push to the working branch
     `claude/sleepy-davinci-jszjm6`. Then sync the user's local clone if you have access to it;
     otherwise give the PowerShell pull commands. Never force-push, rewrite history or touch
     `main`.
   * Author commits as the user (`ujvall`), with **no AI co-author trailers or attribution**.
   * The user is the sole owner and contributor. Never add collaborators, members, bots or
     co-authors, and never change repository access. If the environment demands an automated
     commit or push, stop and explain instead.
8. **Communication.**
   * Be concise and ask only when a decision truly blocks progress.
   * Finish with a short report: what changed, which files, the checks that ran and their
     results, and any limitations.
9. **Done means:**
   * The requested behaviour works and existing behaviour is preserved.
   * The relevant tests and checks pass.
   * The diff has no unrelated changes.
   * Any limitations are stated.
   * For UI work, verify the rendered result where the environment allows.

## graphify

This project has a knowledge graph at graphify-out/ with god nodes, community structure, and cross-file relationships.

Rules:
- When graphify-out/graph.json exists, run `graphify query "<question>"` before your first grep, search or source-file read for a codebase question, and put this rule in every subagent prompt that explores code. Read raw files only for lines the graph or an earlier read already located. Use `graphify path "<A>" "<B>"` for relationships and `graphify explain "<concept>"` for focused concepts. These return a scoped subgraph, usually much smaller than GRAPH_REPORT.md or raw grep output.
- If graphify-out/wiki/index.md exists, use it for broad navigation instead of raw source browsing.
- Read graphify-out/GRAPH_REPORT.md only for broad architecture review or when query/path/explain do not surface enough context.
- After modifying code, run `graphify update .` to keep the graph current (AST-only, no API cost).
- A committed graphify-out/graph.json is rewritten whole on every build, so a large diff to it in a pull request is expected. Do not flag it as churn or suggest skipping the graph rebuild or its hooks.
