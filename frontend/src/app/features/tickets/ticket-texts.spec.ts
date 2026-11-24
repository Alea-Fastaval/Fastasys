import { describe, expect, it } from 'vitest';
import { Weekday } from '@shared/utils';
import {
  dayWithDate,
  statsDayText,
  statsHeadingKey,
  ticketOptionText,
  ticketSearchText,
  ticketTypeListText,
} from './ticket-texts';

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

const partout = { id: 1, name: 'Indgang - Partout', day: '2027-03-24', isAllConvention: true };
const thursday = { id: 2, name: 'Indgang - Enkelt', day: '2027-03-25', isAllConvention: false };

describe('the ticket type texts', () => {
  it('writes a day like PHP date("D d/m"), in the page language', () => {
    expect(dayWithDate('2027-03-24', shortDay)).toBe('Ons 24/03');
    expect(dayWithDate('2027-04-01', shortDay)).toBe('Tor 01/04');
  });

  it('shows "name - day" in the list of all types (entrytypes.phtml)', () => {
    expect(ticketTypeListText(thursday, shortDay)).toBe('Indgang - Enkelt - Tor 25/03');
    // Infosys shows the day also for a ticket for the whole convention
    expect(ticketTypeListText(partout, shortDay)).toBe('Indgang - Partout - Ons 24/03');
  });

  it('shows "name (day)" in the participant ticket editor (viseditmadwear.phtml)', () => {
    expect(ticketOptionText(thursday, shortDay)).toBe('Indgang - Enkelt (Tor)');
    expect(ticketOptionText(partout, shortDay)).toBe('Indgang - Partout (Ons)');
  });

  it('leaves the day out of the search row of a ticket for the whole convention (ViewHelper)', () => {
    expect(ticketSearchText(thursday, shortDay)).toBe('Indgang - Enkelt Tor');
    expect(ticketSearchText(partout, shortDay)).toBe('Indgang - Partout');
  });

  it('writes a statistics day like DanishDayNames(date("D, j"))', () => {
    expect(statsDayText('2027-03-24', shortDay)).toBe('Ons, 24');
    expect(statsDayText('2027-04-01', shortDay)).toBe('Tor, 1');
  });

  it('makes the statistics headings like Indgang::getProperType', () => {
    expect(statsHeadingKey({ kind: 'Sleeping', isAllConvention: true })).toBe('tickets.stats.sleepingPartout');
    expect(statsHeadingKey({ kind: 'Entrance', isAllConvention: true })).toBe('tickets.stats.entrancePartout');
    // Infosys calls any ticket for the whole convention that isn't for sleeping an entrance
    expect(statsHeadingKey({ kind: 'Other', isAllConvention: true })).toBe('tickets.stats.entrancePartout');
    expect(statsHeadingKey({ kind: 'Entrance', isAllConvention: false })).toBe('tickets.stats.entranceDay');
    expect(statsHeadingKey({ kind: 'Sleeping', isAllConvention: false })).toBe('tickets.stats.sleepingDay');
    // Otherwise the name is the heading
    expect(statsHeadingKey({ kind: 'Other', isAllConvention: false })).toBeNull();
    expect(statsHeadingKey({ kind: 'AleaMembership', isAllConvention: false })).toBeNull();
  });
});
