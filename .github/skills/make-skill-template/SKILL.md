---
name: make-skill-template
description: Use when creating a new AI-assistant skill (SKILL.md) for Meshtrail, or restructuring an existing one. Defines the folder location, YAML front matter (name + trigger-rich description), body structure, and how to register it in the AGENTS.md skills routing table. Triggers: "create a skill", "new skill", "add a SKILL.md", "write instructions for the agent", "make a skill for X".
---

# Making a Meshtrail skill

## Where

- Shared skills (all assistants): `.github/skills/<skill-name>/SKILL.md`
- Claude-only skills: `.claude/skills/<skill-name>/SKILL.md` (rare — prefer shared)
- Name: kebab-case, prefixed `meshtrail-` for backend/domain skills (`meshtrail-database-table`).

## Format

```markdown
---
name: <skill-name>
description: Use when <situation>. <What it covers in one sentence, naming the key technologies/files>. Triggers: "<phrase users say>", "<another phrase>", "<another>".
---

# <Title>

One line: what this skill helps with and the worked example to copy (always point at real files, e.g. the Samples module).

## Rules
- Short, testable statements. The "must"s first.

## Steps
1. Numbered, in the order you do them, with file paths and a minimal code template.

## Don'ts
- ❌ Common mistakes seen in reviews.
```

## Writing guidelines

- The **description decides whether the skill is loaded**: start with "Use when…", list concrete triggers and the
  words people actually type. Keep it under ~600 characters.
- Point at existing code instead of pasting large blocks; templates show only the shape.
- Don't duplicate `AGENTS.md` — link to it. Don't duplicate another skill — reference it by name.
- Plain language for a junior developer; explain *why* where a rule is surprising.
- Keep it current: a skill that contradicts the code is worse than none.

## Register it

1. Add a row to the **Skills routing** table in `AGENTS.md` (`When you are… | Skill`).
2. If the skill is about the Angular app, also reference it from `Client-Web/AGENTS.md`.
3. Commit as `docs(skills): add <skill-name> skill`.
