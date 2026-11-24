import { TicketKind, TicketStatsGroup, TicketTypeOption } from '@shared/types';
import { TranslationService } from '@shared/services';
import { Weekday, weekdayOfDate } from '@shared/utils';

// The short name of a day in the page's language, e.g. "Ons"
export type ShortDay = (weekday: Weekday) => string;

// Infosys writes its day names with PHP date('D'), in English on some pages and in Danish on others. Fastasys uses the
// page's language everywhere (common.weekdaysShort).
export function translatedShortDay(translation: TranslationService): ShortDay {
  return weekday => {
    // Reading the signal makes a template update when the language changes, like the translate pipe
    translation.translations();
    return translation.translate(`common.weekdaysShort.${weekday}`);
  };
}

// "Ons 24/03" (Infosys date('D d/m'))
export function dayWithDate(day: string, shortDay: ShortDay): string {
  const [, month, dayOfMonth] = day.split('-');
  return `${shortDay(weekdayOfDate(day))} ${dayOfMonth}/${month}`;
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
