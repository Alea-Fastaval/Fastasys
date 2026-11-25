import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ActivatedRoute, Router, convertToParamMap, provideRouter } from '@angular/router';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { FoodTypeOption, ParticipantMeals, ParticipantTickets, TicketTypeOption } from '@shared/types';
import { AuthService } from '@shared/services';
import { ParticipantFlashService } from '../participant-flash.service';
import { ParticipantEntranceFoodWearComponent, addMeal, addTicket } from './participant-entrance-food-wear';

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
const party: TicketTypeOption = { id: 3, name: 'Ottofest', day: '2027-03-27', kind: 'Other', isAllConvention: false };

// Dinner on Wednesday 24 and Thursday 25 March 2027 at 17:00 Copenhagen time (UTC+1)
const dinner: FoodTypeOption = {
  id: 1,
  nameDanish: 'Aftensmad',
  mealTimes: [
    { id: 10, time: '2027-03-24T16:00:00Z' },
    { id: 11, time: '2027-03-25T16:00:00Z' },
  ],
};
const breakfast: FoodTypeOption = {
  id: 2,
  nameDanish: 'Morgenmad',
  mealTimes: [{ id: 20, time: '2027-03-25T06:00:00Z' }],
};

describe('addTicket (imwedit.js setupIndgang)', () => {
  it('adds the chosen type unless it is already listed', () => {
    expect(addTicket([partout], thursday)).toEqual([partout, thursday]);
    expect(addTicket([partout], partout)).toEqual([partout]);
    expect(addTicket([partout], undefined)).toEqual([partout]);
  });
});

describe('addMeal (imwedit.js setupMad)', () => {
  it('adds the chosen meal time unless it is already listed', () => {
    const wednesday = { mealTimeId: 10, foodName: 'Aftensmad', time: '2027-03-24T16:00:00Z', isReceived: true };

    expect(addMeal([wednesday], dinner, dinner.mealTimes[1])).toEqual([
      wednesday,
      { mealTimeId: 11, foodName: 'Aftensmad', time: '2027-03-25T16:00:00Z', isReceived: false },
    ]);
    expect(addMeal([wednesday], dinner, dinner.mealTimes[0])).toEqual([wednesday]);
    expect(addMeal([wednesday], dinner, undefined)).toEqual([wednesday]);
    expect(addMeal([wednesday], undefined, undefined)).toEqual([wednesday]);
  });
});

describe('ParticipantEntranceFoodWearComponent', () => {
  let http: HttpTestingController;

  afterEach(() => http.verify());

  const ticketsPage: ParticipantTickets = {
    participantId: 7,
    tickets: [partout],
    ticketTypes: [partout, thursday, party],
  };
  const mealsPage: ParticipantMeals = {
    participantId: 7,
    meals: [{ mealTimeId: 10, foodName: 'Aftensmad', time: '2027-03-24T16:00:00Z', isReceived: true }],
    foodTypes: [dinner, breakfast],
  };

  // The user has the given privileges; each part loads only for the user who may change it
  const render = async (privileges: string[], found = true) => {
    await TestBed.configureTestingModule({
      imports: [ParticipantEntranceFoodWearComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: convertToParamMap({ id: '7' }) } } },
      ],
    }).compileComponents();
    TestBed.inject(AuthService).currentUser.set({
      id: 1,
      username: 'info',
      email: '',
      firstName: '',
      lastName: '',
      roles: [],
      privileges,
    });
    http = TestBed.inject(HttpTestingController);
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
    const fixture = TestBed.createComponent(ParticipantEntranceFoodWearComponent);
    fixture.detectChanges();
    http.match('/assets/i18n/en.json').forEach(request => request.flush({}));
    const notFound = { status: 404, statusText: 'Not found' };
    if (privileges.includes('entrance_participant_edit')) {
      const request = http.expectOne('/api/participants/7/tickets');
      if (found) request.flush(ticketsPage);
      else request.flush({}, notFound);
    }
    if (privileges.includes('food_participant_edit')) {
      const request = http.expectOne('/api/participants/7/meals');
      if (found) request.flush(mealsPage);
      else request.flush({}, notFound);
    }
    fixture.detectChanges();
    return { fixture, component: fixture.componentInstance, navigate, flash: TestBed.inject(ParticipantFlashService) };
  };

  const cells = (element: HTMLElement, selector: string) =>
    [...element.querySelectorAll(selector)].map(cell => cell.textContent?.replace(/\s+/g, ' ').trim());

  it('lists the tickets and the meals', async () => {
    const { fixture, component } = await render(['entrance_participant_edit', 'food_participant_edit']);
    const element = fixture.nativeElement as HTMLElement;

    expect(cells(element, '.ticket-list .ticket')).toEqual(['Indgang - Partout (tickets.partout)']);
    expect(component.ticketText(thursday)).toBe('Indgang - Enkelt (common.weekdaysShort.Thursday)');
    // The ticket select starts on the first type, the food selects on "Vælg"
    expect(component.chosenTicketId).toBe(1);
    expect(component.chosenFoodId).toBeNull();
    // "Aftensmad Ons" (Infosys's kategori and day)
    expect(cells(element, '.meal-list .meal')).toEqual(['Aftensmad common.weekdaysShort.Wednesday']);
  });

  it('shows only the parts the user may change', async () => {
    const { fixture } = await render(['food_participant_edit']);
    const element = fixture.nativeElement as HTMLElement;

    expect(element.querySelector('section.entrance')).toBeNull();
    expect(element.querySelector('section.food')).not.toBeNull();
  });

  it('adds and removes tickets and meals, and "Opdater" saves both lists', async () => {
    const { component, navigate, flash } = await render(['entrance_participant_edit', 'food_participant_edit']);

    component.chosenTicketId = 3;
    component.addTicket();
    component.removeTicket(partout);
    component.chosenFoodId = 2;
    component.foodChanged();
    component.chosenMealTimeId = 20;
    component.addMeal();
    component.submit();

    const tickets = http.expectOne('/api/participants/7/tickets');
    expect(tickets.request.method).toBe('PUT');
    expect(tickets.request.body).toEqual({ ticketTypeIds: [3] });
    const meals = http.expectOne('/api/participants/7/meals');
    expect(meals.request.method).toBe('PUT');
    expect(meals.request.body).toEqual({ mealTimeIds: [10, 20] });
    tickets.flush(null);
    meals.flush(null);

    expect(flash.take()).toEqual({ type: 'success', key: 'updated' });
    expect(navigate).toHaveBeenCalledWith(['/participants', 7]);
  });

  it('choosing another food type clears the chosen day', async () => {
    const { component } = await render(['food_participant_edit']);

    component.chosenFoodId = 1;
    component.chosenMealTimeId = 10;
    component.chosenFoodId = 2;
    component.foodChanged();

    expect(component.chosenMealTimeId).toBeNull();
    expect(component.chosenFood()?.nameDanish).toBe('Morgenmad');
  });

  it('goes back to the participant with "Kunne ikke opdatere deltageren." when saving fails', async () => {
    const { component, navigate, flash } = await render(['food_participant_edit']);

    component.submit();
    http.expectOne('/api/participants/7/meals').flush({}, { status: 500, statusText: 'Server error' });

    expect(flash.take()).toEqual({ type: 'error', key: 'entranceFoodWearUpdateFailed' });
    expect(navigate).toHaveBeenCalledWith(['/participants', 7]);
  });

  it('says so for an unknown participant', async () => {
    const { fixture } = await render(['entrance_participant_edit'], false);

    expect((fixture.nativeElement as HTMLElement).textContent).toContain('participants.messages.notFound');
  });
});
