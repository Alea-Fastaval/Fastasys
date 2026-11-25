import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { describe, it, expect } from 'vitest';
import { ParticipantMeal, ParticipantTicket } from '@shared/types';
import { Weekday } from '@shared/utils';
import { ParticipantEntranceFoodComponent, TicketTexts, mealText, ticketLineText } from './participant-entrance-food';

// 24 March 2027 is a Wednesday. Copenhagen is UTC+1 until the clocks go forward in the night to Sunday 28 March.
const ticket = (overrides: Partial<ParticipantTicket>): ParticipantTicket => ({
  line: 'Entrance',
  isAllConvention: false,
  day: '2027-03-24',
  name: 'Indgang - Enkelt',
  ...overrides,
});

const partout = ticket({ isAllConvention: true, name: 'Indgang - Partout' });
const thursday = ticket({ day: '2027-03-25', name: 'Dagsbillet torsdag' });
const saturday = ticket({ day: '2027-03-27', name: 'Indgang - Digital' });
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
const texts: TicketTexts = { partout: 'Partout', shortDay: weekday => shortDanish[weekday] };

describe('ticketLineText (a line of the Infosys Indgang/Mad box)', () => {
  const tickets = [partout, thursday, saturday, sleepingWednesday, party, bubbles];

  it('shows the entrance tickets as Partout or their short Danish day', () => {
    // Infosys added " (Digital)" for a type with "digital" in its name; dropped on 2026-11-25
    expect(ticketLineText(tickets, 'Entrance', texts)).toBe('Partout, Tor, Lør');
  });

  it('shows the sleeping tickets the same way', () => {
    expect(ticketLineText(tickets, 'Sleeping', texts)).toBe('Ons');
    expect(ticketLineText([ticket({ line: 'Sleeping', isAllConvention: true })], 'Sleeping', texts)).toBe('Partout');
  });

  it('shows the other tickets by name', () => {
    expect(ticketLineText(tickets, 'Other', texts)).toBe('Fest, Champagne');
  });

  it('is empty for a line without tickets', () => {
    expect(ticketLineText([party], 'Entrance', texts)).toBe('');
  });

  it("takes the ticket's day as it is, also across the clock change", () => {
    expect(ticketLineText([ticket({ day: '2027-03-28' })], 'Entrance', texts)).toBe('Søn');
    expect(ticketLineText([ticket({ day: '2027-03-29' })], 'Entrance', texts)).toBe('Man');
  });
});

describe('mealText (a meal in the Infosys Indgang/Mad box)', () => {
  it("shows the short Danish name of the meal's day and the food", () => {
    expect(mealText(dinner, weekday => shortDanish[weekday])).toBe('Ons Aftensmad');
    expect(mealText(breakfast, weekday => shortDanish[weekday])).toBe('Tor Morgenmad');
  });
});

describe('ParticipantEntranceFoodComponent', () => {
  const render = async (tickets: ParticipantTicket[], meals: ParticipantMeal[] | null, canEditTickets = false) => {
    await TestBed.configureTestingModule({
      imports: [ParticipantEntranceFoodComponent],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    }).compileComponents();
    const fixture = TestBed.createComponent(ParticipantEntranceFoodComponent);
    fixture.componentRef.setInput('tickets', tickets);
    fixture.componentRef.setInput('meals', meals);
    fixture.componentRef.setInput('participantId', 7);
    fixture.componentRef.setInput('canEditTickets', canEditTickets);
    fixture.detectChanges();
    TestBed.inject(HttpTestingController)
      .match('/assets/i18n/en.json')
      .forEach(req => req.flush({}));
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  };
  const text = (element: HTMLElement, selector: string) =>
    element.querySelector(selector)?.textContent?.replace(/\s+/g, ' ').trim();

  it('says that food is not ported yet while there are no meals', async () => {
    const element = await render([], null);

    expect(element.querySelector('.meals-not-ported')?.textContent).toContain(
      'participants.entranceFood.mealsNotPorted',
    );
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
    // Food may be ported on its own, so it can still say it is not ported
    expect(element.querySelector('.meals-not-ported')).not.toBeNull();
  });

  it('links "Ret/slet" to the ticket editor for users who may change tickets', async () => {
    const element = await render([], [], true);

    expect(element.querySelector('a.edit-tickets')?.getAttribute('href')).toBe('/participants/7/tickets');
  });

  it('has no "Ret/slet" link for other users', async () => {
    const element = await render([], []);

    expect(element.querySelector('a.edit-tickets')).toBeNull();
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
    const element = await render([], [dinner, breakfast]);

    const lines = [...element.querySelectorAll('.meal-list li')].map(li => li.textContent?.replace(/\s+/g, ' ').trim());
    expect(lines).toEqual([
      'common.weekdaysShort.Wednesday Aftensmad — (participants.entranceFood.received)',
      'common.weekdaysShort.Thursday Morgenmad — (participants.entranceFood.notReceived)',
    ]);
    expect(element.querySelector('.meals-not-ported')).toBeNull();
  });
});
