import { UnknownQueryParamError } from '@shared/services';
import {
  BIRTH_DATE_RANGES,
  ParticipantCriteria,
  ParticipantRow,
  ParticipantSearchStep,
  PARTICIPANT_LANGUAGES,
  SEARCH_COMBINATIONS,
  SEARCH_LOGICS,
} from '@shared/types';
import { formatConventionDateTime } from './participant-format';

// The columns that can be added to the Infosys participant list ("Tilføj/fjern kolonne"), as the API names them
export const LIST_COLUMNS = [
  'firstName',
  'lastName',
  'email',
  'phoneNumber',
  'addressLine1',
  'zipCode',
  'city',
  'smsSignup',
  'activityLanguages',
  'category',
  'isAuthorOrDesigner',
  'amountPaid',
  'createdAt',
  'organizerArea',
  'game',
  'isNoShow',
  'youthClub',
  'setupHelpMonday',
  'setupHelpTuesday',
  'checkedInAt',
  'desiredActivities',
  'birthDate',
  'updatedAt',
  'signedUpAt',
  'isAnnulled',
  'desiredHeroTasks',
  'nickname',
  'financialSupport',
  'preferredLanguage',
  'country',
  'superGameMaster',
  'maxGameMasterRuns',
  'superRulesGuide',
  'maxRulesGuideGames',
  'pronouns',
  'sleepingAreas',
  'adminNote',
  'messageToParticipant',
  'paymentNote',
  'commentNote',
  'heroForceNote',
  'juniorContact',
  'juniorComment',
  'sleepingNote',
] as const;
export type ListColumn = (typeof LIST_COLUMNS)[number];

// The notes are only for users who may edit participants, as on the participant page
export const EDITOR_COLUMNS: readonly ListColumn[] = [
  'adminNote',
  'messageToParticipant',
  'paymentNote',
  'commentNote',
  'heroForceNote',
  'juniorContact',
  'juniorComment',
  'sleepingNote',
];

type ColumnKind =
  'text' | 'number' | 'yesNo' | 'date' | 'instant' | 'languages' | 'sleepingAreas' | 'preferredLanguage';

const KINDS: Partial<Record<ListColumn, ColumnKind>> = {
  smsSignup: 'yesNo',
  isAuthorOrDesigner: 'yesNo',
  isNoShow: 'yesNo',
  setupHelpMonday: 'yesNo',
  setupHelpTuesday: 'yesNo',
  isAnnulled: 'yesNo',
  financialSupport: 'yesNo',
  superGameMaster: 'yesNo',
  superRulesGuide: 'yesNo',
  amountPaid: 'number',
  desiredActivities: 'number',
  desiredHeroTasks: 'number',
  maxGameMasterRuns: 'number',
  maxRulesGuideGames: 'number',
  birthDate: 'date',
  createdAt: 'instant',
  updatedAt: 'instant',
  signedUpAt: 'instant',
  checkedInAt: 'instant',
  activityLanguages: 'languages',
  sleepingAreas: 'sleepingAreas',
  preferredLanguage: 'preferredLanguage',
};

const kindOf = (column: ListColumn): ColumnKind => KINDS[column] ?? 'text';

// Infosys deltagere.sprog values
const LANGUAGE_VALUES: Record<string, string> = { Danish: 'dansk', English: 'engelsk', Scandinavian: 'skandinavisk' };

// The text Infosys shows in the list: the stored value (ja/nej, the set values, da/en, the pronoun codes), with
// times in Danish time. A time that isn't set is empty instead of 0000-00-00 00:00:00.
export function cellText(column: ListColumn, value: unknown, yes: string, no: string): string {
  if (value === null || value === undefined) return '';
  switch (kindOf(column)) {
    case 'yesNo':
      return value ? yes : no;
    case 'instant':
      return formatConventionDateTime(scalarText(value));
    case 'languages':
      return (value as string[]).map(l => LANGUAGE_VALUES[l] ?? l).join(',');
    case 'sleepingAreas':
      return (value as string[]).map(a => a.toLowerCase()).join(',');
    case 'preferredLanguage':
      return value === 'English' ? 'en' : 'da';
    default:
      return scalarText(value);
  }
}

// The API sends the other columns as text, numbers or true/false
function scalarText(value: unknown): string {
  return typeof value === 'string' || typeof value === 'number' || typeof value === 'boolean' ? String(value) : '';
}

export const SORT_DIRECTIONS = ['asc', 'desc'] as const;
export type SortDirection = (typeof SORT_DIRECTIONS)[number];
export const SORT_KEYS = ['id', 'name', ...LIST_COLUMNS] as const;
export type SortKey = (typeof SORT_KEYS)[number];

const danish = new Intl.Collator('da', { numeric: true });

// Like the list's columns in Infosys: numbers and times in their own order, ja before nej, text in Danish order.
// Rows that are equal keep their id order.
export function sortRows(rows: ParticipantRow[], key: SortKey | null, direction: SortDirection): ParticipantRow[] {
  if (!key) return rows;
  const sign = direction === 'desc' ? -1 : 1;
  const compare = comparer(key);
  return [...rows].sort((a, b) => sign * compare(a, b) || a.id - b.id);
}

function comparer(key: SortKey): (a: ParticipantRow, b: ParticipantRow) => number {
  if (key === 'id') return (a, b) => a.id - b.id;
  if (key === 'name') return (a, b) => danish.compare(a.name, b.name);

  const value = (row: ParticipantRow) => row.values[key];
  switch (kindOf(key)) {
    case 'number':
      return (a, b) => num(value(a)) - num(value(b));
    case 'instant':
      return (a, b) => time(value(a)) - time(value(b));
    case 'yesNo':
      return (a, b) => Number(Boolean(value(b))) - Number(Boolean(value(a)));
    default:
      return (a, b) => danish.compare(cellText(key, value(a), 'ja', 'nej'), cellText(key, value(b), 'ja', 'nej'));
  }
}

const num = (value: unknown) => (typeof value === 'number' ? value : -Infinity);
const time = (value: unknown) => (typeof value === 'string' ? Date.parse(value) : -Infinity);

// The list's search field. Infosys looked for the text in the first and the last name separately, so a full name
// like "Anna Hansen" found nothing; the whole name is searched now (fixed on 2026-11-11). Like Infosys it also
// looks in the id and the shown columns, without regard to case.
export function filterRows(
  rows: ParticipantRow[],
  filter: string,
  texts: (row: ParticipantRow) => string[],
): ParticipantRow[] {
  const needle = filter.trim().toLocaleLowerCase('da');
  if (!needle) return rows;
  return rows.filter(row =>
    [String(row.id), row.name, ...texts(row)].some(text => text.toLocaleLowerCase('da').includes(needle)),
  );
}

// Infosys adds the column of each field used in the search box to the list (activateUsedFields). Fields without a
// column of the same name (ID, Adresse, Land, Sprog, Scenarie, Noter, Super Heltetjanser) add nothing.
const CRITERIA_COLUMNS: Partial<Record<keyof ParticipantCriteria, ListColumn>> = {
  birthDate: 'birthDate',
  categoryId: 'category',
  firstName: 'firstName',
  lastName: 'lastName',
  nickname: 'nickname',
  email: 'email',
  amountPaid: 'amountPaid',
  zipCode: 'zipCode',
  city: 'city',
  phoneNumber: 'phoneNumber',
  smsSignup: 'smsSignup',
  desiredHeroTasks: 'desiredHeroTasks',
  setupHelpMonday: 'setupHelpMonday',
  setupHelpTuesday: 'setupHelpTuesday',
  isAuthorOrDesigner: 'isAuthorOrDesigner',
  desiredActivities: 'desiredActivities',
  superGameMaster: 'superGameMaster',
  financialSupport: 'financialSupport',
  isNoShow: 'isNoShow',
  isAnnulled: 'isAnnulled',
  youthClub: 'youthClub',
  organizerAreaId: 'organizerArea',
  checkedInAt: 'checkedInAt',
};

export function columnsUsedBy(criteria: ParticipantCriteria): ListColumn[] {
  return (Object.keys(criteria) as (keyof ParticipantCriteria)[])
    .map(field => CRITERIA_COLUMNS[field])
    .filter((column): column is ListColumn => column !== undefined);
}

// The searches are kept in the URL as JSON (?search=[...]); the session kept them in Infosys
export function serializeSearch(steps: ParticipantSearchStep[]): string {
  return JSON.stringify(steps);
}

// The API takes every field of a search, also the unused ones
export function toApiSteps(steps: ParticipantSearchStep[]) {
  return steps.map(step =>
    'quick' in step
      ? { quick: step.quick, criteria: null, logic: 'And', combination: 'New' }
      : { quick: null, criteria: step.criteria, logic: step.logic, combination: step.combination },
  );
}

const TEXT_CRITERIA = [
  'id',
  'birthDate',
  'firstName',
  'lastName',
  'nickname',
  'email',
  'amountPaid',
  'address',
  'zipCode',
  'city',
  'country',
  'phoneNumber',
  'youthClub',
  'game',
  'checkedInAt',
  'notes',
];
const NUMBER_CRITERIA = ['categoryId', 'desiredHeroTasks', 'desiredActivities', 'organizerAreaId'];
const BOOLEAN_CRITERIA = [
  'smsSignup',
  'superHero',
  'setupHelpMonday',
  'setupHelpTuesday',
  'isAuthorOrDesigner',
  'superGameMaster',
  'financialSupport',
  'isNoShow',
  'isAnnulled',
];

// Reads ?search=; a value that isn't a list of searches the page knows throws an UnknownQueryParamError
export function parseSearch(value: string): ParticipantSearchStep[] {
  const unknown = () => new UnknownQueryParamError('search', value);
  let parsed: unknown;
  try {
    parsed = JSON.parse(value);
  } catch {
    throw unknown();
  }
  if (!Array.isArray(parsed) || !parsed.every(isStep)) throw unknown();
  return parsed;
}

const isObject = (value: unknown): value is Record<string, unknown> =>
  typeof value === 'object' && value !== null && !Array.isArray(value);

function isStep(step: unknown): step is ParticipantSearchStep {
  if (!isObject(step)) return false;
  if ('quick' in step) return Object.keys(step).length === 1 && typeof step['quick'] === 'string';
  return (
    Object.keys(step).length === 3 &&
    (SEARCH_LOGICS as readonly unknown[]).includes(step['logic']) &&
    (SEARCH_COMBINATIONS as readonly unknown[]).includes(step['combination']) &&
    isCriteria(step['criteria'])
  );
}

function isCriteria(criteria: unknown): criteria is ParticipantCriteria {
  if (!isObject(criteria)) return false;
  return Object.entries(criteria).every(([field, value]) => {
    if (TEXT_CRITERIA.includes(field)) return typeof value === 'string';
    if (NUMBER_CRITERIA.includes(field)) return Number.isInteger(value);
    if (BOOLEAN_CRITERIA.includes(field)) return typeof value === 'boolean';
    if (field === 'birthDateRange') return (BIRTH_DATE_RANGES as readonly unknown[]).includes(value);
    if (field === 'activityLanguages') {
      return Array.isArray(value) && value.every(l => (PARTICIPANT_LANGUAGES as readonly unknown[]).includes(l));
    }
    return false;
  });
}
