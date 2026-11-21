import { describe, it, expect } from 'vitest';
import {
  PRONOUNS_NOT_GIVEN,
  PRONOUN_FIRST_CHOICES,
  changeFirstPronoun,
  formatBirthDate,
  formatConventionDate,
  formatConventionDateTime,
  hasSecondPronoun,
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

  it('shows one pronoun and ask', () => {
    expect(pronounText('ns', 'Danish')).toBe('De');
    expect(pronounText('ask', 'Danish')).toBe('Spørg mig');
    expect(pronounText('ask', 'English')).toBe('Ask me');
  });

  it('is blank when the pronouns are not given yet (Infosys: Ingen; changed 2026-11-21)', () => {
    expect(pronounText(PRONOUNS_NOT_GIVEN, 'Danish')).toBe('');
    expect(pronounText(PRONOUNS_NOT_GIVEN, 'English')).toBe('');
  });
});

describe('the pronoun widget', () => {
  it('splits the stored value into its two selects', () => {
    expect(toPronounChoice('nsno')).toEqual({ first: 'ns', second: 'no' });
    expect(toPronounChoice('ms')).toEqual({ first: 'ms', second: '' });
    expect(toPronounChoice('ask')).toEqual({ first: 'ask', second: '' });
  });

  it('picks nothing when the pronouns are not given yet or unknown (Infosys showed Ingen)', () => {
    expect(toPronounChoice(PRONOUNS_NOT_GIVEN)).toEqual({ first: '', second: '' });
    expect(toPronounChoice('xx')).toEqual({ first: '', second: '' });
  });

  it('cannot choose no pronouns', () => {
    expect(PRONOUN_FIRST_CHOICES).toEqual(['ns', 'no', 'fs', 'fo', 'ms', 'mo', 'ask']);
  });

  it('suggests the usual pair when the second select appears', () => {
    expect(changeFirstPronoun({ first: '', second: '' }, 'fs')).toEqual({ first: 'fs', second: 'fo' });
    expect(changeFirstPronoun({ first: 'ask', second: '' }, 'no')).toEqual({ first: 'no', second: '' });
  });

  it('keeps the second choice when the first changes, unless they would be the same', () => {
    expect(changeFirstPronoun({ first: 'ns', second: 'fo' }, 'ms')).toEqual({ first: 'ms', second: 'fo' });
    expect(changeFirstPronoun({ first: 'ns', second: 'fo' }, 'fo')).toEqual({ first: 'fo', second: '' });
  });

  it('hides the second select while nothing is picked and for ask', () => {
    expect(hasSecondPronoun({ first: '', second: '' })).toBe(false);
    expect(hasSecondPronoun({ first: 'ask', second: '' })).toBe(false);
    expect(hasSecondPronoun({ first: 'ns', second: '' })).toBe(true);
    expect(changeFirstPronoun({ first: 'ns', second: 'no' }, 'ask')).toEqual({ first: 'ask', second: '' });
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
