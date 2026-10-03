import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { rxResource, takeUntilDestroyed, toObservable, toSignal } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ButtonModule } from '@openng/optimus-ui/button';
import { IconFieldModule } from '@openng/optimus-ui/iconfield';
import { PlusIcon } from '@openng/optimus-ui/icons/plus';
import { SearchIcon } from '@openng/optimus-ui/icons/search';
import { InputIconModule } from '@openng/optimus-ui/inputicon';
import { InputTextModule } from '@openng/optimus-ui/inputtext';
import { MessageModule } from '@openng/optimus-ui/message';
import { SelectModule } from '@openng/optimus-ui/select';
import { TableLazyLoadEvent, TableModule } from '@openng/optimus-ui/table';
import { debounceTime } from 'rxjs';
import { describeHttpError } from '../../../core/api/problem-details';
import { RealtimeService } from '../../../core/realtime/realtime.service';
import { SAMPLES_CHANGED_EVENT, SampleGridRequest, SampleSortColumn } from '../sample.models';
import { SamplesApi } from '../samples.api';

/** Reference list page: server-side paging, sorting and filtering, refreshed live via SignalR. */
@Component({
  selector: 'app-sample-list',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DatePipe,
    FormsModule,
    RouterLink,
    ButtonModule,
    IconFieldModule,
    InputIconModule,
    PlusIcon,
    SearchIcon,
    InputTextModule,
    MessageModule,
    SelectModule,
    TableModule,
  ],
  templateUrl: './sample-list.html',
})
export class SampleList {
  private readonly api = inject(SamplesApi);

  protected readonly pageSizeOptions = [10, 25, 50];

  // Grid state lives in signals; any change re-runs the query below.
  protected readonly page = signal(1);
  protected readonly pageSize = signal(25);
  protected readonly sortBy = signal<SampleSortColumn | undefined>(undefined);
  protected readonly sortDescending = signal(false);
  protected readonly createdBy = signal<string | undefined>(undefined);
  protected readonly searchText = signal('');

  // Wait until the user stops typing, so we do not send a request per key press.
  private readonly search = toSignal(toObservable(this.searchText).pipe(debounceTime(300)), {
    initialValue: '',
  });

  private readonly request = computed<SampleGridRequest>(() => ({
    page: this.page(),
    pageSize: this.pageSize(),
    search: this.search().trim() || undefined,
    createdBy: this.createdBy(),
    sortBy: this.sortBy(),
    sortDescending: this.sortDescending(),
  }));

  protected readonly grid = rxResource({
    params: () => this.request(),
    stream: ({ params }) => this.api.getGrid(params),
  });

  protected readonly filterOptions = rxResource({ stream: () => this.api.getFilterOptions() });

  protected readonly rows = computed(() => this.grid.value()?.items ?? []);
  protected readonly totalCount = computed(() => this.grid.value()?.totalCount ?? 0);
  protected readonly pageCount = computed(() =>
    Math.max(1, Math.ceil(this.totalCount() / this.pageSize())),
  );
  protected readonly first = computed(() => (this.page() - 1) * this.pageSize());
  protected readonly error = computed(() =>
    this.grid.error() ? describeHttpError(this.grid.error()) : null,
  );

  constructor() {
    // Someone (maybe in another tab) changed samples: reload the current page and the filter values.
    inject(RealtimeService)
      .on(SAMPLES_CHANGED_EVENT)
      .pipe(takeUntilDestroyed())
      .subscribe(() => {
        this.grid.reload();
        this.filterOptions.reload();
      });
  }

  protected onLazyLoad(event: TableLazyLoadEvent): void {
    const rows = event.rows ?? this.pageSize();
    this.pageSize.set(rows);
    this.page.set(Math.floor((event.first ?? 0) / rows) + 1);

    const field =
      typeof event.sortField === 'string' ? (event.sortField as SampleSortColumn) : undefined;
    this.sortBy.set(field);
    this.sortDescending.set(event.sortOrder === -1);
  }

  protected onSearch(value: string): void {
    this.searchText.set(value);
    this.page.set(1);
  }

  protected onCreatedByChange(value: string | null): void {
    this.createdBy.set(value ?? undefined);
    this.page.set(1);
  }
}
