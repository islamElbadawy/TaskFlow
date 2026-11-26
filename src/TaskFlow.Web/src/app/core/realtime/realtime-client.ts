import { Notification } from '../api/models';

export interface BoardChange {
  readonly taskId: string;
  readonly action: 'created' | 'updated' | 'moved' | 'deleted';
  readonly actorId?: string | null;
}

export abstract class RealtimeClient {
  abstract start(tokenFactory: () => string): Promise<void>;
  abstract stop(): Promise<void>;
  abstract onNotification(handler: (notification: Notification) => void): () => void;
  abstract onBoardChanged(handler: (change: BoardChange) => void): () => void;
  abstract onReconnected(handler: () => void): () => void;
}
