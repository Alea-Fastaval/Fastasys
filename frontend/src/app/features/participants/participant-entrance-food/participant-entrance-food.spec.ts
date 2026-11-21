import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { describe, it, expect } from 'vitest';
import { ParticipantMeal, ParticipantTicket } from '@shared/types';
import { Weekday } from '@shared/utils';
import { ParticipantEntranceFoodComponent, TicketTexts, mealText, ticketLineText } from './participant-entrance-food';

// 24 March 2027 is a Wednesday. Copenhagen is UTC+1 until the clocks go forward in the night to Sunday 28 March.
const ticket = (overrides: Partial<ParticipantTicket>): ParticipantTicket => ({
  line: 'Entrance',
  isAllConvention: false,
  start: '2027-03-24T08:00:00Z',
  isDigital: false,
  name: 'Indgang - Enkelt',
  ...overrides,
});

const partout = ticket({ isAllConvention: true, name: 'Indgang - Partout' });
const thursday = ticket({ start: '2027-03-25T08:00:00Z', name: 'Dagsbillet torsdag' });
const digitalSaturday = ticket({ start: '2027-03-27T08:00:00Z', isDigital: true, name: 'Indgang - Digital' });
const sleepingWednesday = ticket({ line: 'Sleeping', name: 'Overnatning - Enkelt' });
const party = ticket({ line: 'Other', name: 'Fest' });
const bubbles = ticket({ line: 'Other', name: 'Champagne' });

const dinner: ParticipantMeal = { time: '2027-03-24T17:00:00Z', category: 'Aftensmad', isReceived: true };
const breakfast: ParticipantMeal = { time: '2027-03-25T07:00:00Z', category: 'Morgenmad', isReceived: false };

const shortDanish: Record<Weekday, string> = {
  Monday: 'Man',
  Tuesday: 'Tir',
  Wednesday: 'Ons',
  Thursday: 'Tor',
  Friday: 'Fre',
  Saturday: 'Lør',
  Sunday: 'Søn',
};
const texts: TicketTexts = { partout: 'Partout', digital: '(Digital)', shortDay: weekday => shortDanish[weekday] };

describe('ticketLineText (a line of the Infosys Indgang/Mad box)', () => {
  const tickets = [partout, thursday, digitalSaturday, sleepingWednesday, party, bubbles];

  it('shows the entrance tickets as Partout or their short Danish day, with (Digital) for digital ones', () => {
    expect(ticketLineText(tickets, 'Entrance', texts)).toBe('Partout, Tor, Lør (Digital)');
  });

  it('shows the sleeping tickets the same way', () => {
    expect(ticketLineText(tickets, 'Sleeping', texts)).toBe('Ons');
    expect(ticketLineText([ticket({ line: 'Sleeping', isAllConvention: true })], 'Sleeping', texts)).toBe('Partout');
  });

  it('shows the other tickets by name', () => {
    expect(ticketLineText(tickets, 'Other', texts)).toBe('Fest, Champagne');
  });

  it('shows Partout with (Digital) for a digital ticket for the whole convention', () => {
    expect(ticketLineText([ticket({ isAllConvention: true, isDigital: true })], 'Entrance', texts)).toBe(
      'Partout (Digital)',
    );
  });

  it('is empty for a line without tickets', () => {
    expect(ticketLineText([party], 'Entrance', texts)).toBe('');
  });

  it('takes the day in Copenhagen, not in UTC', () => {
    // 23:30 UTC on Wednesday is 00:30 on Thursday in Copenhagen (UTC+1 until the clocks go forward)
    expect(ticketLineText([ticket({ start: '2027-03-24T23:30:00Z' })], 'Entrance', texts)).toBe('Tor');
    // After the clocks go forward on Sunday 28 March, Copenhagen is UTC+2: 22:30 UTC on Sunday is Monday there
    expect(ticketLineText([ticket({ start: '2027-03-28T22:30:00Z' })], 'Entrance', texts)).toBe('Man');
  });
});

describe('mealText (a meal in the Infosys Indgang/Mad box)', () => {
  it("shows the short Danish name of the meal's day and the food", () => {
    expect(mealText(dinner, weekday => shortDanish[weekday])).toBe('Ons Aftensmad');
    expect(mealText(breakfast, weekday => shortDanish[weekday])).toBe('Tor Morgenmad');
  });
});

describe('ParticipantEntranceFoodComponent', () => {
  const render = async (tickets: ParticipantTicket[] | null, meals: ParticipantMeal[] | null) => {
    await TestBed.configureTestingModule({
      imports: [ParticipantEntranceFoodComponent],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();
    const fixture = TestBed.createComponent(ParticipantEntranceFoodComponent);
    fixture.componentRef.setInput('tickets', tickets);
    fixture.componentRef.setInput('meals', meals);
    fixture.detectChanges();
    TestBed.inject(HttpTestingController)
      .match('/assets/i18n/en.json')
      .forEach(req => req.flush({}));
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  };
  const text = (element: HTMLElement, selector: string) =>
    element.querySelector(selector)?.textContent?.replace(/\s+/g, ' ').trim();

  it('says that entrance and food are not ported yet while there is no data', async () => {
    const element = await render(null, null);

    expect(element.querySelector('.tickets-not-ported')?.textContent).toContain(
      'participants.entranceFood.ticketsNotPorted',
    );
    expect(element.querySelector('.meals-not-ported')?.textContent).toContain(
      'participants.entranceFood.mealsNotPorted',
    );
    expect(element.querySelector('.line.entrance')).toBeNull();
    expect(element.querySelector('.meal-list')).toBeNull();
  });

  it('shows the tickets on the lines Indgang, Overnatning and Andet', async () => {
    const element = await render([partout, sleepingWednesday, party], null);

    // Without translations the keys are shown; the day names come from common.weekdaysShort
    expect(text(element, '.line.entrance')).toBe(
      'participants.entranceFood.entrance: participants.entranceFood.partout',
    );
    expect(text(element, '.line.sleeping')).toBe('participants.entranceFood.sleeping: common.weekdaysShort.Wednesday');
    expect(text(element, '.line.other')).toBe('participants.entranceFood.other: Fest');
    expect(element.querySelector('.tickets-not-ported')).toBeNull();
    // Food may be ported on its own, so it can still say it is not ported
    expect(element.querySelector('.meals-not-ported')).not.toBeNull();
  });

  it('shows every line, also the empty ones, like Infosys', async () => {
    const element = await render([], []);

    expect(text(element, '.line.entrance')).toBe('participants.entranceFood.entrance:');
    expect(text(element, '.line.sleeping')).toBe('participants.entranceFood.sleeping:');
    expect(text(element, '.line.other')).toBe('participants.entranceFood.other:');
    expect(text(element, '.line.meals')).toBe('participants.entranceFood.meals:');
    // Infosys shows no list when the participant has no meals
    expect(element.querySelector('.meal-list')).toBeNull();
    expect(element.querySelector('.not-ported')).toBeNull();
  });

  it('lists the meals with whether each has been handed out', async () => {
    const element = await render(null, [dinner, breakfast]);

    const lines = [...element.querySelectorAll('.meal-list li')].map(li => li.textContent?.replace(/\s+/g, ' ').trim());
    expect(lines).toEqual([
      'common.weekdaysShort.Wednesday Aftensmad — (participants.entranceFood.received)',
      'common.weekdaysShort.Thursday Morgenmad — (participants.entranceFood.notReceived)',
    ]);
    expect(element.querySelector('.meals-not-ported')).toBeNull();
    expect(element.querySelector('.tickets-not-ported')).not.toBeNull();
  });
});
