import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { describe, beforeEach, afterEach, it, expect, vi } from 'vitest';
import { Activity, ActivitySchedule, Run } from '@shared/types';
import { ActivityDetailComponent } from './activity-detail';
import { AuthService } from '@shared/services';

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
  isCancelled: false,
  canUsePlayers: true,
  seats: [],
};

const fullRun: Run = {
  ...emptyRun,
  id: 21,
  number: 2,
  needsGameMasters: false,
  isCancelled: false,
  seats: [{ seatNumber: 1, participantId: 11, participantName: 'Test Spilleder', karma: 0, role: 'GameMaster' }],
};

const schedule: ActivitySchedule = {
  id: 9,
  activityId: 5,
  startTime: '2027-03-24T09:00:00Z',
  endTime: '2027-03-24T13:00:00Z',
  roomId: null,
  roomName: null,
  runs: [emptyRun, fullRun],
  isCancelled: false,
  cancelledAt: null,
};

describe('ActivityDetailComponent', () => {
  let httpMock: HttpTestingController;

  beforeEach(async () => {
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
    vi.restoreAllMocks();
  });

  const grantEdit = () =>
    TestBed.inject(AuthService).currentUser.set({
      id: 1,
      username: 'planner',
      email: '',
      firstName: '',
      lastName: '',
      roles: [],
      privileges: ['activities_edit'],
    });

  const render = () => {
    const fixture = TestBed.createComponent(ActivityDetailComponent);
    fixture.detectChanges();
    httpMock.match('/assets/i18n/en.json').forEach(req => req.flush({}));
    return fixture;
  };

  const renderLoaded = (schedules: ActivitySchedule[] = [schedule]) => {
    const fixture = render();
    httpMock.expectOne('/api/activities/5').flush(activity);
    httpMock.expectOne('/api/activities/5/schedules').flush(schedules);
    fixture.detectChanges();
    return fixture;
  };

  it('cancels an occurrence and reloads every visible run with updated karma', () => {
    const fixture = renderLoaded();
    grantEdit();
    vi.spyOn(window, 'confirm').mockReturnValue(true);
    fixture.componentInstance.setCancellation(schedule);
    const request = httpMock.expectOne('/api/activities/5/schedules/9/cancellation');
    expect(request.request.method).toBe('PUT');
    expect(request.request.body).toEqual({ isCancelled: true });
    const updatedRun = { ...fullRun, isCancelled: true, seats: [{ ...fullRun.seats[0], karma: -12 }] };
    const cancelled = { ...schedule, isCancelled: true, cancelledAt: '2026-11-19T12:00:00Z', runs: [updatedRun] };
    request.flush(cancelled);
    httpMock
      .expectOne('/api/activities/5/schedules')
      .flush([cancelled, { ...schedule, id: 10, runs: [{ ...updatedRun, id: 22, isCancelled: false }] }]);
    expect(fixture.componentInstance.schedules()).toHaveLength(2);
    expect(fixture.componentInstance.schedules()[0].isCancelled).toBe(true);
    expect(
      fixture.componentInstance
        .schedules()
        .flatMap(s => s.runs)
        .flatMap(r => r.seats)
        .map(s => s.karma),
    ).toEqual([-12, -12]);
    expect(fixture.componentInstance.busy()).toBe(false);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('activities.messages.cancelledHelp');
    fixture.componentInstance.createRun(cancelled);
    httpMock.expectNone('/api/runs');
  });

  it('hides stale schedules and karma if reloading after cancellation fails', () => {
    const fixture = renderLoaded();
    grantEdit();
    vi.spyOn(window, 'confirm').mockReturnValue(true);
    fixture.componentInstance.setCancellation(schedule);
    httpMock.expectOne('/api/activities/5/schedules/9/cancellation').flush({ ...schedule, isCancelled: true });
    httpMock.expectOne('/api/activities/5/schedules').flush({}, { status: 500, statusText: 'Server error' });
    expect(fixture.componentInstance.schedules()).toEqual([]);
    expect(fixture.componentInstance.message()).toEqual({
      type: 'error',
      key: 'activities.messages.scheduleLoadFailed',
    });
    expect(fixture.componentInstance.busy()).toBe(false);
  });

  it('restores an occurrence and keeps its cancelled state visible when a room conflict is rejected', () => {
    const cancelled = { ...schedule, isCancelled: true };
    const fixture = renderLoaded([cancelled]);
    grantEdit();
    vi.spyOn(window, 'confirm').mockReturnValue(true);
    fixture.componentInstance.setCancellation(cancelled);
    const request = httpMock.expectOne('/api/activities/5/schedules/9/cancellation');
    expect(request.request.body).toEqual({ isCancelled: false });
    request.flush({}, { status: 409, statusText: 'Conflict' });
    expect(fixture.componentInstance.schedules()[0].isCancelled).toBe(true);
    expect(fixture.componentInstance.message()?.key).toBe('activities.messages.scheduleRestoreFailed');
    fixture.componentInstance.setCancellation(cancelled);
    httpMock.expectOne('/api/activities/5/schedules/9/cancellation').flush(schedule);
    httpMock.expectOne('/api/activities/5/schedules').flush([schedule]);
    expect(fixture.componentInstance.schedules()[0].isCancelled).toBe(false);
  });

  it('does not send a cancellation when confirmation is declined or edit access is missing', () => {
    const fixture = renderLoaded();
    fixture.componentInstance.setCancellation(schedule);
    httpMock.expectNone('/api/activities/5/schedules/9/cancellation');
    grantEdit();
    vi.spyOn(window, 'confirm').mockReturnValue(false);
    fixture.componentInstance.setCancellation(schedule);
    httpMock.expectNone('/api/activities/5/schedules/9/cancellation');
  });

  it('loads and shows the activity', () => {
    const fixture = render();

    httpMock.expectOne('/api/activities/5').flush(activity);
    httpMock.expectOne('/api/activities/5/schedules').flush([]);
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
    httpMock.expectOne('/api/activities/5/schedules').flush([schedule]);

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
    httpMock.expectOne('/api/activities/5/schedules').flush([]);
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
    httpMock.expectOne('/api/activities/5/schedules').flush([]);
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
    httpMock.expectOne(r => r.url === '/api/activities/5/schedules' && r.method === 'GET').flush([schedule]);

    expect(component.scheduleForm()).toBeNull();
    expect(component.message()).toEqual({ type: 'success', key: 'activities.messages.scheduleCreated' });
  });

  it('waits for current schedules before allowing cancellation after creating an occurrence', () => {
    const fixture = renderLoaded();
    const component = fixture.componentInstance;
    grantEdit();
    vi.spyOn(window, 'confirm').mockReturnValue(true);
    component.scheduleForm.set({ start: '2027-03-25T10:00', end: '2027-03-25T14:00', roomId: null });
    component.createSchedule();
    httpMock
      .expectOne(r => r.url === '/api/activities/5/schedules' && r.method === 'POST')
      .flush({ ...schedule, id: 10 });
    const reload = httpMock.expectOne(r => r.url === '/api/activities/5/schedules' && r.method === 'GET');

    expect(component.busy()).toBe(true);
    component.setCancellation(schedule);
    httpMock.expectNone('/api/activities/5/schedules/9/cancellation');

    reload.flush([schedule]);
    expect(component.busy()).toBe(false);
    component.setCancellation(schedule);
    httpMock.expectOne('/api/activities/5/schedules/9/cancellation').flush({ ...schedule, isCancelled: true });
    httpMock.expectOne('/api/activities/5/schedules').flush([{ ...schedule, isCancelled: true }]);
    expect(component.schedules()[0].isCancelled).toBe(true);
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
});
