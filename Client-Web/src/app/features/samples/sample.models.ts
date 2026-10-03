// TypeScript copies of Meshtrail.Core.Contracts/Samples. Keep them in sync with the C# records.

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface Sample {
  id: string;
  name: string;
  description: string | null;
  createdAt: string;
  createdBy: string;
  modifiedAt: string | null;
  modifiedBy: string | null;
  /** Base64 version stamp; send it back on update so the API can detect concurrent edits. */
  rowVersion: string;
}

export interface SampleGridItem {
  id: string;
  name: string;
  description: string | null;
  createdAt: string;
  createdBy: string;
}

export type SampleSortColumn = 'name' | 'createdAt' | 'createdBy';

export interface SampleGridRequest {
  page: number;
  pageSize: number;
  search?: string;
  createdBy?: string;
  sortBy?: SampleSortColumn;
  sortDescending?: boolean;
}

export interface SampleFilterOptions {
  createdBy: string[];
}

export interface SaveSampleRequest {
  name: string;
  description: string | null;
}

/** Name of the SignalR event the API pushes after any sample change. */
export const SAMPLES_CHANGED_EVENT = 'samplesChanged';
