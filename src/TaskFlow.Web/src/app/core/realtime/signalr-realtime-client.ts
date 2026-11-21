import { Injectable, inject } from '@angular/core';
import type { HubConnection } from '@microsoft/signalr';
import { Notification } from '../api/models';
import { APP_CONFIG } from '../config/app-config.token';
import { RealtimeClient } from './realtime-client';

@Injectable()
export class SignalrRealtimeClient extends RealtimeClient {
  private readonly config = inject(APP_CONFIG);
  private connection: HubConnection | null = null;
  private readonly notificationHandlers = new Set<(n: Notification) => void>();
  private readonly reconnectHandlers = new Set<() => void>();

  override async start(tokenFactory: () => string): Promise<void> {
    const { HubConnectionBuilder, HubConnectionState, LogLevel } = await import(
      '@microsoft/signalr'
    );

    if (this.connection && this.connection.state !== HubConnectionState.Disconnected) {
      return;
    }

    const base = this.config.apiUrl.endsWith('/')
      ? this.config.apiUrl.slice(0, -1)
      : this.config.apiUrl;

    const connection = new HubConnectionBuilder()
      .withUrl(`${base}/hubs/notifications`, {
        accessTokenFactory: tokenFactory,
      })
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build();

    connection.on('ReceiveNotification', (notification: Notification) => {
      for (const handler of this.notificationHandlers) {
        handler(notification);
      }
    });

    connection.onreconnected(() => {
      for (const handler of this.reconnectHandlers) {
        handler();
      }
    });

    this.connection = connection;

    try {
      await connection.start();
    } catch {
      // Connection errors are tolerated; automatic reconnect or manual refresh handles recovery.
    }
  }

  override async stop(): Promise<void> {
    if (!this.connection) return;
    const conn = this.connection;
    this.connection = null;
    try {
      await conn.stop();
    } catch {
      // Ignore stop errors during sign-out.
    }
  }

  override onNotification(handler: (notification: Notification) => void): () => void {
    this.notificationHandlers.add(handler);
    return () => this.notificationHandlers.delete(handler);
  }

  override onReconnected(handler: () => void): () => void {
    this.reconnectHandlers.add(handler);
    return () => this.reconnectHandlers.delete(handler);
  }
}
