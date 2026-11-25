import { MealTime, MealTimeFieldsDto } from '@shared/types';
import { conventionLocalToIso, formatClock, toConventionLocalInput, toConventionTime } from '@shared/utils';
import { ShortDay } from '../tickets/ticket-texts';

// A meal time on a food type's page: "Ons 17:00" in Copenhagen time (Infosys showfood.phtml, date('D H:i'))
export function mealTimeText(time: string, shortDay: ShortDay): string {
  const local = toConventionTime(time);
  return `${shortDay(local.weekday)} ${formatClock(local)}`;
}

// A meal time in the participant's Mad selects and list: the day only, "Ons" (Infosys mad/ajaxgetmadtider, date('D'))
export function mealDayText(time: string, shortDay: ShortDay): string {
  return shortDay(toConventionTime(time).weekday);
}

// A meal time on the statistics page: "Ons, 24" (Infosys foodstats.phtml, DanishDayNames(date('D, j')))
export function foodStatsDayText(time: string, shortDay: ShortDay): string {
  const local = toConventionTime(time);
  return `${shortDay(local.weekday)}, ${local.day}`;
}

// The share handed out, like PHP round($received / $total * 100, 1); 0 when nothing was ordered
export function receivedPercent(received: number, total: number): number {
  return total > 0 ? Math.round((received / total) * 1000) / 10 : 0;
}

// A meal time on the create/edit form: "Dag" (a convention day), "Tid" (HH:mm) and the descriptions, in Danish time
export interface MealTimeRow {
  id: number | null;
  // "yyyy-mm-dd", or '' while not chosen
  day: string;
  // "HH:mm", or '' while not chosen
  clock: string;
  descriptionDanish: string;
  descriptionEnglish: string;
  // Participants who have ordered it; an ordered meal time can't be removed
  orderCount: number;
  // The day it had when the page was opened, which stays choosable when it is outside the convention
  originalDay: string | null;
}

export function toMealTimeRow(mealTime: MealTime): MealTimeRow {
  const [day, clock] = toConventionLocalInput(mealTime.time).split('T');
  return {
    id: mealTime.id,
    day,
    clock,
    descriptionDanish: mealTime.descriptionDanish,
    descriptionEnglish: mealTime.descriptionEnglish,
    orderCount: mealTime.orderCount,
    originalDay: day,
  };
}

// Infosys's "Tilføj" adds an empty meal time
export function newMealTimeRow(): MealTimeRow {
  return {
    id: null,
    day: '',
    clock: '',
    descriptionDanish: '',
    descriptionEnglish: '',
    orderCount: 0,
    originalDay: null,
  };
}

// The day and time are Danish wall-clock time; the API stores the instant (a time skipped when the clocks go forward
// moves an hour on, see conventionLocalToIso)
export function toMealTimeDto(row: MealTimeRow): MealTimeFieldsDto {
  return {
    id: row.id,
    time: row.day !== '' && row.clock !== '' ? conventionLocalToIso(`${row.day}T${row.clock}`) : null,
    descriptionDanish: row.descriptionDanish,
    descriptionEnglish: row.descriptionEnglish,
  };
}
