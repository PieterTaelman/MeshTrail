# AGENTS.md — Meshtrail Client-Web (Angular)

Source of truth for AI assistants working in `Client-Web/`. Repository-wide rules (commits, docs, backend) are in
[../AGENTS.md](../AGENTS.md). Detailed how-to: [angular-development skill](../.github/skills/angular-development/SKILL.md).
Worked example to copy: `src/app/features/samples/` (list + detail page against `api/v1/samples`).

## Stack

- Angular 22, **zoneless**, TypeScript 6 **strict** (+ `strictTemplates`), RxJS 7.8, Signals for state
- UI: **Optimus UI only** (`@openng/optimus-ui`, theme `@openng/optimus-ui-themes/aura`). API-compatible with PrimeNG 21
  (`p-table`, `p-button`, `pInputText` …) — docs: https://optimus.openng.org/
- Tailwind CSS 4 for layout/spacing; colours via semantic Optimus tokens (`bg-content`, `border-content-border`,
  `text-foreground`, `text-muted-color`, `text-primary`). Theme: `core/theme/meshtrail-preset.ts`, dark by default
- `@microsoft/signalr` via `core/realtime/realtime.service.ts`
- Vitest (`npm test`), Playwright (`npm run e2e`), ESLint (`npm run lint`), Prettier (`npm run format`)
- Node **≥ 22.22.3 or ≥ 24.15**. LF line endings everywhere.

## Standards

- ✅ Standalone components only (the default — don't write `standalone: true`).
- ✅ `changeDetection: ChangeDetectionStrategy.OnPush` on every component (lint-enforced).
- ✅ `input()` / `output()` / `model()` instead of decorators; `inject()` instead of constructor parameters.
- ✅ Native control flow `@if` / `@for` (with `track`) / `@switch`; `@let` where it helps.
- ✅ State in `signal()` / `computed()`; data loading with `resource` / `rxResource`; `effect()` only for side effects.
- ✅ `[class.x]` / `[style.x]` bindings; `host: {}` in the component metadata for host bindings/listeners.
- ✅ Route params as component inputs (`withComponentInputBinding()`), lazy routes with `loadComponent`/`loadChildren`.
- ✅ Services that talk to the API are thin (`samples.api.ts`) and stateless; components own their state.
- ✅ Errors from the API are ProblemDetails → show them with `describeHttpError()`.
- ✅ Accessible markup: labels for every input, `aria-label` when there is no visible label.

## Forbidden

- ❌ NgModules, `standalone: true`
- ❌ `ngClass` / `ngStyle`, `*ngIf` / `*ngFor` / `*ngSwitch`
- ❌ `@HostBinding` / `@HostListener`, `@Input()` / `@Output()` decorators
- ❌ `signal.mutate` (use `set` / `update`)
- ❌ Any other UI library (PrimeNG, AWF `@ambe-web-framework/*`, Angular Material) — lint-enforced
- ❌ Hard-coded API URLs in components (use `API_BASE_URL` / `API_V1`)
- ❌ Subscriptions without cleanup (use `takeUntilDestroyed()` or resources)

## Commands

```bash
npm ci
npm start          # http://localhost:3000 (the Aspire AppHost normally runs this)
npm run lint
npm run format
npm test
npm run build
npm run e2e        # needs the AppHost running
```

## Layout

```
src/app/core/        cross-cutting: api config, ProblemDetails helper, realtime (SignalR)
src/app/features/    one folder per feature: <feature>.models.ts, <feature>.api.ts, pages, <feature>.routes.ts
src/environments/    apiBaseUrl per build configuration
e2e/                 Playwright tests
```

## Agents

- [angular-expert](../.github/agents/angular-expert.md) — implements and reviews Angular code against these standards.
- [specification](../.github/agents/specification.md) — turns a feature request into a spec before coding.
