import { DestroyRef, Injectable, inject, signal } from '@angular/core';
import {
  HubConnection,
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel,
} from '@microsoft/signalr';
import { Observable } from 'rxjs';
import { API_BASE_URL } from '../api/api-config';

export type RealtimeStatus = 'disconnected' | 'connecting' | 'connected';

/** A map area: [west, south, east, north] in degrees. */
export type WatchedArea = [number, number, number, number];

/**
 * One shared SignalR connection to /hubs/notifications. Components subscribe to named events
 * (e.g. "NodeUpdated") and refresh their data. The only thing a client sends is the map area it shows
 * (watchArea), so the server pushes node changes nearby instead of everything in the world.
 */
@Injectable({ providedIn: 'root' })
export class RealtimeService {
  private readonly baseUrl = inject(API_BASE_URL);
  private connection?: HubConnection;
  private area: WatchedArea | null = null;

  /** Connection state, e.g. to show a "live" indicator in the shell. */
  readonly status = signal<RealtimeStatus>('disconnected');

  constructor() {
    inject(DestroyRef).onDestroy(() => void this.connection?.stop());
  }

  /** Emits the payload every time the server pushes <eventName>. Starts the connection on first use. */
  on<T>(eventName: string): Observable<T> {
    return new Observable<T>((subscriber) => {
      const connection = this.ensureConnection();
      const handler = (payload: T) => subscriber.next(payload);
      connection.on(eventName, handler);
      return () => connection.off(eventName, handler);
    });
  }

  /** Follow node changes in this map area. Sent again automatically after a reconnect. */
  watchArea(area: WatchedArea): void {
    this.area = area;
    const connection = this.ensureConnection();
    if (connection.state === HubConnectionState.Connected) {
      void this.sendArea(connection);
    }
  }

  private ensureConnection(): HubConnection {
    if (this.connection) {
      return this.connection;
    }

    this.connection = new HubConnectionBuilder()
      .withUrl(`${this.baseUrl}/hubs/notifications`, { withCredentials: true })
      // Keep retrying: the API may restart during development.
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build();

    const connection = this.connection;
    connection.onreconnecting(() => this.status.set('connecting'));
    connection.onreconnected(() => {
      this.status.set('connected');
      // A new connection has no groups yet.
      void this.sendArea(connection);
    });
    connection.onclose(() => this.status.set('disconnected'));

    void this.start(connection);
    return connection;
  }

  private async start(connection: HubConnection): Promise<void> {
    if (connection.state !== HubConnectionState.Disconnected) {
      return;
    }

    this.status.set('connecting');
    try {
      await connection.start();
      this.status.set('connected');
      await this.sendArea(connection);
    } catch {
      // Realtime is a nice-to-have: the app still works, it just will not auto-refresh.
      this.status.set('disconnected');
    }
  }

  private async sendArea(connection: HubConnection): Promise<void> {
    if (!this.area) {
      return;
    }
    try {
      await connection.invoke('WatchArea', ...this.area);
    } catch {
      // Not fatal: the map still loads per view, only live updates are missing until the next move.
    }
  }
}
