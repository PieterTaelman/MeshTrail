import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { API_BASE_URL, API_V1 } from '../../core/api/api-config';
import {
  PagedResult,
  Sample,
  SampleFilterOptions,
  SampleGridItem,
  SampleGridRequest,
  SaveSampleRequest,
} from './sample.models';

/** Thin HTTP wrapper around /api/v1/samples. No state here: components own their state in signals. */
@Injectable({ providedIn: 'root' })
export class SamplesApi {
  private readonly http = inject(HttpClient);
  private readonly url = `${inject(API_BASE_URL)}${API_V1}/samples`;

  getGrid(request: SampleGridRequest): Observable<PagedResult<SampleGridItem>> {
    let params = new HttpParams().set('page', request.page).set('pageSize', request.pageSize);
    if (request.search) {
      params = params.set('search', request.search);
    }
    if (request.createdBy) {
      params = params.set('createdBy', request.createdBy);
    }
    if (request.sortBy) {
      params = params.set('sortBy', request.sortBy).set('sortDescending', !!request.sortDescending);
    }
    return this.http.get<PagedResult<SampleGridItem>>(this.url, { params });
  }

  getFilterOptions(): Observable<SampleFilterOptions> {
    return this.http.get<SampleFilterOptions>(`${this.url}/filter-options`);
  }

  getById(id: string): Observable<Sample> {
    return this.http.get<Sample>(`${this.url}/${id}`);
  }

  create(request: SaveSampleRequest): Observable<Sample> {
    return this.http.post<Sample>(this.url, request);
  }

  update(id: string, request: SaveSampleRequest, rowVersion: string): Observable<Sample> {
    return this.http.put<Sample>(`${this.url}/${id}`, { ...request, rowVersion });
  }

  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.url}/${id}`);
  }
}
