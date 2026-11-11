// Fastaval is held in Denmark. The API stores and returns UTC instants; the UI always shows and takes
// Danish wall-clock time, whatever time zone the browser is in.
export const CONVENTION_TIME_ZONE = 'Europe/Copenhagen';

export const WEEKDAYS = ['Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday', 'Sunday'] as const;
export type Weekday = (typeof WEEKDAYS)[number];

export interface ConventionTime {
  year: number;
  month: number;
  day: number;
  weekday: Weekday;
  hour: number;
  minute: number;
}

const formatter = new Intl.DateTimeFormat('en-US', {
  timeZone: CONVENTION_TIME_ZONE,
  year: 'numeric',
  month: '2-digit',
  day: '2-digit',
  weekday: 'long',
  hour: '2-digit',
  minute: '2-digit',
  hourCycle: 'h23',
});

// The wall-clock time in Copenhagen at the given instant
export function toConventionTime(instant: string | number | Date): ConventionTime {
  const parts = formatter.formatToParts(new Date(instant));
  const part = (type: Intl.DateTimeFormatPartTypes): string => parts.find(p => p.type === type)?.value ?? '';

  return {
    year: Number(part('year')),
    month: Number(part('month')),
    day: Number(part('day')),
    weekday: part('weekday') as Weekday,
    hour: Number(part('hour')),
    minute: Number(part('minute')),
  };
}

// "HH:mm", like PHP date('H:i')
export function formatClock(time: ConventionTime): string {
  return `${pad(time.hour)}:${pad(time.minute)}`;
}

// The value for an <input type="datetime-local">, e.g. "2027-03-24T10:00"
export function toConventionLocalInput(instant: string | number | Date): string {
  const time = toConventionTime(instant);
  return `${time.year}-${pad(time.month)}-${pad(time.day)}T${formatClock(time)}`;
}

// Reads a datetime-local value ("2027-03-24T10:00") as Copenhagen wall-clock time and returns the UTC instant
// as an ISO string, or null when the value is not a date and time. A time that doesn't exist because the
// clocks are put forward (02:00-03:00 on the last Sunday of March, which falls during Fastaval 2027) is moved
// forward by an hour without a warning, like Infosys (PHP strtotime) does; that was kept on purpose. A time that
// exists twice when the clocks are put back is read as the second one.
export function conventionLocalToIso(local: string): string | null {
  const match = /^(\d{4})-(\d{2})-(\d{2})T(\d{2}):(\d{2})$/.exec(local);
  if (!match) return null;

  const [year, month, day, hour, minute] = match.slice(1).map(Number);
  const wallAsUtc = Date.UTC(year, month - 1, day, hour, minute);

  const firstOffset = offsetMs(wallAsUtc);
  let instant = wallAsUtc - firstOffset;
  const secondOffset = offsetMs(instant);
  if (secondOffset !== firstOffset) {
    instant = wallAsUtc - secondOffset;
  }

  return new Date(instant).toISOString();
}

// How far Copenhagen is ahead of UTC at the instant, in milliseconds
function offsetMs(instant: number): number {
  const time = toConventionTime(instant);
  const wallAsUtc = Date.UTC(time.year, time.month - 1, time.day, time.hour, time.minute);
  return wallAsUtc - Math.floor(instant / 60_000) * 60_000;
}

function pad(value: number): string {
  return value.toString().padStart(2, '0');
}
