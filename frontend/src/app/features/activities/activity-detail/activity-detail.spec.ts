import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { describe, beforeEach, afterEach, it, expect, vi } from 'vitest';
import { Activity, ActivitySchedule, Run, ScheduleSignups, SignupPoolRow } from '@shared/types';
import { AuthService } from '@shared/services';
import { ActivityDetailComponent } from './activity-detail';

const activity: Activity = {
  id: 5,
  title: 'Testscenarie',
  titleEnglish: 'Test scenario',
  author: 'Test Forfatter',
  type: 'Scenario',
  descriptionDanish: 'Linje 1\nLinje 2',
  descriptionEnglish: '',
  teaserDanish: '',
  teaserEnglish: '',
  note: '',
  durationHours: 4.5,
  minPlayersPerTeam: 3,
  maxPlayersPerTeam: 5,
  gameMastersPerTeam: 1,
  price: 30,
  maxSignups: 0,
  minAge: 13,
  canSignUp: true,
  isHidden: false,
  isRoomExclusive: true,
  isTimeExclusive: true,
  isReplayable: false,
  playerLanguage: 'Danish',
  gameMasterLanguage: 'DanishAndEnglish',
  karmaType: 'Big',
  autoSignupCategory: 'Author',
  wordPressId: 0,
  updatedAt: '2026-10-29T08:00:00Z',
};

const emptyRun: Run = {
  id: 20,
  number: 1,
  scheduleId: 9,
  activityId: 5,
  activityTitle: 'Testscenarie',
  startTime: '2027-03-24T09:00:00Z',
  endTime: '2027-03-24T13:00:00Z',
  roomId: 1,
  roomName: 'Lokale 1',
  needsPlayers: true,
  needsGameMasters: true,
  canUsePlayers: true,
  seats: [],
};

const fullRun: Run = {
  ...emptyRun,
  id: 21,
  number: 2,
  needsGameMasters: false,
  seats: [{ seatNumber: 1, participantId: 11, participantName: 'Test Spilleder', karma: null, role: 'GameMaster' }],
};

const schedule: ActivitySchedule = {
  id: 9,
  activityId: 5,
  startTime: '2027-03-24T09:00:00Z',
  endTime: '2027-03-24T13:00:00Z',
  roomId: null,
  roomName: null,
  runs: [emptyRun, fullRun],
};

describe('ActivityDetailComponent', () => {
  let httpMock: HttpTestingController;
  // Whether the user may edit activities (and so drag participants onto runs)
  let mayPlace: boolean;

  beforeEach(async () => {
    mayPlace = true;
    await TestBed.configureTestingModule({
      imports: [ActivityDetailComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: convertToParamMap({ id: '5' }) } } },
      ],
    }).compileComponents();

    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  const render = () => {
    vi.spyOn(TestBed.inject(AuthService), 'hasPrivilege').mockReturnValue(mayPlace);
    const fixture = TestBed.createComponent(ActivityDetailComponent);
    fixture.detectChanges();
    httpMock.match('/assets/i18n/en.json').forEach(req => req.flush({}));
    return fixture;
  };

  // The page loads the schedules and their signup tables together
  const flushSchedules = (schedules: ActivitySchedule[], signups: ScheduleSignups[] = []) => {
    httpMock.expectOne(r => r.url === '/api/activities/5/schedules' && r.method === 'GET').flush(schedules);
    httpMock.expectOne('/api/activities/5/signups').flush(signups);
  };

  const renderLoaded = (schedules: ActivitySchedule[] = [schedule], signups: ScheduleSignups[] = []) => {
    const fixture = render();
    httpMock.expectOne('/api/activities/5').flush(activity);
    flushSchedules(schedules, signups);
    fixture.detectChanges();
    return fixture;
  };

  it('loads and shows the activity', () => {
    const fixture = render();

    httpMock.expectOne('/api/activities/5').flush(activity);
    flushSchedules([]);
    fixture.detectChanges();

    const text: string = fixture.nativeElement.textContent;
    expect(text).toContain('Testscenarie');
    expect(text).toContain('activities.types.Scenario');
    expect(text).toContain('activities.karmaTypes.Big');
    expect(text).toContain('activities.autoSignupCategories.Author');
    expect(text).not.toContain('ageMax');
  });

  it('shows the Infosys not-found text for an unknown id', () => {
    const fixture = render();

    httpMock
      .expectOne('/api/activities/5')
      .flush({ message: 'Activity not found' }, { status: 404, statusText: 'Not Found' });
    fixture.detectChanges();

    expect(fixture.componentInstance.notFound()).toBe(true);
    expect(fixture.nativeElement.textContent).toContain('activities.messages.notFound');
  });

  it('shows each schedule with its runs, what they are missing and their participants', () => {
    const fixture = renderLoaded();

    const text: string = fixture.nativeElement.textContent;
    expect(text).toContain('activities.scheduleLegend');
    expect(text).toContain('activities.noMeetingRoom');
    expect(text).toContain('runs.runLink');
    expect(text).toContain('Test Spilleder');
    expect(text).toContain('runs.roleShort.GameMaster');
    expect(fixture.nativeElement.querySelectorAll('section.run').length).toBe(2);
    // The empty run misses both players and game masters; the other run only players
    expect(fixture.nativeElement.querySelectorAll('.run-header .missing').length).toBe(3);
  });

  it('each schedule links to its "Ret/slet" page', () => {
    const fixture = renderLoaded();

    const link: HTMLAnchorElement = fixture.nativeElement.querySelector('app-card.schedule a.edit-schedule');
    expect(link.getAttribute('href')).toBe('/activities/5/schedules/9/edit');
    expect(link.textContent).toContain('activities.editDelete');
  });

  it('"Tilføj hold" creates a run for the schedule and reloads the schedules', () => {
    const fixture = renderLoaded();
    const component = fixture.componentInstance;

    component.createRun(schedule);

    const req = httpMock.expectOne('/api/runs');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ scheduleId: 9 });
    req.flush({ ...emptyRun, id: 22, number: 3 });
    flushSchedules([schedule]);

    expect(component.message()).toBeNull();
    expect(component.busy()).toBe(false);
  });

  it('shows the Infosys error when no run can be created', () => {
    const fixture = renderLoaded();
    const component = fixture.componentInstance;

    component.createRun(schedule);
    httpMock.expectOne('/api/runs').flush({ message: 'No available room' }, { status: 409, statusText: 'Conflict' });

    expect(component.message()).toEqual({ type: 'error', key: 'runs.messages.createFailed' });
  });

  it('deletes an empty run without asking', () => {
    const fixture = renderLoaded();
    const confirm = vi.spyOn(window, 'confirm');

    fixture.componentInstance.deleteRun(emptyRun);

    const req = httpMock.expectOne(r => r.url === '/api/runs/20' && r.method === 'DELETE');
    expect(req.request.params.get('removeParticipants')).toBe('false');
    req.flush({ message: 'Run deleted successfully' });
    flushSchedules([]);
    expect(confirm).not.toHaveBeenCalled();
    confirm.mockRestore();
  });

  it('asks before deleting a run with participants and then removes them too', () => {
    const fixture = renderLoaded();
    const confirm = vi.spyOn(window, 'confirm').mockReturnValue(true);

    fixture.componentInstance.deleteRun(fullRun);

    expect(confirm).toHaveBeenCalledWith('runs.messages.deleteWithParticipants');
    const req = httpMock.expectOne(r => r.url === '/api/runs/21' && r.method === 'DELETE');
    expect(req.request.params.get('removeParticipants')).toBe('true');
    req.flush({ message: 'Run deleted successfully' });
    flushSchedules([]);
    confirm.mockRestore();
  });

  it('keeps a run with participants when the question is declined', () => {
    const fixture = renderLoaded();
    const confirm = vi.spyOn(window, 'confirm').mockReturnValue(false);

    fixture.componentInstance.deleteRun(fullRun);

    httpMock.expectNone(r => r.method === 'DELETE');
    confirm.mockRestore();
  });

  it('"Opret afvikling" preselects the first room and sends the Copenhagen times as UTC', () => {
    const fixture = renderLoaded([]);
    const component = fixture.componentInstance;

    component.openScheduleForm();
    httpMock.expectOne('/api/rooms').flush([
      { id: 4, name: 'Mødelokale', location: '', capacity: 0, description: '' },
      { id: 6, name: 'Andet lokale', location: '', capacity: 0, description: '' },
    ]);
    const form = component.scheduleForm();
    expect(form?.roomId).toBe(4);

    component.scheduleForm.set({ start: '2027-03-24T10:00', end: '2027-03-24T14:00', roomId: 4 });
    component.createSchedule();

    const req = httpMock.expectOne(r => r.url === '/api/activities/5/schedules' && r.method === 'POST');
    expect(req.request.body).toEqual({
      startTime: '2027-03-24T09:00:00.000Z',
      endTime: '2027-03-24T13:00:00.000Z',
      roomId: 4,
    });
    req.flush({ ...schedule, runs: [] });
    flushSchedules([schedule]);

    expect(component.scheduleForm()).toBeNull();
    expect(component.message()).toEqual({ type: 'success', key: 'activities.messages.scheduleCreated' });
  });

  it('shows the Infosys error when the schedule cannot be created', () => {
    const fixture = renderLoaded([]);
    const component = fixture.componentInstance;

    component.scheduleForm.set({ start: '2027-03-24T14:00', end: '2027-03-24T10:00', roomId: null });
    component.createSchedule();
    httpMock
      .expectOne(r => r.url === '/api/activities/5/schedules' && r.method === 'POST')
      .flush({ message: 'Start must be before end' }, { status: 400, statusText: 'Bad Request' });

    expect(component.scheduleForm()).not.toBeNull();
    expect(component.message()).toEqual({ type: 'error', key: 'activities.messages.scheduleCreateFailed' });
  });

  // --- The "Tilmeldinger" table and dragging (Infosys visaktivitet.phtml and activity.js) ---

  const row = (overrides: Partial<SignupPoolRow>): SignupPoolRow => ({
    participantId: 30,
    name: 'Anna Spiller',
    role: 'Player',
    priority: 1,
    karma: null,
    age: 23,
    language: null,
    activityLanguages: ['Danish'],
    desiredActivities: 0,
    desiredActivitiesReached: null,
    note: null,
    isOnTeam: false,
    isBusy: false,
    isLocked: false,
    isDoubleBooked: false,
    ...overrides,
  });
  const signups: ScheduleSignups[] = [
    {
      scheduleId: 9,
      signups: [
        // Test Spilleder is on run 21 as game master, so the row is hidden; a player wish of theirs lets them be dragged
        row({ participantId: 11, name: 'Test Spilleder', role: 'GameMaster', priority: 0, isOnTeam: true }),
        row({
          participantId: 30,
          desiredActivities: 2,
          desiredActivitiesReached: false,
          language: 'dk',
          note: 'Glæder mig',
        }),
        row({ participantId: 31, name: 'Bo Optaget', priority: 2, isBusy: true }),
        row({ participantId: 32, name: 'Carl Låst', priority: 3, isLocked: true, isDoubleBooked: true }),
        row({ participantId: 11, name: 'Test Spilleder', priority: 4, isOnTeam: true }),
      ],
    },
  ];
  const dragEvent = () =>
    ({
      preventDefault: vi.fn(),
      dataTransfer: { setData: vi.fn(), effectAllowed: '', dropEffect: '' },
    }) as unknown as DragEvent;
  const rows = (element: HTMLElement) => [...element.querySelectorAll<HTMLTableRowElement>('.signup-pool tbody tr')];

  it('shows the signups of each schedule, without those already on a team', () => {
    const fixture = renderLoaded([schedule], signups);
    const element: HTMLElement = fixture.nativeElement;

    expect(element.querySelector('fieldset.signups legend')?.textContent).toContain('activities.signups.title');
    const shown = rows(element);
    expect(shown.map(tr => tr.querySelector('td.name')?.textContent?.trim())).toEqual([
      'Anna Spiller',
      'Bo Optaget',
      'Carl Låst',
    ]);
    const anna = shown[0];
    expect(anna.querySelector('a')?.getAttribute('href')).toBe('/participants/30');
    expect(anna.querySelector('.heart')?.textContent).toBe('favorite_border');
    expect(anna.querySelector('.flag')?.textContent).toBe('DK');
    expect(anna.getAttribute('title')).toContain('participants.notes.commentNote: Glæder mig');
    expect(anna.getAttribute('draggable')).toBe('true');
    // Busy: grey and not draggable, with Infosys's title on the name
    expect(shown[1].classList).toContain('busy');
    expect(shown[1].getAttribute('draggable')).toBeNull();
    expect(shown[1].querySelector('a')?.getAttribute('title')).toBe('activities.signups.busy');
    // Activity-locked and double-booked
    expect(shown[2].classList).toContain('locked');
    expect(shown[2].classList).toContain('double-booked');
    expect(shown[2].getAttribute('draggable')).toBeNull();
  });

  it('places a dragged row on the run it is dropped on, with the role of the wish', () => {
    const fixture = renderLoaded([schedule], signups);
    const component = fixture.componentInstance;
    const anna = signups[0].signups[1];

    component.startRowDrag(dragEvent(), 9, anna);
    const over = dragEvent();
    component.dragOverRun(over, 9, emptyRun);
    expect(over.preventDefault).toHaveBeenCalled();
    expect(component.hoveredRunId()).toBe(20);
    component.dropOnRun(dragEvent(), 9, emptyRun);
    component.endDrag();

    const req = httpMock.expectOne(r => r.url === '/api/runs/20/seats' && r.method === 'POST');
    expect(req.request.body).toEqual({ participantId: 30, role: 'Player' });
    req.flush(emptyRun);
    flushSchedules([schedule], signups);
    expect(component.dragging()).toBeNull();
  });

  it('moves a participant dragged from one run to another', () => {
    const fixture = renderLoaded([schedule], signups);
    const component = fixture.componentInstance;

    component.startSeatDrag(dragEvent(), 9, fullRun, 11);
    component.dropOnRun(dragEvent(), 9, emptyRun);
    component.endDrag();

    httpMock.expectOne(r => r.url === '/api/runs/21/seats/11' && r.method === 'DELETE').flush(fullRun);
    // Infosys gives them the role of their last row: the player wish
    const req = httpMock.expectOne(r => r.url === '/api/runs/20/seats' && r.method === 'POST');
    expect(req.request.body).toEqual({ participantId: 11, role: 'Player' });
    req.flush(emptyRun);
    flushSchedules([schedule], signups);
  });

  it('takes a participant dragged off a run and dropped elsewhere off the run', () => {
    const fixture = renderLoaded([schedule], signups);
    const component = fixture.componentInstance;

    component.startSeatDrag(dragEvent(), 9, fullRun, 11);
    component.endDrag();

    httpMock.expectOne(r => r.url === '/api/runs/21/seats/11' && r.method === 'DELETE').flush(fullRun);
    flushSchedules([schedule], signups);
  });

  it('does nothing with a row dropped elsewhere, or with rows that may not be dragged', () => {
    const fixture = renderLoaded([schedule], signups);
    const component = fixture.componentInstance;

    component.startRowDrag(dragEvent(), 9, signups[0].signups[1]);
    component.endDrag();
    // Busy and activity-locked rows
    component.startRowDrag(dragEvent(), 9, signups[0].signups[2]);
    expect(component.dragging()).toBeNull();
    // A participant on a run without a wish on the schedule can't be dragged off it (Infosys has no row for them)
    expect(component.canDragSeat(9, 99)).toBe(false);

    httpMock.expectNone(r => r.url.startsWith('/api/runs'));
  });

  it("doesn't take a drop on another schedule's run", () => {
    const fixture = renderLoaded([schedule], signups);
    const component = fixture.componentInstance;

    component.startRowDrag(dragEvent(), 9, signups[0].signups[1]);
    const over = dragEvent();
    component.dragOverRun(over, 10, { ...emptyRun, id: 40, scheduleId: 10 });

    expect(over.preventDefault).not.toHaveBeenCalled();
    expect(component.hoveredRunId()).toBeNull();
  });

  it('flashes the row red when the run refuses the participant', () => {
    vi.useFakeTimers();
    const fixture = renderLoaded([schedule], signups);
    const component = fixture.componentInstance;

    component.startRowDrag(dragEvent(), 9, signups[0].signups[1]);
    component.dropOnRun(dragEvent(), 9, fullRun);
    httpMock
      .expectOne(r => r.url === '/api/runs/21/seats' && r.method === 'POST')
      .flush({ message: 'The run cannot use more players' }, { status: 409, statusText: 'Conflict' });

    expect(component.isFailed(9, 30)).toBe(true);
    flushSchedules([schedule], signups);
    vi.advanceTimersByTime(700);
    expect(component.isFailed(9, 30)).toBe(false);
    vi.useRealTimers();
  });

  it('lets only users who may edit activities drag', () => {
    mayPlace = false;
    const fixture = renderLoaded([schedule], signups);

    expect(rows(fixture.nativeElement as HTMLElement)[0].getAttribute('draggable')).toBeNull();
    expect(fixture.componentInstance.canDragSeat(9, 11)).toBe(false);
  });
});
