import { TestBed } from '@angular/core/testing';
import { HttpErrorResponse, provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { ActivatedRoute, Router, convertToParamMap, provideRouter } from '@angular/router';
import { describe, afterEach, it, expect, vi } from 'vitest';
import { ParticipantSeats, SeatRunOption } from '@shared/types';
import { ParticipantFlashService } from '../participant-flash.service';
import {
  ParticipantSeatsComponent,
  canAddSeat,
  isLocked,
  roleOptionDisabled,
  runOptionDisabled,
} from './participant-seats';

// 24 March 2027 is a Wednesday; Copenhagen is UTC+1 that week
const page: ParticipantSeats = {
  participantId: 7,
  seats: [
    {
      runId: 100,
      runNumber: 1,
      role: 'Player',
      title: 'Zombier',
      titleEnglish: 'Zombies',
      start: '2027-03-24T09:00:00Z',
      end: '2027-03-24T13:00:00Z',
    },
  ],
  activities: [
    {
      id: 2,
      title: 'Natspil',
      titleEnglish: 'Night game',
      schedules: [
        {
          id: 20,
          start: '2027-03-24T21:00:00Z',
          end: '2027-03-25T01:00:00Z',
          isBusy: false,
          runs: [
            { id: 200, number: 1, canUsePlayers: false, needsGameMasters: false },
            { id: 201, number: 2, canUsePlayers: true, needsGameMasters: true },
          ],
        },
      ],
    },
  ],
  wishes: [],
};

const run = (canUsePlayers: boolean, needsGameMasters: boolean): SeatRunOption => ({
  id: 1,
  number: 1,
  canUsePlayers,
  needsGameMasters,
});

describe('runOptionDisabled and roleOptionDisabled (Infosys aktivitet/gethold and getholdneeds)', () => {
  it('disables a run that can use no more players and needs no more game masters', () => {
    expect(runOptionDisabled(run(false, false))).toBe(true);
    expect(runOptionDisabled(run(true, false))).toBe(false);
    expect(runOptionDisabled(run(false, true))).toBe(false);
  });

  it('disables Spiller and Spilleder separately', () => {
    expect(roleOptionDisabled(run(false, true), 'Player')).toBe(true);
    expect(roleOptionDisabled(run(false, true), 'GameMaster')).toBe(false);
    expect(roleOptionDisabled(run(true, false), 'Player')).toBe(false);
    expect(roleOptionDisabled(run(true, false), 'GameMaster')).toBe(true);
  });
});

describe('canAddSeat ("Tilføj Aktivitet")', () => {
  const activity = page.activities[0];
  const schedule = activity.schedules[0];
  const free = schedule.runs[1];

  it('needs an activity, a time, a run and a role', () => {
    expect(canAddSeat([], activity, schedule, free, 'Player')).toBe(true);
    expect(canAddSeat([], activity, schedule, free, null)).toBe(false);
    expect(canAddSeat([], activity, schedule, undefined, 'Player')).toBe(false);
    expect(canAddSeat([], undefined, undefined, undefined, null)).toBe(false);
  });

  it('adds a run only once, like imwedit.js checkForAlreadySelected', () => {
    const listed = { ...page.seats[0], runId: free.id };
    expect(canAddSeat([listed], activity, schedule, free, 'GameMaster')).toBe(false);
  });
});

describe('isLocked', () => {
  it('is only true for the 409 the API gives an activity-locked participant', () => {
    expect(isLocked(new HttpErrorResponse({ status: 409, error: { reason: 'locked' } }))).toBe(true);
    expect(isLocked(new HttpErrorResponse({ status: 409, error: { message: 'Could not update' } }))).toBe(false);
    expect(isLocked(new HttpErrorResponse({ status: 400, error: { reason: 'locked' } }))).toBe(false);
  });
});

describe('ParticipantSeatsComponent (Infosys deltager/updateaktivitet)', () => {
  let httpMock: HttpTestingController;

  afterEach(() => {
    httpMock.verify();
  });

  const setup = async () => {
    await TestBed.configureTestingModule({
      imports: [ParticipantSeatsComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: convertToParamMap({ id: '7' }) } } },
      ],
    }).compileComponents();

    httpMock = TestBed.inject(HttpTestingController);
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
    const flash = TestBed.inject(ParticipantFlashService);
    const fixture = TestBed.createComponent(ParticipantSeatsComponent);
    fixture.detectChanges();
    httpMock
      .match('/assets/i18n/en.json')
      .forEach(req => req.flush({ common: { weekdays: { Wednesday: 'Onsdag', Thursday: 'Torsdag' } } }));
    return {
      fixture,
      component: fixture.componentInstance,
      element: fixture.nativeElement as HTMLElement,
      navigate,
      flash,
    };
  };

  const rows = (element: HTMLElement) =>
    [...element.querySelectorAll('.seat-list td.seat')].map(td => td.textContent?.replace(/\s+/g, ' ').trim());

  it("lists the participant's seats", async () => {
    const { fixture, element } = await setup();
    httpMock.expectOne('/api/participants/7/seats').flush(page);
    fixture.detectChanges();

    expect(rows(element)).toEqual([
      'Zombier / Zombies — participants.activities.roles.Player, participants.seats.run 1, Onsdag 10:00-14:00',
    ]);
    expect(element.querySelector('.back, a[href="/participants/7"]')).not.toBeNull();
  });

  it('adds a chosen run to the list and clears the choices below a changed one', async () => {
    const { fixture, component, element } = await setup();
    httpMock.expectOne('/api/participants/7/seats').flush(page);

    component.activityId = 2;
    component.activityChanged();
    component.scheduleId = 20;
    component.scheduleChanged();
    component.runId = 201;
    component.runChanged();
    component.role = 'GameMaster';
    expect(component.canAdd()).toBe(true);
    component.add();
    fixture.detectChanges();

    expect(rows(element)[1]).toBe(
      'Natspil / Night game — participants.activities.roles.GameMaster, participants.seats.run 2, Onsdag 22:00-02:00',
    );
    // The same run can't be added again
    expect(component.canAdd()).toBe(false);

    component.activityId = 2;
    component.activityChanged();
    expect([component.scheduleId, component.runId, component.role]).toEqual([null, null, null]);
  });

  it('lists the wishes and adds a seat from one with "Tilføj"', async () => {
    // Fixed: Infosys's "Tilføj" needed Prototype.js and never worked
    const { fixture, element } = await setup();
    httpMock.expectOne('/api/participants/7/seats').flush({
      ...page,
      wishes: [
        {
          scheduleId: 20,
          role: 'GameMaster',
          priority: 0,
          title: 'Natspil',
          titleEnglish: 'Night game',
          start: '2027-03-24T21:00:00Z',
          end: '2027-03-25T01:00:00Z',
          runId: 201,
          runNumber: 2,
        },
        {
          scheduleId: 30,
          role: 'Player',
          priority: 1,
          title: 'Fuldt',
          titleEnglish: 'Full',
          start: '2027-03-24T09:00:00Z',
          end: '2027-03-24T13:00:00Z',
          runId: null,
          runNumber: null,
        },
      ],
    });
    fixture.detectChanges();

    const wishRows = () => [...element.querySelectorAll('.wish-list tbody tr')];
    expect(element.querySelector('.wish-list th')?.textContent).toContain('participants.seats.wishesTitle');
    expect(wishRows().map(tr => tr.querySelector('td.wish')?.textContent?.replace(/\s+/g, ' ').trim())).toEqual([
      'Natspil / Night game — Onsdag 22:00-02:00, participants.activities.roles.GameMaster, participants.signups.priorityText',
      'Fuldt / Full — Onsdag 10:00-14:00, participants.activities.roles.Player, participants.signups.priorityText',
    ]);
    // No run of that schedule has room, or the participant is busy then
    expect(wishRows()[1].querySelector('button')).toBeNull();

    wishRows()[0].querySelector<HTMLButtonElement>('button')?.click();
    fixture.detectChanges();

    expect(rows(element)[1]).toBe(
      'Natspil / Night game — participants.activities.roles.GameMaster, participants.seats.run 2, Onsdag 22:00-02:00',
    );
    // The run is in the list now
    expect(wishRows()[0].querySelector<HTMLButtonElement>('button')?.disabled).toBe(true);
  });

  it('removes a seat from the list with "Slet"', async () => {
    const { fixture, element } = await setup();
    httpMock.expectOne('/api/participants/7/seats').flush(page);
    fixture.detectChanges();

    element.querySelector<HTMLButtonElement>('.seat-list .actions button')?.click();
    fixture.detectChanges();

    expect(rows(element)).toEqual([]);
  });

  it('saves the list with "Opdater" and goes back to the participant with "Deltageren blev opdateret."', async () => {
    const { fixture, component, navigate, flash } = await setup();
    httpMock.expectOne('/api/participants/7/seats').flush(page);
    fixture.detectChanges();
    const set = vi.spyOn(flash, 'set');

    component.submit();
    const req = httpMock.expectOne({ method: 'PUT', url: '/api/participants/7/seats' });
    expect(req.request.body).toEqual({ seats: [{ runId: 100, role: 'Player' }] });
    req.flush(null, { status: 204, statusText: 'No Content' });

    expect(set).toHaveBeenCalledWith('success', 'updated');
    expect(navigate).toHaveBeenCalledWith(['/participants', 7]);
  });

  it('goes back to the participant with an error when the save fails', async () => {
    const { component, navigate, flash } = await setup();
    httpMock.expectOne('/api/participants/7/seats').flush(page);
    const set = vi.spyOn(flash, 'set');

    component.submit();
    httpMock
      .expectOne({ method: 'PUT', url: '/api/participants/7/seats' })
      .flush({ message: 'Unknown run' }, { status: 400, statusText: 'Bad Request' });

    expect(set).toHaveBeenCalledWith('error', 'activitiesUpdateFailed');
    expect(navigate).toHaveBeenCalledWith(['/participants', 7]);
  });

  it('sends an activity-locked participant back with "Deltagerens aktiviteter kan ikke ændres."', async () => {
    const { navigate, flash } = await setup();
    const set = vi.spyOn(flash, 'set');

    httpMock
      .expectOne('/api/participants/7/seats')
      .flush({ reason: 'locked' }, { status: 409, statusText: 'Conflict' });

    expect(set).toHaveBeenCalledWith('error', 'activitiesLocked');
    expect(navigate).toHaveBeenCalledWith(['/participants', 7]);
  });

  it('says the participant was not found', async () => {
    const { fixture, element } = await setup();
    httpMock.expectOne('/api/participants/7/seats').flush({}, { status: 404, statusText: 'Not Found' });
    fixture.detectChanges();

    expect(element.querySelector('.flash.error')?.textContent).toContain('participants.messages.notFound');
  });
});
