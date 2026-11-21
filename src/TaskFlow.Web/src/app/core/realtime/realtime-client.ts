import { Notification } from '../api/models';

export abstract class RealtimeClient {
  abstract start(tokenFactory: () => string): Promise<void>;
  abstract stop(): Promise<void>;
  abstract onNotification(handler: (notification: Notification) => void): () => void;
  abstract onReconnected(handler: () => void): () => void;
}
