import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import type { FeatureCollection, Point } from 'geojson';
import { Observable } from 'rxjs';
import { API_BASE_URL, API_V1 } from '../../core/api/api-config';
import {
  BoundingBox,
  Gateway,
  GatewayCredentials,
  GatewaySummary,
  MeshMessage,
  MeshNode,
  MessageListRequest,
  NodeDetail,
  NodeListRequest,
  NodeTraceroute,
  PagedResult,
  Registration,
  SendMessageRequest,
  Team,
} from './mesh.models';

/** "west,south,east,north" as the API expects it (6 decimals ≈ 10 cm is plenty). */
export function formatBbox(box: BoundingBox): string {
  return box.map((value) => value.toFixed(6)).join(',');
}

/** Thin HTTP wrapper around the gateways, nodes, messages and map endpoints. No state here. */
@Injectable({ providedIn: 'root' })
export class MeshApi {
  private readonly http = inject(HttpClient);
  private readonly url = `${inject(API_BASE_URL)}${API_V1}`;

  /** mine = true: my gateways (pending ones included); otherwise every active gateway. */
  getGateways(mine: boolean): Observable<Gateway[]> {
    return this.http.get<Gateway[]>(`${this.url}/gateways`, { params: { mine } });
  }

  getGatewaySummary(): Observable<GatewaySummary> {
    return this.http.get<GatewaySummary>(`${this.url}/gateways/summary`);
  }

  /** Makes the node a gateway: MQTT credentials that only work for that node; the password is only in this answer. */
  addGateway(nodeNum: number): Observable<GatewayCredentials> {
    return this.http.post<GatewayCredentials>(`${this.url}/gateways`, { nodeNum });
  }

  revokeGateway(id: string): Observable<void> {
    return this.http.delete<void>(`${this.url}/gateways/${id}`);
  }

  getNodes(request: NodeListRequest): Observable<PagedResult<MeshNode>> {
    let params = new HttpParams().set('page', request.page).set('pageSize', request.pageSize);
    if (request.search) {
      params = params.set('search', request.search);
    }
    if (request.registered !== undefined) {
      params = params.set('registered', request.registered);
    }
    if (request.online !== undefined) {
      params = params.set('online', request.online);
    }
    if (request.bbox) {
      params = params.set('bbox', formatBbox(request.bbox));
    }
    if (request.mine) {
      params = params.set('owner', 'me');
    }
    return this.http.get<PagedResult<MeshNode>>(`${this.url}/nodes`, { params });
  }

  getNode(nodeNum: number): Observable<NodeDetail> {
    return this.http.get<NodeDetail>(`${this.url}/nodes/${nodeNum}`);
  }

  requestPosition(nodeNum: number): Observable<void> {
    return this.http.post<void>(`${this.url}/nodes/${nodeNum}/position-request`, null);
  }

  requestTraceroute(nodeNum: number): Observable<NodeTraceroute> {
    return this.http.post<NodeTraceroute>(`${this.url}/nodes/${nodeNum}/traceroute`, null);
  }

  getMessages(request: MessageListRequest): Observable<PagedResult<MeshMessage>> {
    let params = new HttpParams().set('page', request.page).set('pageSize', request.pageSize);
    if (request.node !== undefined) {
      params = params.set('node', request.node);
    }
    if (request.team !== undefined) {
      params = params.set('team', request.team);
    }
    return this.http.get<PagedResult<MeshMessage>>(`${this.url}/messages`, { params });
  }

  sendMessage(request: SendMessageRequest): Observable<MeshMessage> {
    return this.http.post<MeshMessage>(`${this.url}/messages`, request);
  }

  getTeams(): Observable<Team[]> {
    return this.http.get<Team[]>(`${this.url}/teams`);
  }

  createTeam(name: string, channelName: string): Observable<Team> {
    return this.http.post<Team>(`${this.url}/teams`, { name, channelName });
  }

  joinTeam(code: string): Observable<Team> {
    return this.http.post<Team>(`${this.url}/teams/join`, { code });
  }

  renewJoinCode(id: string): Observable<Team> {
    return this.http.post<Team>(`${this.url}/teams/${id}/join-code`, null);
  }

  leaveTeam(id: string): Observable<void> {
    return this.http.delete<void>(`${this.url}/teams/${id}/members/me`);
  }

  getMyRegistrations(): Observable<Registration[]> {
    return this.http.get<Registration[]>(`${this.url}/registrations`);
  }

  registerFromContactUrl(url: string): Observable<Registration> {
    return this.http.post<Registration>(`${this.url}/registrations/from-contact-url`, { url });
  }

  verifyRegistration(id: string, code: string): Observable<Registration> {
    return this.http.post<Registration>(`${this.url}/registrations/${id}/verify`, { code });
  }

  revokeRegistration(id: string): Observable<void> {
    return this.http.delete<void>(`${this.url}/registrations/${id}`);
  }

  /** GeoJSON of the given map layers (all layers when empty), optionally only inside a map view. */
  getMapFeatures(layers: string[], bbox?: BoundingBox): Observable<FeatureCollection<Point>> {
    let params = new HttpParams();
    if (layers.length > 0) {
      params = params.set('layers', layers.join(','));
    }
    if (bbox) {
      params = params.set('bbox', formatBbox(bbox));
    }
    return this.http.get<FeatureCollection<Point>>(`${this.url}/map/features`, { params });
  }
}
