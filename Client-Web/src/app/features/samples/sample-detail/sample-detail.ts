import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  input,
  signal,
} from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { ButtonModule } from '@openng/optimus-ui/button';
import { ArrowLeftIcon } from '@openng/optimus-ui/icons/arrowleft';
import { InputTextModule } from '@openng/optimus-ui/inputtext';
import { MessageModule } from '@openng/optimus-ui/message';
import { TextareaModule } from '@openng/optimus-ui/textarea';
import { of } from 'rxjs';
import { describeHttpError } from '../../../core/api/problem-details';
import { Sample, SaveSampleRequest } from '../sample.models';
import { SamplesApi } from '../samples.api';

/** Create/edit page. Route "samples/new" creates; "samples/:id" edits with optimistic concurrency. */
@Component({
  selector: 'app-sample-detail',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DatePipe,
    ReactiveFormsModule,
    RouterLink,
    ArrowLeftIcon,
    ButtonModule,
    InputTextModule,
    MessageModule,
    TextareaModule,
  ],
  templateUrl: './sample-detail.html',
})
export class SampleDetail {
  private readonly api = inject(SamplesApi);
  private readonly router = inject(Router);

  /** Route parameter (bound by withComponentInputBinding). Undefined on "samples/new". */
  readonly id = input<string>();

  protected readonly isNew = computed(() => !this.id());

  // Same limits as the C# validator, so most mistakes are caught before a round trip.
  protected readonly form = inject(NonNullableFormBuilder).group({
    name: ['', [Validators.required, Validators.maxLength(200)]],
    description: ['', [Validators.maxLength(2000)]],
  });

  protected readonly sample = rxResource({
    params: () => this.id(),
    stream: ({ params: id }) => (id ? this.api.getById(id) : of(undefined)),
  });

  protected readonly saving = signal(false);
  protected readonly confirmDelete = signal(false);
  protected readonly error = signal<string | null>(null);
  /** True after a 409: someone else saved first, so the user must reload before saving again. */
  protected readonly conflict = signal(false);

  constructor() {
    // Fill the form whenever a (re)loaded sample arrives.
    effect(() => {
      const sample = this.sample.value();
      if (sample) {
        this.form.reset({ name: sample.name, description: sample.description ?? '' });
      }
    });
  }

  protected save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const raw = this.form.getRawValue();
    const request: SaveSampleRequest = { name: raw.name, description: raw.description || null };
    const current = this.sample.value();
    const call = current
      ? this.api.update(current.id, request, current.rowVersion)
      : this.api.create(request);

    this.saving.set(true);
    this.error.set(null);
    call.subscribe({
      next: (saved: Sample) => {
        this.saving.set(false);
        void this.router.navigate(['/samples', saved.id]);
        this.sample.set(saved);
      },
      error: (error: unknown) => this.handleError(error),
    });
  }

  protected delete(): void {
    const current = this.sample.value();
    if (!current) {
      return;
    }

    this.saving.set(true);
    this.api.delete(current.id).subscribe({
      next: () => void this.router.navigate(['/samples']),
      error: (error: unknown) => this.handleError(error),
    });
  }

  protected reload(): void {
    this.conflict.set(false);
    this.error.set(null);
    this.sample.reload();
  }

  private handleError(error: unknown): void {
    this.saving.set(false);
    this.confirmDelete.set(false);
    this.conflict.set(error instanceof HttpErrorResponse && error.status === 409);
    this.error.set(describeHttpError(error));
  }
}
