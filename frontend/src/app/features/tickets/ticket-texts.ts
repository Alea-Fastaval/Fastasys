import { TicketKind, TicketStatsGroup, TicketStatsType, TicketTypeOption } from '@shared/types';
import { TranslationService } from '@shared/services';
import { Weekday, datesBetween, weekdayOfDate } from '@shared/utils';

// Whether a type is for one day of its own, picked in the form's "Dag": not partout, and not an Alea membership
// (agreed 2026-11-25). The API gives the others the convention's first day, and the pages don't show it.
export function hasOwnDay(type: { kind: TicketKind | null; isAllConvention: boolean }): boolean {
  return !type.isAllConvention && type.kind !== 'AleaMembership';
}

// The name of a day in the page's language, e.g. "Ons" or "Onsdag"
export type ShortDay = (weekday: Weekday) => string;

// Infosys writes its day names with PHP date('D'), in English on some pages and in Danish on others. Fastasys uses the
// page's language everywhere (common.weekdaysShort).
export function translatedShortDay(translation: TranslationService): ShortDay {
  return translatedDayName(translation, 'common.weekdaysShort');
}

// The full name of a day in the page's language, e.g. "Onsdag" (common.weekdays)
export function translatedLongDay(translation: TranslationService): ShortDay {
  return translatedDayName(translation, 'common.weekdays');
}

function translatedDayName(translation: TranslationService, key: string): ShortDay {
  return weekday => {
    // Reading the signal makes a template update when the language changes, like the translate pipe
    translation.translations();
    return translation.translate(`${key}.${weekday}`);
  };
}

// "Ons 24/03" (Infosys date('D d/m')), or "Onsdag 24/03" with the full day names
export function dayWithDate(day: string, dayName: ShortDay): string {
  const [, month, dayOfMonth] = day.split('-');
  return `${dayName(weekdayOfDate(day))} ${dayOfMonth}/${month}`;
}

// The days the form's "Dag" offers: the convention's days (agreed 2026-11-25), plus the type's own day when it is
// outside them, so editing doesn't change it. No days when the convention's dates aren't known.
export function ticketDayOptions(start: string | null, end: string | null, current: string | null): string[] {
  const days = start && end ? datesBetween(start, end) : [];
  return current && !days.includes(current) ? [...days, current].sort() : days;
}

// The Type column of "Alle indgangs-typer": "Indgang - Enkelt - Ons 24/03" (entrytypes.phtml). Infosys showed a day
// for every type; a type without a day of its own shows "Partout", or only its name for an Alea membership
// (agreed 2026-11-25).
export function ticketTypeListText(
  type: { name: string; day: string; kind: TicketKind; isAllConvention: boolean },
  shortDay: ShortDay,
  partout: string,
): string {
  if (hasOwnDay(type)) return `${type.name} - ${dayWithDate(type.day, shortDay)}`;
  return type.kind === 'AleaMembership' ? type.name : `${type.name} - ${partout}`;
}

// A ticket type in the participant's ticket editor: "Indgang - Enkelt (Ons)" (viseditmadwear.phtml), "(Partout)" for a
// ticket for the whole convention, or only the name for an Alea membership (Infosys showed a day for every type)
export function ticketOptionText(option: TicketTypeOption, shortDay: ShortDay, partout: string): string {
  if (hasOwnDay(option)) return `${option.name} (${shortDay(weekdayOfDate(option.day))})`;
  return option.kind === 'AleaMembership' ? option.name : `${option.name} (${partout})`;
}

// A row of the search box's Indgang section: "Indgang - Enkelt Ons", or the name alone for a ticket for the whole
// convention (ViewHelper::searchBox) and for an Alea membership
export function ticketSearchText(option: TicketTypeOption, shortDay: ShortDay): string {
  return hasOwnDay(option) ? `${option.name} ${shortDay(weekdayOfDate(option.day))}` : option.name;
}

// The line under a heading on the statistics page: what kind of tickets they are, like Indgang::getProperType. The
// heading itself is the name (agreed 2026-11-25): Infosys's heading was only this, so an Alea membership or an
// "Andet" type for the whole convention looked like "Indgang, partout".
export function statsKindKey(group: Pick<TicketStatsGroup, 'kind' | 'isAllConvention'>): string {
  switch (group.kind) {
    case 'AleaMembership':
      return 'tickets.kinds.AleaMembership';
    case 'Other':
      return 'tickets.kinds.Other';
    case 'Sleeping':
      return group.isAllConvention ? 'tickets.stats.sleepingPartout' : 'tickets.stats.sleepingDay';
    default:
      return group.isAllConvention ? 'tickets.stats.entrancePartout' : 'tickets.stats.entranceDay';
  }
}

// A day on the statistics page: "Ons, 24" (Infosys DanishDayNames(date('D, j')))
export function statsDayText(day: string, shortDay: ShortDay): string {
  return `${shortDay(weekdayOfDate(day))}, ${Number(day.split('-')[2])}`;
}

// A type under a heading on the statistics page: its day, "Partout", or "Antal solgt" for an Alea membership
export function statsTypeText(
  type: TicketStatsType,
  shortDay: ShortDay,
  texts: { partout: string; soldCount: string },
): string {
  if (hasOwnDay(type)) return statsDayText(type.day, shortDay);
  return type.kind === 'AleaMembership' ? texts.soldCount : texts.partout;
}
