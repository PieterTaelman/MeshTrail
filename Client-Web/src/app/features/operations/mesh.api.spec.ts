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
    expect(request.request.params.has('bbox')).toBe(false);
    request.flush({ items: [], totalCount: 0, page: 1, pageSize: 500 });
  });

  it('sends the map view and the "my nodes" filter', () => {
    api.getNodes({ page: 1, pageSize: 200, bbox: [2.5, 49.5, 6.4, 51.5], mine: true }).subscribe();

    const request = http.expectOne((r) => r.url === 'https://api.test/api/v1/nodes');
    expect(request.request.params.get('bbox')).toBe('2.500000,49.500000,6.400000,51.500000');
    expect(request.request.params.get('owner')).toBe('me');
    request.flush({ items: [], totalCount: 0, page: 1, pageSize: 200 });
  });

  it('asks only for the requested map layers in the map view', () => {
    api.getMapFeatures(['nodes', 'gateways'], [4, 50, 5, 51]).subscribe();

    const request = http.expectOne((r) => r.url === 'https://api.test/api/v1/map/features');
    expect(request.request.params.get('layers')).toBe('nodes,gateways');
    expect(request.request.params.get('bbox')).toBe('4.000000,50.000000,5.000000,51.000000');
    request.flush({ type: 'FeatureCollection', features: [] });
  });

  it('adds a gateway and lists only mine', () => {
    api.addGateway(4044729068).subscribe();
    api.getGateways(true).subscribe();

    const add = http.expectOne('https://api.test/api/v1/gateways');
    expect(add.request.method).toBe('POST');
    expect(add.request.body).toEqual({ nodeNum: 4044729068 });
    add.flush({});
    const mine = http.expectOne((r) => r.url === 'https://api.test/api/v1/gateways');
    expect(mine.request.params.get('mine')).toBe('true');
    mine.flush([]);
  });

  it('posts a traceroute request for the node', () => {
    api.requestTraceroute(4044729068).subscribe();

    const request = http.expectOne('https://api.test/api/v1/nodes/4044729068/traceroute');
    expect(request.request.method).toBe('POST');
    request.flush({});
  });

  it('asks for one conversation by node', () => {
    api.getMessages({ page: 1, pageSize: 50, node: 42 }).subscribe();

    const request = http.expectOne((r) => r.url === 'https://api.test/api/v1/messages');
    expect(request.request.params.get('node')).toBe('42');
    request.flush({ items: [], totalCount: 0, page: 1, pageSize: 50 });
  });

  it('posts the contact link and the code to the registration endpoints', () => {
    api.registerFromContactUrl('https://meshtastic.org/v/#abc').subscribe();
    api.verifyRegistration('r1', '123456').subscribe();

    const register = http.expectOne('https://api.test/api/v1/registrations/from-contact-url');
    expect(register.request.body).toEqual({ url: 'https://meshtastic.org/v/#abc' });
    register.flush({});
    const verify = http.expectOne('https://api.test/api/v1/registrations/r1/verify');
    expect(verify.request.body).toEqual({ code: '123456' });
    verify.flush({});
  });
});
