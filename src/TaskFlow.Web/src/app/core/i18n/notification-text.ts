import { Dictionary } from '../../../i18n/en';
import { Notification } from '../api/models';

const QUOTED_RE = /"([^"]+)"/;

/**
 * The backend generates English notification messages.
 * In Arabic mode, attempts to parse known server message patterns by type and render Arabic copy;
 * falls back to the original server message if the pattern doesn't match.
 */
export function formatNotificationMessage(
  notification: Notification,
  lang: 'en' | 'ar',
  dict: Dictionary,
): string {
  if (lang === 'en') {
    return notification.message;
  }

  const msg = notification.message;
  const quoted = QUOTED_RE.exec(msg)?.[1];

  if (notification.type === 'DeadlineApproaching' && quoted) {
    return dict.notifDeadline(quoted);
  }

  if (quoted) {
    const quoteStart = msg.indexOf('"');
    const prefix = msg.slice(0, quoteStart).trim();
    const actor = prefix
      .replace(/\s+(assigned you|updated|completed|marked|commented on|moved)$/i, '')
      .trim();

    if (actor) {
      switch (notification.type) {
        case 'TaskAssigned':
          return dict.notifAssigned(actor, quoted);
        case 'TaskUpdated':
          return dict.notifUpdated(actor, quoted);
        case 'TaskCompleted':
          return dict.notifCompleted(actor, quoted);
        case 'CommentAdded':
          return dict.notifComment(actor, quoted);
      }
    }
  }

  return msg;
}
