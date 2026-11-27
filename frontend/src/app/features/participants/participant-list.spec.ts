import { describe, it, expect } from 'vitest';
import { UnknownQueryParamError } from '@shared/services';
import { ParticipantRow } from '@shared/types';
import {
  ListColumn,
  cellText,
  columnsUsedBy,
  filterRows,
  parseSearch,
  serializeSearch,
  sortRows,
  toApiSteps,
} from './participant-list';

const row = (id: number, name: string, values: Record<string, unknown> = {}): ParticipantRow => ({ id, name, values });

describe('cellText (what Infosys shows in the list)', () => {
  const text = (column: ListColumn, value: unknown) => cellText(column, value, 'ja', 'nej');

  it('shows the stored values', () => {
    expect(text('isNoShow', true)).toBe('ja');
    expect(text('smsSignup', false)).toBe('nej');
    expect(text('activityLanguages', ['Danish', 'Scandinavian'])).toBe('dansk,skandinavisk');
    expect(text('sleepingAreas', ['Organizer', 'Sober'])).toBe('organizer,sober');
    expect(text('preferredLanguage', 'English')).toBe('en');
    expect(text('pronouns', 'nsno')).toBe('nsno');
    expect(text('amountPaid', 450)).toBe('450');
    expect(text('birthDate', '2001-02-03')).toBe('2001-02-03');
  });

  it('shows times in Danish time, and nothing when not set', () => {
    expect(text('checkedInAt', '2027-03-31T08:05:00Z')).toBe('2027-03-31 10:05');
    expect(text('checkedInAt', null)).toBe('');
    expect(text('organizerArea', null)).toBe('');
  });
});

describe('filterRows (the list’s search field)', () => {
  const rows = [row(1, 'Anna Hansen', { city: 'Aarhus' }), row(12, 'Bo Jensen', { city: 'Odense' })];
  const texts = (r: ParticipantRow) => [String(r.values['city'])];

  it('finds a full name, which Infosys could not (fixed)', () => {
    expect(filterRows(rows, 'anna hansen', texts).map(r => r.id)).toEqual([1]);
  });

  it('looks in the id, the name parts and the shown columns, without regard to case', () => {
    expect(filterRows(rows, '12', texts).map(r => r.id)).toEqual([12]);
    expect(filterRows(rows, 'JENS', texts).map(r => r.id)).toEqual([12]);
    expect(filterRows(rows, 'aarhus', texts).map(r => r.id)).toEqual([1]);
    expect(filterRows(rows, '  ', texts).map(r => r.id)).toEqual([1, 12]);
  });
});

describe('sortRows', () => {
  const rows = [
    row(1, 'Østergaard', { amountPaid: 100, isNoShow: false, city: 'Lokale 10' }),
    row(2, 'Andersen', { amountPaid: 20, isNoShow: true, city: 'Lokale 2' }),
    row(3, 'Zacho', { amountPaid: 100, isNoShow: true, city: 'Åby' }),
  ];
  const ids = (sorted: ParticipantRow[]) => sorted.map(r => r.id);

  it('keeps the id order until a column is chosen', () => {
    expect(ids(sortRows(rows, null, 'asc'))).toEqual([1, 2, 3]);
  });

  it('sorts names in Danish order and numbers as numbers; equal rows keep their id order', () => {
    expect(ids(sortRows(rows, 'name', 'asc'))).toEqual([2, 3, 1]);
    expect(ids(sortRows(rows, 'amountPaid', 'asc'))).toEqual([2, 1, 3]);
    expect(ids(sortRows(rows, 'amountPaid', 'desc'))).toEqual([1, 3, 2]);
    expect(ids(sortRows(rows, 'city', 'asc'))).toEqual([2, 1, 3]);
  });

  it('puts ja before nej', () => {
    expect(ids(sortRows(rows, 'isNoShow', 'asc'))).toEqual([2, 3, 1]);
  });
});

describe('columnsUsedBy (Infosys activateUsedFields)', () => {
  it('adds the column of each searched field that has one', () => {
    expect(
      columnsUsedBy({ firstName: 'a', categoryId: 2, organizerAreaId: 3, address: 'x', country: 'y', notes: 'z' }),
    ).toEqual(['firstName', 'category', 'organizerArea']);
  });
});

describe('the searches in the URL', () => {
  it('reads what it wrote', () => {
    const steps = [
      { quick: 'anna' },
      {
        criteria: { city: 'Aarhus', isNoShow: false, categoryId: 2, activityLanguages: ['Danish' as const] },
        logic: 'Or' as const,
        combination: 'Intersection' as const,
      },
    ];
    expect(parseSearch(serializeSearch(steps))).toEqual(steps);
  });

  it.each([
    'not json',
    '{"quick":"a"}',
    '[{"quick":5}]',
    '[{"quick":"a","extra":1}]',
    '[{"criteria":{},"logic":"Maybe","combination":"New"}]',
    '[{"criteria":{"password":"x"},"logic":"And","combination":"New"}]',
    '[{"criteria":{"categoryId":"2"},"logic":"And","combination":"New"}]',
    '[{"criteria":{"activityLanguages":["Klingon"]},"logic":"And","combination":"New"}]',
  ])('refuses %s', value => {
    expect(() => parseSearch(value)).toThrow(UnknownQueryParamError);
  });

  it('sends every field of a search to the API', () => {
    expect(toApiSteps([{ quick: 'a' }])).toEqual([{ quick: 'a', criteria: null, logic: 'And', combination: 'New' }]);
  });
});
