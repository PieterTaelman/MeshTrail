import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { API_BASE_URL } from '../../core/api/api-config';
import { MeshApi } from './mesh.api';

describe('MeshApi', () => {
  let api: MeshApi;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: API_BASE_URL, useValue: 'https://api.test' },
      ],
    });
    api = TestBed.inject(MeshApi);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('sends node list filters as query parameters', () => {
    api.getNodes({ page: 1, pageSize: 500, search: '!f115', online: true }).subscribe();

    const request = http.expectOne((r) => r.url === 'https://api.test/api/v1/nodes');
    expect(request.request.params.get('pageSize')).toBe('500');
    expect(request.request.params.get('search')).toBe('!f115');
    expect(request.request.params.get('online')).toBe('true');
    expect(request.request.params.has('registered')).toBe(false);
    request.flush({ items: [], totalCount: 0, page: 1, pageSize: 500 });
  });

  it('asks only for the requested map layers', () => {
    api.getMapFeatures(['nodes']).subscribe();

    const request = http.expectOne((r) => r.url === 'https://api.test/api/v1/map/features');
    expect(request.request.params.get('layers')).toBe('nodes');
    request.flush({ type: 'FeatureCollection', features: [] });
  });

  it('posts a traceroute request for the node', () => {
    api.requestTraceroute(4044729068).subscribe();

    const request = http.expectOne('https://api.test/api/v1/nodes/4044729068/traceroute');
    expect(request.request.method).toBe('POST');
    request.flush({});
  });
});
