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

/**
 * One shared SignalR connection to /hubs/notifications. Components subscribe to named events
 * (e.g. "samplesChanged") and refresh their data; they never send anything over the hub.
 */
@Injectable({ providedIn: 'root' })
export class RealtimeService {
  private readonly baseUrl = inject(API_BASE_URL);
  private connection?: HubConnection;

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

    this.connection.onreconnecting(() => this.status.set('connecting'));
    this.connection.onreconnected(() => this.status.set('connected'));
    this.connection.onclose(() => this.status.set('disconnected'));

    void this.start(this.connection);
    return this.connection;
  }

  private async start(connection: HubConnection): Promise<void> {
    if (connection.state !== HubConnectionState.Disconnected) {
      return;
    }

    this.status.set('connecting');
    try {
      await connection.start();
      this.status.set('connected');
    } catch {
      // Realtime is a nice-to-have: the app still works, it just will not auto-refresh.
      this.status.set('disconnected');
    }
  }
}
