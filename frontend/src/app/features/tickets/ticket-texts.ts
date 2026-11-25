import { TicketKind, TicketStatsGroup, TicketTypeOption } from '@shared/types';
import { TranslationService } from '@shared/services';
import { Weekday, datesBetween, weekdayOfDate } from '@shared/utils';

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

// The Type column of "Alle indgangs-typer": "Indgang - Enkelt - Ons 24/03" (entrytypes.phtml)
export function ticketTypeListText(type: { name: string; day: string }, shortDay: ShortDay): string {
  return `${type.name} - ${dayWithDate(type.day, shortDay)}`;
}

// A ticket type in the participant's ticket editor: "Indgang - Partout (Ons)" (viseditmadwear.phtml). Infosys shows
// the day also for a ticket for the whole convention.
export function ticketOptionText(option: TicketTypeOption, shortDay: ShortDay): string {
  return `${option.name} (${shortDay(weekdayOfDate(option.day))})`;
}

// A row of the search box's Indgang section: "Indgang - Enkelt Ons", or the name alone for a ticket for the whole
// convention (ViewHelper::searchBox)
export function ticketSearchText(option: TicketTypeOption, shortDay: ShortDay): string {
  return option.isAllConvention ? option.name : `${option.name} ${shortDay(weekdayOfDate(option.day))}`;
}

// The heading of a group on the statistics page (Indgang::getProperType): a translation key, or null when the group's
// name is the heading. Infosys also had "Fest" for its type "Ottofest"; the name is now shown instead.
export function statsHeadingKey(group: Pick<TicketStatsGroup, 'kind' | 'isAllConvention'>): string | null {
  const kind: TicketKind = group.kind;
  if (group.isAllConvention) {
    // Like Infosys, a ticket for the whole convention that isn't a sleeping ticket is called an entrance
    return kind === 'Sleeping' ? 'tickets.stats.sleepingPartout' : 'tickets.stats.entrancePartout';
  }
  if (kind === 'Entrance') return 'tickets.stats.entranceDay';
  if (kind === 'Sleeping') return 'tickets.stats.sleepingDay';
  return null;
}

// A day on the statistics page: "Ons, 24" (Infosys DanishDayNames(date('D, j')))
export function statsDayText(day: string, shortDay: ShortDay): string {
  return `${shortDay(weekdayOfDate(day))}, ${Number(day.split('-')[2])}`;
}
