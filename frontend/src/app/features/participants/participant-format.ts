import { PreferredLanguage } from '@shared/types';
import { formatClock, toConventionTime } from '@shared/utils';

// Infosys Deltagere::getPronoun. The words are in the participant's own preferred language, as in Infosys.
const PRONOUN_WORDS: Record<PreferredLanguage, Record<string, string>> = {
  Danish: { none: 'Ingen', ask: 'Spørg mig', ns: 'De', no: 'Dem', fs: 'Hun', fo: 'Hende', ms: 'Han', mo: 'Ham' },
  English: { none: 'None', ask: 'Ask me', ns: 'They', no: 'Them', fs: 'She', fo: 'Her', ms: 'He', mo: 'Him' },
};

// The first select of the Infosys pronoun widget, in its order; the second has the codes only
export const PRONOUN_FIRST_CHOICES = ['none', 'ns', 'no', 'fs', 'fo', 'ms', 'mo', 'ask'] as const;
export const PRONOUN_CODES = ['ns', 'no', 'fs', 'fo', 'ms', 'mo'] as const;

// When the widget shows its second select, it suggests the usual pair
const PRONOUN_PAIRS: Record<string, string> = { ns: 'no', fs: 'fo', ms: 'mo' };

export function pronounWord(code: string, language: PreferredLanguage): string {
  return PRONOUN_WORDS[language][code] ?? code;
}

// "Ingen", "Spørg mig", "Hun" or "Hun/Hende"
export function pronounText(pronouns: string, language: PreferredLanguage): string {
  // "none" is four letters too, so it is checked first, as in Infosys
  if (pronouns === 'none' || pronouns === 'ask') return pronounWord(pronouns, language);
  if (pronouns.length === 4) {
    return `${pronounWord(pronouns.slice(0, 2), language)}/${pronounWord(pronouns.slice(2), language)}`;
  }
  return pronounWord(pronouns, language);
}

export interface PronounChoice {
  first: string;
  second: string;
}

// Splits a stored value into the widget's two selects; an unknown value shows as "none", like the widget
export function toPronounChoice(pronouns: string): PronounChoice {
  if (pronouns === 'none' || pronouns === 'ask') return { first: pronouns, second: '' };
  const isCode = (value: string) => (PRONOUN_CODES as readonly string[]).includes(value);
  if (pronouns.length === 2 && isCode(pronouns)) return { first: pronouns, second: '' };
  if (pronouns.length === 4 && isCode(pronouns.slice(0, 2)) && isCode(pronouns.slice(2))) {
    return { first: pronouns.slice(0, 2), second: pronouns.slice(2) };
  }
  return { first: 'none', second: '' };
}

// The widget hides the second select for "none" and "ask"; when it appears it suggests the pair
export function changeFirstPronoun(choice: PronounChoice, first: string): PronounChoice {
  if (first === 'none' || first === 'ask') return { first, second: '' };
  const secondWasHidden = choice.first === 'none' || choice.first === 'ask';
  const second = secondWasHidden ? (PRONOUN_PAIRS[first] ?? '') : choice.second === first ? '' : choice.second;
  return { first, second };
}

export function pronounValue(choice: PronounChoice): string {
  return choice.first === 'none' || choice.first === 'ask' ? choice.first : choice.first + choice.second;
}

// Infosys shows birth dates as d-m-Y, e.g. "03-02-2001"
export function formatBirthDate(date: string | null): string {
  if (!date) return '';
  const [year, month, day] = date.split('-');
  return `${day}-${month}-${year}`;
}

// Infosys "Tilmeldt": date('Y-m-d') in Danish time
export function formatConventionDate(instant: string): string {
  const time = toConventionTime(instant);
  return `${time.year}-${pad(time.month)}-${pad(time.day)}`;
}

// Infosys "Checked in": date('Y-m-d H:i') in Danish time
export function formatConventionDateTime(instant: string): string {
  return `${formatConventionDate(instant)} ${formatClock(toConventionTime(instant))}`;
}

// Infosys sorts countries, organizer areas and games with utf8mb4_danish_ci (æ, ø, å after z)
const danish = new Intl.Collator('da');

export function sortByDanish<T>(items: readonly T[], name: (item: T) => string): T[] {
  return [...items].sort((a, b) => danish.compare(name(a), name(b)));
}

function pad(value: number): string {
  return value.toString().padStart(2, '0');
}
