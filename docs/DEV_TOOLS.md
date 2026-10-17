# Developer tools: Graphify and Ponytail

These tools are for **AI-assisted development only**. They are not referenced by any Unity
assembly and are never shipped in the Android app.

Both were identified from their official sources, installed and tested in the Claude Code cloud
session on 2026-10-17. Your Windows laptop still needs the steps below.

| Tool | What it is | Official source | License | Version tested |
|---|---|---|---|---|
| **Graphify** | A Python CLI plus a Claude Code skill and PreToolUse hook. It builds a local knowledge graph of the code with tree-sitter, so the assistant can query relationships instead of reading whole files. | PyPI [`graphifyy`](https://pypi.org/project/graphifyy/) (double "y"); repo [Graphify-Labs/graphify](https://github.com/Graphify-Labs/graphify) | MIT | 0.9.92 |
| **Ponytail** | A Claude Code plugin: a skill plus Node.js lifecycle hooks (SessionStart, SubagentStart, UserPromptSubmit). It steers the assistant towards the smallest correct change, to reduce code and token use. | Marketplace [DietrichGebert/ponytail](https://github.com/DietrichGebert/ponytail) | MIT | 5.3.2 |

Other packages on PyPI named `graphify*` are **not** this project, so don't install them.
Similarly named Ponytail forks and mirrors are not the source listed above.

## What is committed in this repo
* `.claude/settings.json`
  * Graphify's PreToolUse hooks: `graphify hook-guard search|read`.
  * `enabledPlugins: ponytail@ponytail`.
  * `extraKnownMarketplaces` pointing at `DietrichGebert/ponytail`, so Claude Code offers to
    install it when you trust the project.
* `.claude/skills/graphify/`: the project-scoped Graphify skill (`/graphify`).
* `CLAUDE.md` and `.claude/CLAUDE.md`: Atria context plus Graphify's usage rules.
* `graphify-out/` is **git-ignored**. It is a generated cache, so rebuild it locally.

## Setup on Windows
Prerequisites: Python 3.10+ and [uv](https://docs.astral.sh/uv/) (or pipx), Node.js on PATH
(for Ponytail's hooks), and Claude Code.

```powershell
# Graphify CLI
uv tool install graphifyy          # or: pipx install graphifyy
graphify --version                 # expect 0.9.92 or newer

# Build the code graph, fully offline (no API key, code only)
cd C:\Users\ujval\Desktop\Projects\Atria
$env:GRAPHIFY_BACKEND = "none"
graphify extract . --code-only
graphify cluster-only . --no-label # writes graphify-out\GRAPH_REPORT.md and graph.html
```

For Ponytail, open Claude Code in the project and accept the prompt to install the `ponytail`
marketplace and plugin. If no prompt appears, enter these as **two separate** prompts:
```
/plugin marketplace add DietrichGebert/ponytail
/plugin install ponytail@ponytail
```

The skill and hook are already committed, so you don't need to re-run `graphify install`. To
reinstall them, run `graphify install --platform windows --project` and then
`graphify claude install --project`.

## Daily use
* `graphify query "how is the route camera driven by the slider"` returns a scoped subgraph.
* `graphify explain "RouteFinder"` and `graphify path "AtriaApp" "WallGenerator"`.
* Run `graphify update .` after code changes. It uses the AST only and needs no API cost.
* Ponytail commands:
  * `/ponytail lite|full|ultra|off` sets its strength.
  * `/ponytail-review` reviews the current diff.
  * `/ponytail-help` shows a quick reference.
* **Recommendation:** use `/ponytail lite` on Atria. The project's rules require tests,
  documentation and accuracy labelling, and these must never be cut to save tokens.

## Verified in the cloud session
* `graphify extract . --code-only` indexed 26 C# files with no LLM, producing about 370–400
  nodes and 1064 edges. `query`, `explain` and `path` gave correct C# relationships, and the
  hook-guard fired on reads and searches.
* `claude plugin marketplace add DietrichGebert/ponytail` and
  `claude plugin install ponytail@ponytail --scope project` succeeded.
* Ponytail's SessionStart hook script ran and printed `PONYTAIL MODE ACTIVE — level: full`.
* The hook source was reviewed. It makes no network calls except an **optional** webhook,
  which is empty by default. Its task ledger writes to `~/.config/ponytail/`.

## Caveats
* If `graphify` is not on PATH, the Graphify hook command fails. Claude Code treats this as a
  non-blocking hook error, so install the CLI first.
* Third-party reviews report much smaller token savings than either project advertises: about
  5–10× for Graphify and about 10–22% for Ponytail. One review mentions a Ponytail per-message
  delay on Windows. If you notice it, disable the plugin with `/plugin`.
* To remove both: run `graphify uninstall`, then `/plugin uninstall ponytail@ponytail`, then
  delete the related keys from `.claude/settings.json`.
