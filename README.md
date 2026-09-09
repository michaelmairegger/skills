# mairegger.skills

A collection of [Agent Skills](https://www.anthropic.com/news/agent-skills) for Claude Code and Claude.ai — reusable, self-contained instructions that teach Claude how to do a specific job well.

## Layout

Each skill lives in its own folder under [`skills/`](skills/):

```
skills/
└── <skill-name>/
    ├── SKILL.md        (required) — YAML frontmatter (name, description) + instructions
    ├── scripts/        (optional) — executable helpers for deterministic steps
    ├── references/      (optional) — docs loaded into context only when needed
    └── assets/          (optional) — files used in the skill's output (templates, icons, ...)
```

`SKILL.md`'s frontmatter `description` is what Claude reads to decide *when* to use the skill — it should state clearly what the skill does and in what situations to reach for it.

See [`skills/conventional-commit-messages/`](skills/conventional-commit-messages/SKILL.md) for a minimal working example.

## Adding a skill

1. Create `skills/<skill-name>/SKILL.md` with `name` and `description` frontmatter.
2. Write the instructions the way you'd explain the task to a capable colleague — imperative, explaining *why* steps matter, not just a rigid checklist.
3. Keep `SKILL.md` under ~500 lines; move large reference material into `references/` and point to it from the main file.
4. Add any test prompts you used to check the skill under an `evals/` folder in the skill's own directory, if useful.

## License

[MIT](LICENSE)
