import { TaskStatus } from '../api/models';

const MS_PER_DAY = 86_400_000;
const ISO_OFFSET_RE = /(Z|[+-]\d{2}:\d{2})$/i;

/**
 * Parses an ISO date string from the API as UTC.
 * If the backend omits a trailing Z (e.g. EF DateTime without Kind=Utc), appends 'Z'
 * so JavaScript never misinterprets a UTC timestamp as local time.
 */
export function parseUtcDate(iso: string): Date {
  const normalized = ISO_OFFSET_RE.test(iso) ? iso : `${iso}Z`;
  return new Date(normalized);
}

/**
 * Due-date client rule (03-api): converts a calendar day (YYYY-MM-DD) from the date picker
 * to 23:59:59.999 in the user's local time zone, formatted as an ISO UTC string.
 */
export function toEndOfDayUtc(dateInput: string | null): string | null {
  if (!dateInput) return null;
  const parts = dateInput.split('-').map(Number);
  if (parts.length !== 3 || parts.some(Number.isNaN)) return null;
  const [year, month, day] = parts;
  const localEndOfDay = new Date(year, month - 1, day, 23, 59, 59, 999);
  return localEndOfDay.toISOString();
}

/** Formats a stored UTC ISO string as YYYY-MM-DD in the user's local time zone for <input type="date">. */
export function toDateInputValue(iso: string | null): string {
  if (!iso) return '';
  const d = parseUtcDate(iso);
  if (Number.isNaN(d.getTime())) return '';
  const y = d.getFullYear();
  const m = String(d.getMonth() + 1).padStart(2, '0');
  const day = String(d.getDate()).padStart(2, '0');
  return `${y}-${m}-${day}`;
}

export function isOverdue(
  dueDate: string | null,
  status: TaskStatus,
  now: Date = new Date(),
): boolean {
  if (!dueDate || status === 'Done') return false;
  return parseUtcDate(dueDate).getTime() < now.getTime();
}

/** Whole calendar days past due (0 when overdue earlier today, 1+ for previous days). */
export function overdueDays(
  dueDate: string | null,
  status: TaskStatus,
  now: Date = new Date(),
): number {
  if (!dueDate || status === 'Done') return 0;
  const due = parseUtcDate(dueDate);
  if (due.getTime() >= now.getTime()) return 0;
  const todayStart = new Date(now.getFullYear(), now.getMonth(), now.getDate()).getTime();
  const dueStart = new Date(due.getFullYear(), due.getMonth(), due.getDate()).getTime();
  return Math.max(0, Math.round((todayStart - dueStart) / MS_PER_DAY));
}

export function isDueWithinDays(
  dueDate: string | null,
  status: TaskStatus,
  days: number,
  now: Date = new Date(),
): boolean {
  if (!dueDate || status === 'Done') return false;
  const due = parseUtcDate(dueDate).getTime();
  const horizon = now.getTime() + days * MS_PER_DAY;
  return due <= horizon;
}
