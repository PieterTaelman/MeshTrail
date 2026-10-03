---
name: technical-documentation
description: Use when writing, updating or reviewing Meshtrail documentation in Documentation/ (living docs) or Documentation/Research/ (decision records), including Mermaid class/sequence diagrams, DB schema tables and module READMEs, and when a code change makes an existing doc outdated. Triggers: "document this", "write docs", "update the documentation", "add a diagram", "decision record", "ADR", "explain the module".
---

# Meshtrail technical documentation

Example of a good module doc: [Documentation/Samples/README.md](../../../Documentation/Samples/README.md).

## Two kinds of docs

| Folder | Kind | Kept in sync? |
|---|---|---|
| `Documentation/<Area>/README.md` | **Living doc** — how it works today | **Yes**: update in the same change as the code |
| `Documentation/Research/YYYY-MM-DD-topic.md` | **Decision record** — why we chose X at that moment | **No**: never edit afterwards; write a new record instead |

Add every new doc to the index in `Documentation/README.md` (and decision records to `Documentation/Research/README.md`).

## Living doc structure (module)

1. One paragraph: what the module does and for whom.
2. **Where everything lives** — table of layer → files.
3. **Request flow** — Mermaid `sequenceDiagram` for the main use case.
4. **Class diagram** — Mermaid `classDiagram` of entities, repository, important relations.
5. **Database schema** — table per DB table: column, type, null, notes. Must match the `.sqlproj` script.
6. Special behaviour: concurrency, realtime events, jobs, permissions.

## Decision record structure

Context → Options considered (with pros/cons) → Decision → Consequences. Date in the file name.

## Style

- Write for a junior developer: short sentences, plain words, explain *why*.
- Prefer tables and diagrams over long prose. Mermaid only (renders on GitHub, no images to keep in sync).
- Link to files with relative paths; don't paste large code blocks that will rot.
- English, LF line endings.

## Checklist when changing code

- Did the change alter something a living doc states (endpoints, columns, flow, config keys, jobs)? → update it.
- Did you add a module? → add its README and index entry.
- Did you make a notable technical choice? → add a decision record.
