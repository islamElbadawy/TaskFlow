import { isOverdue, overdueDays, parseUtcDate, toDateInputValue, toEndOfDayUtc } from './due-date';

describe('due-date', () => {
  it('normalizes ISO timestamps missing a trailing Z as UTC', () => {
    const withZ = parseUtcDate('2026-11-21T12:00:00Z');
    const withoutZ = parseUtcDate('2026-11-21T12:00:00');
    expect(withoutZ.getTime()).toBe(withZ.getTime());
  });

  it('round-trips calendar day through end-of-day UTC and local date input', () => {
    const iso = toEndOfDayUtc('2026-11-25');
    expect(iso).not.toBeNull();
    expect(toDateInputValue(iso)).toBe('2026-11-25');
    expect(toEndOfDayUtc(null)).toBeNull();
    expect(toEndOfDayUtc('')).toBeNull();
  });

  it('never marks Done tasks as overdue', () => {
    const now = new Date('2026-11-21T12:00:00Z');
    expect(isOverdue('2026-11-18T23:59:59Z', 'InProgress', now)).toBe(true);
    expect(isOverdue('2026-11-18T23:59:59Z', 'Done', now)).toBe(false);
    expect(overdueDays('2026-11-18T23:59:59Z', 'Done', now)).toBe(0);
    expect(overdueDays('2026-11-18T12:00:00Z', 'Todo', now)).toBeGreaterThanOrEqual(2);
    expect(overdueDays('2026-11-21T08:00:00Z', 'Todo', now)).toBe(0);
  });
});
