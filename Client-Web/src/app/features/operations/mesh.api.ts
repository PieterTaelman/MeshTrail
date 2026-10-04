import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import type { FeatureCollection, Point } from 'geojson';
import { Observable } from 'rxjs';
import { API_BASE_URL, API_V1 } from '../../core/api/api-config';
import {
  GatewayStatus,
  MeshMessage,
  MeshNode,
  MessageListRequest,
  Registration,
  SendMessageRequest,
  NodeDetail,
  NodeListRequest,
  NodeTraceroute,
  PagedResult,
} from './mesh.models';

/** Thin HTTP wrapper around the gateway, nodes and map endpoints. No state here. */
@Injectable({ providedIn: 'root' })
export class MeshApi {
  private readonly http = inject(HttpClient);
  private readonly url = `${inject(API_BASE_URL)}${API_V1}`;

  getGateway(): Observable<GatewayStatus> {
    return this.http.get<GatewayStatus>(`${this.url}/gateway`);
  }

  reconnectGateway(): Observable<void> {
    return this.http.post<void>(`${this.url}/gateway/reconnect`, null);
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
    if (request.channel !== undefined) {
      params = params.set('channel', request.channel);
    }
    if (request.node !== undefined) {
      params = params.set('node', request.node);
    }
    return this.http.get<PagedResult<MeshMessage>>(`${this.url}/messages`, { params });
  }

  sendMessage(request: SendMessageRequest): Observable<MeshMessage> {
    return this.http.post<MeshMessage>(`${this.url}/messages`, request);
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

  /** GeoJSON of the given map layers (all layers when empty). */
  getMapFeatures(layers: string[]): Observable<FeatureCollection<Point>> {
    const params = layers.length > 0 ? new HttpParams().set('layers', layers.join(',')) : undefined;
    return this.http.get<FeatureCollection<Point>>(`${this.url}/map/features`, { params });
  }
}
