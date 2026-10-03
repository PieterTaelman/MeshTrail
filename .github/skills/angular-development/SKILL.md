---
name: angular-development
description: Use when building or changing anything in the Meshtrail Angular app (Client-Web) — components, pages, routes, services, HTTP calls to the API, reactive forms, Optimus UI components (p-table grids with server-side paging/sorting/filtering, p-button, p-select, pInputText, p-message), Tailwind styling, SignalR realtime refresh, Vitest unit tests. Triggers: "add a page", "new component", "Angular", "grid", "form", "list/detail screen", "call the API from the frontend", "Optimus", "frontend".
---

# Angular development in Meshtrail

Standards (what is allowed/forbidden): [Client-Web/AGENTS.md](../../../Client-Web/AGENTS.md).
Worked example to copy: `Client-Web/src/app/features/samples/`.

## New feature `<feature>` (list + detail)

1. **Models** `features/<feature>/<feature>.models.ts` — interfaces mirroring the C# contracts in
   `Code/Libraries/Meshtrail.Core.Contracts/<Feature>/` (camelCase). Keep them in sync when the API changes.
2. **API service** `features/<feature>/<feature>.api.ts` — `@Injectable({ providedIn: 'root' })`, `inject(HttpClient)`,
   URL from `API_BASE_URL` + `API_V1`, one method per endpoint, returns `Observable<T>`. No state, no error handling.
3. **List page** `features/<feature>/<feature>-list/` — copy `sample-list`:
   - grid state as signals (`page`, `pageSize`, `sortBy`, `sortDescending`, filters), a `computed` request,
     `rxResource({ params: () => request(), stream: ({ params }) => api.getGrid(params) })`;
   - `<p-table [lazy]="true" [paginator]="true" [totalRecords]="totalCount()" [first]="first()" (onLazyLoad)="onLazyLoad($event)">`
     with `pSortableColumn` + `<p-sortIcon>`; sorting/paging happen on the server;
   - debounce free-text search with `toSignal(toObservable(searchText).pipe(debounceTime(300)))`;
   - reset `page` to 1 when a filter changes;
   - realtime: `inject(RealtimeService).on('<feature>Changed').pipe(takeUntilDestroyed()).subscribe(() => grid.reload())`.
4. **Detail page** `features/<feature>/<feature>-detail/` — copy `sample-detail`:
   - `id = input<string>()` (route param, needs `withComponentInputBinding()`);
   - typed reactive form via `inject(NonNullableFormBuilder)`, validators mirroring the C# validator limits;
   - load with `rxResource` and fill the form in an `effect`;
   - send `rowVersion` back on update; on **409** show the message + "Reload latest version";
   - inline delete confirmation (no browser `confirm()`).
5. **Routes** `features/<feature>/<feature>.routes.ts` with `loadComponent`, registered in `app.routes.ts` via `loadChildren`.
6. **Navigation** link in `app.html`.
7. **Tests** `*.spec.ts` next to the file (Vitest + `TestBed`): API service with `provideHttpClientTesting()`,
   pure helpers directly. Optional Playwright test in `e2e/`.
8. `npm run lint && npm test && npm run build` must pass.

## Component skeleton

```ts
@Component({
  selector: 'app-thing-card',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ButtonModule],
  host: { class: 'block' },
  template: `
    <h2 class="text-lg font-semibold">{{ thing().name }}</h2>
    @if (editable()) {
      <p-button label="Edit" (onClick)="edit.emit(thing().id)" />
    }
  `,
})
export class ThingCard {
  readonly thing = input.required<Thing>();
  readonly editable = input(false);
  readonly edit = output<string>();
}
```

## Optimus UI notes

- Import per component from its entry point: `@openng/optimus-ui/table`, `/button`, `/select`, `/inputtext`,
  `/textarea`, `/message`, `/tag`, `/dialog`, `/toast` … (same names as PrimeNG 21).
- Configured once in `app.config.ts` with `provideOptimus({ theme: { preset: Aura, … } })`. Don't add global CSS
  overrides for components; use Tailwind utilities (the CSS layer order lets them win) or the component's `pt`/`styleClass`.
- Templates use `#header` / `#body` / `#emptymessage` template references in `p-table`.
- Component docs and API: https://optimus.openng.org/

## Styling

- Tailwind utilities in templates for layout and spacing. Colours through the Optimus tokens
  (`bg-surface-0`, `bg-surface-50`, `text-primary`, `text-muted-color`) so theming keeps working.
- Component `.css` files only when Tailwind can't express it; budget is 4 kB per component.
