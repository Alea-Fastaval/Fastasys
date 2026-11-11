import { describe, it, expect } from 'vitest';
import { conventionLocalToIso, formatClock, toConventionLocalInput, toConventionTime } from './convention-time';

describe('toConventionTime', () => {
  it('reads an instant as Copenhagen winter time (UTC+1)', () => {
    expect(toConventionTime('2027-03-24T09:00:00Z')).toEqual({
      year: 2027,
      month: 3,
      day: 24,
      weekday: 'Wednesday',
      hour: 10,
      minute: 0,
    });
  });

  it('reads an instant as Copenhagen summer time (UTC+2)', () => {
    expect(toConventionTime('2027-04-01T22:30:00Z')).toMatchObject({ day: 2, weekday: 'Friday', hour: 0, minute: 30 });
  });

  it('jumps from 01:59 to 03:00 when the clocks go forward during Fastaval (28 March 2027)', () => {
    expect(toConventionTime('2027-03-28T00:59:00Z')).toMatchObject({ weekday: 'Sunday', hour: 1, minute: 59 });
    expect(toConventionTime('2027-03-28T01:00:00Z')).toMatchObject({ weekday: 'Sunday', hour: 3, minute: 0 });
  });

  it('shows midnight as 00, not 24', () => {
    expect(formatClock(toConventionTime('2027-03-23T23:00:00Z'))).toBe('00:00');
  });
});

describe('formatClock', () => {
  it('pads hours and minutes like date("H:i")', () => {
    expect(formatClock({ year: 2027, month: 3, day: 24, weekday: 'Wednesday', hour: 9, minute: 5 })).toBe('09:05');
  });
});

describe('toConventionLocalInput', () => {
  it('gives the datetime-local value in Copenhagen time', () => {
    expect(toConventionLocalInput('2027-03-24T09:00:00Z')).toBe('2027-03-24T10:00');
  });
});

describe('conventionLocalToIso', () => {
  it('reads winter wall-clock time as UTC+1', () => {
    expect(conventionLocalToIso('2027-03-24T10:00')).toBe('2027-03-24T09:00:00.000Z');
  });

  it('reads summer wall-clock time as UTC+2', () => {
    expect(conventionLocalToIso('2027-03-28T10:00')).toBe('2027-03-28T08:00:00.000Z');
  });

  it('handles the hours around the spring change (clocks go from 02:00 to 03:00 on 28 March 2027)', () => {
    expect(conventionLocalToIso('2027-03-28T01:30')).toBe('2027-03-28T00:30:00.000Z');
    expect(conventionLocalToIso('2027-03-28T03:30')).toBe('2027-03-28T01:30:00.000Z');
    // 02:00-02:59 doesn't exist that night. Like Infosys (PHP strtotime), such a time silently becomes an hour
    // later; this was kept on purpose (2026-11-11).
    expect(conventionLocalToIso('2027-03-28T02:00')).toBe('2027-03-28T01:00:00.000Z');
    expect(conventionLocalToIso('2027-03-28T02:30')).toBe('2027-03-28T01:30:00.000Z');
    expect(conventionLocalToIso('2027-03-28T02:59')).toBe('2027-03-28T01:59:00.000Z');
  });

  it('gives a time from the skipped hour back as the hour after', () => {
    expect(toConventionLocalInput(conventionLocalToIso('2027-03-28T02:30') ?? '')).toBe('2027-03-28T03:30');
  });

  it('round-trips the times on both sides of the switch', () => {
    for (const local of ['2027-03-28T01:59', '2027-03-28T03:00', '2027-03-27T23:00', '2027-03-28T04:00']) {
      expect(toConventionLocalInput(conventionLocalToIso(local) ?? '')).toBe(local);
    }
  });

  it('reads the repeated hour in autumn as the second one (31 October 2027)', () => {
    expect(conventionLocalToIso('2027-10-31T02:30')).toBe('2027-10-31T01:30:00.000Z');
  });

  it('round-trips with toConventionLocalInput', () => {
    expect(toConventionLocalInput(conventionLocalToIso('2027-04-01T23:45') ?? '')).toBe('2027-04-01T23:45');
  });

  it('returns null for an empty or partial value', () => {
    expect(conventionLocalToIso('')).toBeNull();
    expect(conventionLocalToIso('2027-03-24')).toBeNull();
  });
});
