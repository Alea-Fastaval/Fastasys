import { describe, it, expect } from 'vitest';
import {
  changeFirstPronoun,
  formatBirthDate,
  formatConventionDate,
  formatConventionDateTime,
  pronounText,
  pronounValue,
  sortByDanish,
  toPronounChoice,
} from './participant-format';

describe('pronounText (Infosys Deltagere::getPronoun)', () => {
  it('uses the participant’s own language', () => {
    expect(pronounText('fsfo', 'Danish')).toBe('Hun/Hende');
    expect(pronounText('fsfo', 'English')).toBe('She/Her');
  });

  it('shows one pronoun, none and ask', () => {
    expect(pronounText('ns', 'Danish')).toBe('De');
    expect(pronounText('none', 'Danish')).toBe('Ingen');
    expect(pronounText('ask', 'Danish')).toBe('Spørg mig');
    expect(pronounText('ask', 'English')).toBe('Ask me');
  });
});

describe('the pronoun widget', () => {
  it('splits the stored value into its two selects', () => {
    expect(toPronounChoice('nsno')).toEqual({ first: 'ns', second: 'no' });
    expect(toPronounChoice('ms')).toEqual({ first: 'ms', second: '' });
    expect(toPronounChoice('ask')).toEqual({ first: 'ask', second: '' });
    // An unknown value shows as Ingen, like the widget
    expect(toPronounChoice('xx')).toEqual({ first: 'none', second: '' });
  });

  it('suggests the usual pair when the second select appears', () => {
    expect(changeFirstPronoun({ first: 'none', second: '' }, 'fs')).toEqual({ first: 'fs', second: 'fo' });
    expect(changeFirstPronoun({ first: 'ask', second: '' }, 'no')).toEqual({ first: 'no', second: '' });
  });

  it('keeps the second choice when the first changes, unless they would be the same', () => {
    expect(changeFirstPronoun({ first: 'ns', second: 'fo' }, 'ms')).toEqual({ first: 'ms', second: 'fo' });
    expect(changeFirstPronoun({ first: 'ns', second: 'fo' }, 'fo')).toEqual({ first: 'fo', second: '' });
  });

  it('hides the second select for none and ask', () => {
    expect(changeFirstPronoun({ first: 'ns', second: 'no' }, 'none')).toEqual({ first: 'none', second: '' });
    expect(pronounValue({ first: 'ask', second: 'no' })).toBe('ask');
    expect(pronounValue({ first: 'ns', second: 'no' })).toBe('nsno');
    expect(pronounValue({ first: 'ns', second: '' })).toBe('ns');
  });
});

describe('dates', () => {
  it('shows a birth date as d-m-Y and nothing when it is unknown', () => {
    expect(formatBirthDate('2001-02-03')).toBe('03-02-2001');
    expect(formatBirthDate(null)).toBe('');
  });

  it('shows the signup and check-in time in Danish time', () => {
    // 23:30 UTC on 31 December is already the next day in Copenhagen
    expect(formatConventionDate('2026-12-31T23:30:00Z')).toBe('2027-01-01');
    // Fastaval 2027 is in summer time after 28 March
    expect(formatConventionDateTime('2027-03-31T08:05:00Z')).toBe('2027-03-31 10:05');
  });
});

describe('sortByDanish', () => {
  it('puts æ, ø and å after z, like utf8mb4_danish_ci', () => {
    const sorted = sortByDanish(['Åland', 'Zambia', 'Ægypten', 'Østrig', 'Danmark'], name => name);
    expect(sorted).toEqual(['Danmark', 'Zambia', 'Ægypten', 'Østrig', 'Åland']);
  });
});
