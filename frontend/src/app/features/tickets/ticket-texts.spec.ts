import { describe, expect, it } from 'vitest';
import { Weekday } from '@shared/utils';
import { TicketKind, TicketTypeOption } from '@shared/types';
import {
  dayWithDate,
  hasOwnDay,
  statsDayText,
  statsKindKey,
  statsTypeText,
  ticketDayOptions,
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

const partout: TicketTypeOption = {
  id: 1,
  name: 'Indgang - Partout',
  day: '2027-03-24',
  kind: 'Entrance',
  isAllConvention: true,
};
const thursday: TicketTypeOption = {
  id: 2,
  name: 'Indgang - Enkelt',
  day: '2027-03-25',
  kind: 'Entrance',
  isAllConvention: false,
};
const membership: TicketTypeOption = {
  id: 3,
  name: 'Alea medlemskab',
  day: '2027-03-24',
  kind: 'AleaMembership',
  isAllConvention: false,
};

describe('hasOwnDay', () => {
  it('is false for a partout type and for an Alea membership (2026-11-25)', () => {
    const type = (kind: TicketKind, isAllConvention: boolean) => hasOwnDay({ kind, isAllConvention });
    expect(type('Entrance', false)).toBe(true);
    expect(type('Sleeping', false)).toBe(true);
    expect(type('Other', false)).toBe(true);
    expect(type('Entrance', true)).toBe(false);
    expect(type('Other', true)).toBe(false);
    expect(type('AleaMembership', false)).toBe(false);
    // The form before a kind is chosen
    expect(hasOwnDay({ kind: null, isAllConvention: false })).toBe(true);
  });
});

describe('the ticket type texts', () => {
  it('writes a day like PHP date("D d/m"), in the page language', () => {
    expect(dayWithDate('2027-03-24', shortDay)).toBe('Ons 24/03');
    expect(dayWithDate('2027-04-01', shortDay)).toBe('Tor 01/04');
    // The form's "Dag" uses the full day names
    expect(dayWithDate('2027-03-24', () => 'Onsdag')).toBe('Onsdag 24/03');
  });

  it("offers the convention's days, and keeps a type's own day outside them", () => {
    expect(ticketDayOptions('2027-03-24', '2027-03-26', null)).toEqual(['2027-03-24', '2027-03-25', '2027-03-26']);
    expect(ticketDayOptions('2027-03-24', '2027-03-26', '2027-03-25')).toEqual([
      '2027-03-24',
      '2027-03-25',
      '2027-03-26',
    ]);
    expect(ticketDayOptions('2027-03-24', '2027-03-26', '2027-02-03')).toEqual([
      '2027-02-03',
      '2027-03-24',
      '2027-03-25',
      '2027-03-26',
    ]);
    // Without the convention's dates there is nothing to pick, apart from the type's own day
    expect(ticketDayOptions(null, '2027-03-26', null)).toEqual([]);
    expect(ticketDayOptions(null, null, '2027-03-25')).toEqual(['2027-03-25']);
  });

  it('shows "name - day" in the list of all types (entrytypes.phtml)', () => {
    expect(ticketTypeListText(thursday, shortDay, 'Partout')).toBe('Indgang - Enkelt - Tor 25/03');
    // Infosys showed the day also for a ticket for the whole convention, which has no day of its own (2026-11-25)
    expect(ticketTypeListText({ ...partout, name: 'Hele Fastaval' }, shortDay, 'Partout')).toBe(
      'Hele Fastaval - Partout',
    );
    // An Alea membership isn't for a day at all
    expect(ticketTypeListText(membership, shortDay, 'Partout')).toBe('Alea medlemskab');
    expect(ticketTypeListText({ ...membership, isAllConvention: true }, shortDay, 'Partout')).toBe('Alea medlemskab');
  });

  it('shows "name (day)" in the participant ticket editor (viseditmadwear.phtml)', () => {
    expect(ticketOptionText(thursday, shortDay, 'Partout')).toBe('Indgang - Enkelt (Tor)');
    expect(ticketOptionText({ ...partout, name: 'Hele Fastaval' }, shortDay, 'Partout')).toBe(
      'Hele Fastaval (Partout)',
    );
    expect(ticketOptionText(membership, shortDay, 'Partout')).toBe('Alea medlemskab');
  });

  it('leaves the day out of the search row of a type without a day of its own (ViewHelper)', () => {
    expect(ticketSearchText(thursday, shortDay)).toBe('Indgang - Enkelt Tor');
    expect(ticketSearchText(partout, shortDay)).toBe('Indgang - Partout');
    expect(ticketSearchText(membership, shortDay)).toBe('Alea medlemskab');
  });

  it('writes a statistics day like DanishDayNames(date("D, j"))', () => {
    expect(statsDayText('2027-03-24', shortDay)).toBe('Ons, 24');
    expect(statsDayText('2027-04-01', shortDay)).toBe('Tor, 1');
  });

  it('describes the kind under a statistics heading like Indgang::getProperType', () => {
    expect(statsKindKey({ kind: 'Sleeping', isAllConvention: true })).toBe('tickets.stats.sleepingPartout');
    expect(statsKindKey({ kind: 'Entrance', isAllConvention: true })).toBe('tickets.stats.entrancePartout');
    expect(statsKindKey({ kind: 'Entrance', isAllConvention: false })).toBe('tickets.stats.entranceDay');
    expect(statsKindKey({ kind: 'Sleeping', isAllConvention: false })).toBe('tickets.stats.sleepingDay');
    // Infosys called these "Indgang, partout" when they were for the whole convention (fixed 2026-11-25)
    expect(statsKindKey({ kind: 'AleaMembership', isAllConvention: true })).toBe('tickets.kinds.AleaMembership');
    expect(statsKindKey({ kind: 'Other', isAllConvention: true })).toBe('tickets.kinds.Other');
    expect(statsKindKey({ kind: 'Other', isAllConvention: false })).toBe('tickets.kinds.Other');
  });

  it('shows each type under a statistics heading by its day, as Partout, or as Antal solgt', () => {
    const texts = { partout: 'Partout', soldCount: 'Antal solgt' };
    const row = (option: TicketTypeOption) => ({ ...option, count: 4 });

    expect(statsTypeText(row(thursday), shortDay, texts)).toBe('Tor, 25');
    expect(statsTypeText(row(partout), shortDay, texts)).toBe('Partout');
    expect(statsTypeText(row(membership), shortDay, texts)).toBe('Antal solgt');
  });
});
