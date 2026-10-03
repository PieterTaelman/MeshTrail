import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { API_BASE_URL } from '../../core/api/api-config';
import { SamplesApi } from './samples.api';

describe('SamplesApi', () => {
  let api: SamplesApi;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: API_BASE_URL, useValue: 'https://api.test' },
      ],
    });
    api = TestBed.inject(SamplesApi);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('sends paging, filter and sort as query parameters', () => {
    api
      .getGrid({ page: 2, pageSize: 10, search: 'abc', sortBy: 'name', sortDescending: true })
      .subscribe();

    const request = http.expectOne((r) => r.url === 'https://api.test/api/v1/samples');
    expect(request.request.params.get('page')).toBe('2');
    expect(request.request.params.get('pageSize')).toBe('10');
    expect(request.request.params.get('search')).toBe('abc');
    expect(request.request.params.get('sortBy')).toBe('name');
    expect(request.request.params.get('sortDescending')).toBe('true');
    expect(request.request.params.has('createdBy')).toBe(false);
    request.flush({ items: [], totalCount: 0, page: 2, pageSize: 10 });
  });

  it('sends the row version back on update', () => {
    api.update('42', { name: 'Name', description: null }, 'AQID').subscribe();

    const request = http.expectOne('https://api.test/api/v1/samples/42');
    expect(request.request.method).toBe('PUT');
    expect(request.request.body).toEqual({ name: 'Name', description: null, rowVersion: 'AQID' });
    request.flush({});
  });
});
