import { describe, expect, it } from 'vitest';
import { Weekday } from '@shared/utils';
import {
  foodStatsDayText,
  mealDayText,
  mealTimeText,
  newMealTimeRow,
  receivedPercent,
  toMealTimeDto,
  toMealTimeRow,
} from './food-texts';

const danish: Record<Weekday, string> = {
  Monday: 'Man',
  Tuesday: 'Tir',
  Wednesday: 'Ons',
  Thursday: 'Tor',
  Friday: 'Fre',
  Saturday: 'Lør',
  Sunday: 'Søn',
};
const shortDay = (weekday: Weekday) => danish[weekday];

describe('the food texts', () => {
  it('writes a meal time in Copenhagen time like PHP date("D H:i")', () => {
    // 16:00 UTC is 17:00 in Copenhagen before the clocks go forward, 18:00 after
    expect(mealTimeText('2027-03-24T16:00:00Z', shortDay)).toBe('Ons 17:00');
    expect(mealTimeText('2027-03-28T16:00:00Z', shortDay)).toBe('Søn 18:00');
    expect(mealDayText('2027-03-24T23:30:00Z', shortDay)).toBe('Tor');
    expect(foodStatsDayText('2027-03-24T16:00:00Z', shortDay)).toBe('Ons, 24');
  });

  it('rounds the share handed out like PHP round(x, 1)', () => {
    expect(receivedPercent(1, 3)).toBe(33.3);
    expect(receivedPercent(2, 3)).toBe(66.7);
    expect(receivedPercent(1, 8)).toBe(12.5);
    expect(receivedPercent(4, 4)).toBe(100);
    expect(receivedPercent(0, 0)).toBe(0);
  });

  it('turns a meal time into a form row of Danish day and time, and back', () => {
    const row = toMealTimeRow({
      id: 5,
      time: '2027-03-28T16:30:00Z',
      descriptionDanish: 'Med salat',
      descriptionEnglish: 'With salad',
      orderCount: 2,
    });

    expect(row).toEqual({
      id: 5,
      day: '2027-03-28',
      clock: '18:30',
      descriptionDanish: 'Med salat',
      descriptionEnglish: 'With salad',
      orderCount: 2,
      originalDay: '2027-03-28',
    });
    expect(toMealTimeDto(row)).toEqual({
      id: 5,
      time: '2027-03-28T16:30:00.000Z',
      descriptionDanish: 'Med salat',
      descriptionEnglish: 'With salad',
    });
    // A row without a day or time sends no time
    expect(toMealTimeDto(newMealTimeRow()).time).toBeNull();
  });
});
