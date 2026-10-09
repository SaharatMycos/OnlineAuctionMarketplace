# auction-marketplace skill

A [Claude Code skill](https://docs.claude.com/en/docs/claude-code/skills) for this repo. It teaches Claude this project's architecture rules, product rules and everyday workflows, so changes stay consistent with the specs.

## How it's used

- **Automatically:** Claude Code loads the skill when a request matches its description, for example "add a bids table to the auctions module" or "how do I run the stack?".
- **On demand:** type `/auction-marketplace` in Claude Code from the repo root.

The skill is project-scoped. It lives in `.claude/skills/` and is committed with the code, so everyone who opens the repo in Claude Code gets it.

## Files

| File | Purpose |
|---|---|
| [SKILL.md](SKILL.md) | Entry point: layout, architecture rules, workflows (run, add module/migration/endpoint/test), definition of done, gotchas |
| [reference/domain-rules.md](reference/domain-rules.md) | Short product rules: proxy bidding, increments, soft close, deal agreement states, location privacy |
| [reference/spec-map.md](reference/spec-map.md) | Which spec section to read for each module |
| [scripts/new-module.ps1](scripts/new-module.ps1) | Scaffolds a code-first module: project, module class, `DbContext` and design-time factory. It also adds the project to the solution and Bootstrap, registers it in `ModuleCatalog` and generates the EF `InitialCreate` migration. It stops on the first failed step. |

You can also run the script yourself:

```bash
pwsh -File .claude/skills/auction-marketplace/scripts/new-module.ps1 -Name Disputes -Summary "Disputes: mediation of problem reports."
```

## Keeping it current

Update the skill in the same change whenever you change an architecture rule, a workflow, or a product rule it summarises. The specs in `docs/` stay the source of truth; this skill is a short version of them.
