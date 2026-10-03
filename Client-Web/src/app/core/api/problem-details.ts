import { HttpErrorResponse } from '@angular/common/http';

/** Shape of the ProblemDetails body the API returns for every error. */
export interface ProblemDetails {
  status?: number;
  title?: string;
  detail?: string;
  errors?: Record<string, string[]>;
}

/** Turns any HTTP error into one readable sentence for the user. */
export function describeHttpError(error: unknown): string {
  if (!(error instanceof HttpErrorResponse)) {
    return 'Something went wrong.';
  }

  const problem = error.error as ProblemDetails | null;
  if (problem?.errors) {
    return Object.values(problem.errors).flat().join(' ');
  }

  return problem?.detail ?? problem?.title ?? `Request failed (${error.status}).`;
}
