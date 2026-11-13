import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { ActivatedRoute, Router, convertToParamMap, provideRouter } from '@angular/router';
import { describe, afterEach, it, expect, vi } from 'vitest';
import { ParticipantSignups } from '@shared/types';
import { ParticipantFlashService } from '../participant-flash.service';
import { ParticipantSignupsComponent, canAddSignup, newSignupPriority } from './participant-signups';

// 24 March 2027 is a Wednesday; Copenhagen is UTC+1 that week
const page: ParticipantSignups = {
  participantId: 7,
  signups: [
    {
      activityId: 1,
      title: 'Zombier',
      titleEnglish: 'Zombies',
      scheduleId: 10,
      start: '2027-03-24T09:00:00Z',
      end: '2027-03-24T13:00:00Z',
      priority: 2,
      role: 'Player',
    },
  ],
  activities: [
    {
      id: 1,
      title: 'Zombier',
      titleEnglish: 'Zombies',
      canSignUp: true,
      hasGameMasters: true,
      schedules: [{ id: 10, start: '2027-03-24T09:00:00Z', end: '2027-03-24T13:00:00Z' }],
    },
    { id: 2, title: 'Lukket', titleEnglish: 'Closed', canSignUp: false, hasGameMasters: true, schedules: [] },
    {
      id: 3,
      title: 'Brætspil',
      titleEnglish: 'Board game',
      canSignUp: true,
      hasGameMasters: false,
      schedules: [{ id: 30, start: '2027-03-24T21:00:00Z', end: '2027-03-25T01:00:00Z' }],
    },
  ],
};

describe('canAddSignup and newSignupPriority ("Tilføj Tilmelding")', () => {
  const zombier = page.activities[0];
  const boardGame = page.activities[2];

  it('needs an activity, a time and a role', () => {
    expect(canAddSignup([], zombier, zombier.schedules[0], 'Player')).toBe(true);
    expect(canAddSignup([], zombier, zombier.schedules[0], null)).toBe(false);
    expect(canAddSignup([], zombier, undefined, 'Player')).toBe(false);
  });

  it('adds the same schedule and role only once, but a game master wish next to a player wish', () => {
    // Fixed: Infosys's duplicate check never matched
    expect(canAddSignup(page.signups, zombier, zombier.schedules[0], 'Player')).toBe(false);
    expect(canAddSignup(page.signups, zombier, zombier.schedules[0], 'GameMaster')).toBe(true);
  });

  it('adds no game master wish for an activity without game masters', () => {
    // Fixed: Infosys dropped it silently
    expect(canAddSignup([], boardGame, boardGame.schedules[0], 'GameMaster')).toBe(false);
  });

  it('gives a game master wish priority 1, like Infosys', () => {
    expect(newSignupPriority('GameMaster', 3)).toBe(1);
    expect(newSignupPriority('Player', 3)).toBe(3);
  });
});

describe('ParticipantSignupsComponent (Infosys deltager/updatetilmelding)', () => {
  let httpMock: HttpTestingController;

  afterEach(() => {
    httpMock.verify();
  });

  const setup = async () => {
    await TestBed.configureTestingModule({
      imports: [ParticipantSignupsComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: convertToParamMap({ id: '7' }) } } },
      ],
    }).compileComponents();

    httpMock = TestBed.inject(HttpTestingController);
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
    const set = vi.spyOn(TestBed.inject(ParticipantFlashService), 'set');
    const fixture = TestBed.createComponent(ParticipantSignupsComponent);
    fixture.detectChanges();
    httpMock.match('/assets/i18n/en.json').forEach(req =>
      req.flush({
        common: { weekdays: { Wednesday: 'Onsdag' } },
        participants: { signups: { priorityText: '{{priority}}. prioritet' } },
      }),
    );
    return {
      fixture,
      component: fixture.componentInstance,
      element: fixture.nativeElement as HTMLElement,
      navigate,
      set,
    };
  };
  const rows = (element: HTMLElement) =>
    [...element.querySelectorAll('.signup-list td.signup')].map(td => td.textContent?.replace(/\s+/g, ' ').trim());

  it('lists the wishes and offers the activities that can be signed up for', async () => {
    const { fixture, component, element } = await setup();
    httpMock.expectOne('/api/participants/7/signups').flush(page);
    fixture.detectChanges();

    expect(rows(element)).toEqual([
      'Zombier / Zombies — participants.activities.roles.Player, Onsdag 10:00-14:00, 2. prioritet',
    ]);
    expect(component.activities(page).map(a => a.title)).toEqual(['Zombier', 'Brætspil']);
  });

  it('adds a game master wish with priority 1 and saves the list with "Opdater"', async () => {
    const { fixture, component, element, navigate, set } = await setup();
    httpMock.expectOne('/api/participants/7/signups').flush(page);

    component.activityId = 1;
    component.activityChanged();
    component.scheduleId = 10;
    component.role = 'GameMaster';
    component.priority = 3;
    component.add();
    fixture.detectChanges();
    expect(rows(element)[1]).toContain('participants.activities.roles.GameMaster, Onsdag 10:00-14:00, 1. prioritet');
    expect(component.canAdd()).toBe(false);

    component.submit();
    const req = httpMock.expectOne({ method: 'PUT', url: '/api/participants/7/signups' });
    expect(req.request.body).toEqual({
      signups: [
        { scheduleId: 10, role: 'Player', priority: 2 },
        { scheduleId: 10, role: 'GameMaster', priority: 1 },
      ],
    });
    req.flush(null, { status: 204, statusText: 'No Content' });

    expect(set).toHaveBeenCalledWith('success', 'updated');
    expect(navigate).toHaveBeenCalledWith(['/participants', 7]);
  });

  it('removes a wish from the list with "Slet"', async () => {
    const { fixture, element } = await setup();
    httpMock.expectOne('/api/participants/7/signups').flush(page);
    fixture.detectChanges();

    element.querySelector<HTMLButtonElement>('.signup-list .actions button')?.click();
    fixture.detectChanges();

    expect(rows(element)).toEqual([]);
  });

  it('goes back to the participant with the reason when the save is refused', async () => {
    const { component, set } = await setup();
    httpMock.expectOne('/api/participants/7/signups').flush(page);

    component.submit();
    httpMock
      .expectOne({ method: 'PUT', url: '/api/participants/7/signups' })
      .flush({ reason: 'noGameMasters' }, { status: 400, statusText: 'Bad Request' });

    expect(set).toHaveBeenCalledWith('error', 'signupNoGameMasters');
  });

  it('sends an activity-locked participant back with "Deltagerens aktiviteter kan ikke ændres."', async () => {
    // Fixed: Infosys's edit page didn't check the lock
    const { navigate, set } = await setup();

    httpMock
      .expectOne('/api/participants/7/signups')
      .flush({ reason: 'locked' }, { status: 409, statusText: 'Conflict' });

    expect(set).toHaveBeenCalledWith('error', 'activitiesLocked');
    expect(navigate).toHaveBeenCalledWith(['/participants', 7]);
  });
});
