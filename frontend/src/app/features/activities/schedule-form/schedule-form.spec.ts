import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { ActivatedRoute, Router, convertToParamMap, provideRouter } from '@angular/router';
import { describe, afterEach, it, expect, vi } from 'vitest';
import { Activity, ActivitySchedule, Room } from '@shared/types';
import { ActivityFlashService } from '../activity-flash.service';
import { ScheduleFormComponent, toScheduleForm, withDefaultRoom } from './schedule-form';

const activity = { id: 5, title: 'Testscenarie' } as Activity;

// Wednesday 24 March 2027, 10:00-14:00 in Copenhagen (UTC+1)
const schedule: ActivitySchedule = {
  id: 9,
  activityId: 5,
  startTime: '2027-03-24T09:00:00Z',
  endTime: '2027-03-24T13:00:00Z',
  roomId: 6,
  roomName: 'Andet lokale',
  runs: [],
};

const rooms: Room[] = [
  { id: 4, name: 'Mødelokale', location: '', capacity: 0, description: '' },
  { id: 6, name: 'Andet lokale', location: '', capacity: 0, description: '' },
];

describe('toScheduleForm', () => {
  it('fills in the times as Copenhagen wall-clock time and keeps the meeting room', () => {
    expect(toScheduleForm(schedule)).toEqual({ start: '2027-03-24T10:00', end: '2027-03-24T14:00', roomId: 6 });
  });

  it('uses summer time after the clocks are put forward', () => {
    const summer = { ...schedule, startTime: '2027-07-01T08:00:00Z', endTime: '2027-07-01T12:30:00Z' };
    expect(toScheduleForm(summer)).toMatchObject({ start: '2027-07-01T10:00', end: '2027-07-01T14:30' });
  });
});

describe('toScheduleForm across the switch to summer time (28 March 2027)', () => {
  it('fills in each end in its own offset', () => {
    const night = { ...schedule, startTime: '2027-03-27T22:00:00Z', endTime: '2027-03-28T02:00:00Z' };
    expect(toScheduleForm(night)).toMatchObject({ start: '2027-03-27T23:00', end: '2027-03-28T04:00' });
  });
});

describe('withDefaultRoom', () => {
  const form = { start: '2027-03-24T10:00', end: '2027-03-24T14:00' };

  it('selects the first room for a schedule without a meeting room, like the Infosys select', () => {
    expect(withDefaultRoom({ ...form, roomId: null }, rooms).roomId).toBe(4);
  });

  it("keeps the schedule's own meeting room", () => {
    expect(withDefaultRoom({ ...form, roomId: 6 }, rooms).roomId).toBe(6);
  });

  it('leaves the room empty when there are no rooms', () => {
    expect(withDefaultRoom({ ...form, roomId: null }, []).roomId).toBeNull();
  });
});

describe('ScheduleFormComponent', () => {
  let httpMock: HttpTestingController;

  afterEach(() => {
    httpMock.verify();
  });

  const setup = async () => {
    await TestBed.configureTestingModule({
      imports: [ScheduleFormComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { paramMap: convertToParamMap({ id: '5', scheduleId: '9' }) } },
        },
      ],
    }).compileComponents();

    httpMock = TestBed.inject(HttpTestingController);
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
    const flash = TestBed.inject(ActivityFlashService);
    const fixture = TestBed.createComponent(ScheduleFormComponent);
    // The translate pipe loads the translations the first time it renders
    const render = () => {
      fixture.detectChanges();
      httpMock.match('/assets/i18n/en.json').forEach(req => req.flush({}));
    };
    render();

    return { component: fixture.componentInstance, fixture, render, navigate, flash };
  };

  const load = (loaded: ActivitySchedule = schedule) => {
    httpMock.expectOne('/api/activities/5').flush(activity);
    httpMock.expectOne('/api/activities/5/schedules/9').flush(loaded);
    httpMock.expectOne('/api/rooms').flush(rooms);
  };

  const deleteRequest = () =>
    httpMock.expectOne(r => r.url === '/api/activities/5/schedules/9' && r.method === 'DELETE');

  it('fills in the form with the schedule and offers every room', async () => {
    const { component, fixture, render } = await setup();
    load();
    render();

    expect(component.activityTitle()).toBe('Testscenarie');
    expect(component.form()).toEqual({ start: '2027-03-24T10:00', end: '2027-03-24T14:00', roomId: 6 });
    expect(component.rooms()).toEqual(rooms);
    expect(fixture.nativeElement.textContent).toContain('activities.scheduleEditTitle');
  });

  it('selects the first room when the schedule has no meeting room', async () => {
    const { component } = await setup();
    load({ ...schedule, roomId: null, roomName: null });

    expect(component.form()?.roomId).toBe(4);
  });

  it('"Opdater" sends the Copenhagen times as UTC and returns to the activity with a success message', async () => {
    const { component, navigate, flash } = await setup();
    load();

    component.form.set({ start: '2027-03-25T12:00', end: '2027-03-25T15:00', roomId: 4 });
    component.submit();

    const req = httpMock.expectOne(r => r.url === '/api/activities/5/schedules/9' && r.method === 'PUT');
    expect(req.request.body).toEqual({
      startTime: '2027-03-25T11:00:00.000Z',
      endTime: '2027-03-25T14:00:00.000Z',
      roomId: 4,
    });
    req.flush({ ...schedule, roomId: 4 });

    expect(navigate).toHaveBeenCalledWith(['/activities', '5']);
    expect(flash.take()).toEqual({ type: 'success', key: 'scheduleUpdated' });
  });

  it('keeps the instants of a schedule across the switch to summer time when it is saved unchanged', async () => {
    // Saturday 27 March 2027 23:00 winter time to Sunday 04:00 summer time
    const { component } = await setup();
    load({ ...schedule, startTime: '2027-03-27T22:00:00Z', endTime: '2027-03-28T02:00:00Z' });

    component.submit();

    const req = httpMock.expectOne(r => r.url === '/api/activities/5/schedules/9' && r.method === 'PUT');
    expect(req.request.body).toMatchObject({
      startTime: '2027-03-27T22:00:00.000Z',
      endTime: '2027-03-28T02:00:00.000Z',
    });
    req.flush(schedule);
  });

  it('sends a time from the skipped hour on 28 March 2027 as the hour after, like Infosys', async () => {
    const { component } = await setup();
    load();

    component.form.set({ start: '2027-03-27T23:00', end: '2027-03-28T02:30', roomId: 4 });
    component.submit();

    const req = httpMock.expectOne(r => r.url === '/api/activities/5/schedules/9' && r.method === 'PUT');
    // 02:30 doesn't exist that night; it is saved as 03:30 summer time (01:30 UTC)
    expect(req.request.body).toMatchObject({ endTime: '2027-03-28T01:30:00.000Z' });
    req.flush(schedule);
  });

  it('returns to the activity with the Infosys error when the update is refused', async () => {
    const { component, navigate, flash } = await setup();
    load();

    component.submit();
    httpMock
      .expectOne(r => r.url === '/api/activities/5/schedules/9' && r.method === 'PUT')
      .flush(
        { message: 'The activity already has a schedule with that start and end' },
        { status: 409, statusText: 'Conflict' },
      );

    expect(navigate).toHaveBeenCalledWith(['/activities', '5']);
    expect(flash.take()).toEqual({ type: 'error', key: 'scheduleUpdateFailed' });
  });

  it('reports the update as failed without calling the server when a time is not a date and time', async () => {
    const { component, navigate, flash } = await setup();
    load();

    component.form.set({ start: '', end: '2027-03-25T15:00', roomId: 4 });
    component.submit();

    httpMock.expectNone(r => r.method === 'PUT');
    expect(navigate).toHaveBeenCalledWith(['/activities', '5']);
    expect(flash.take()).toEqual({ type: 'error', key: 'scheduleUpdateFailed' });
  });

  it('"Fortryd" returns to the activity with "Afviklingen blev ikke opdateret."', async () => {
    const { component, navigate, flash } = await setup();
    load();

    component.cancel();

    expect(navigate).toHaveBeenCalledWith(['/activities', '5']);
    expect(flash.take()).toEqual({ type: 'error', key: 'scheduleNotUpdated' });
  });

  it('"Slet" asks first and then deletes the schedule and returns to the activity', async () => {
    const { component, fixture, render, navigate, flash } = await setup();
    load();

    component.askDelete();
    render();
    expect(fixture.nativeElement.textContent).toContain('activities.scheduleDeleteConfirm');
    httpMock.expectNone(r => r.method === 'DELETE');

    component.confirmDelete();
    deleteRequest().flush({ message: 'Schedule deleted successfully' });

    expect(navigate).toHaveBeenCalledWith(['/activities', '5']);
    expect(flash.take()).toEqual({ type: 'success', key: 'scheduleDeleted' });
  });

  it('says why when the schedule has runs and cannot be deleted', async () => {
    const { component, navigate, flash } = await setup();
    load();

    component.confirmDelete();
    deleteRequest().flush(
      { message: 'Schedule has runs and cannot be deleted' },
      { status: 409, statusText: 'Conflict' },
    );

    expect(navigate).toHaveBeenCalledWith(['/activities', '5']);
    expect(flash.take()).toEqual({ type: 'error', key: 'scheduleDeleteHasRuns' });
  });

  it('shows the Infosys error when the delete fails for another reason', async () => {
    const { component, flash } = await setup();
    load();

    component.confirmDelete();
    deleteRequest().flush({ message: 'nope' }, { status: 500, statusText: 'Server Error' });

    expect(flash.take()).toEqual({ type: 'error', key: 'scheduleDeleteFailed' });
  });

  it('cancelling the delete confirmation returns to the activity without a message', async () => {
    const { component, navigate, flash } = await setup();
    load();

    component.askDelete();
    component.cancelDelete();

    httpMock.expectNone(r => r.method === 'DELETE');
    expect(navigate).toHaveBeenCalledWith(['/activities', '5']);
    expect(flash.take()).toBeNull();
  });

  it('shows the Infosys not-found text for an unknown schedule', async () => {
    const { component, fixture, render } = await setup();

    httpMock.expectOne('/api/activities/5').flush(activity);
    httpMock
      .expectOne('/api/activities/5/schedules/9')
      .flush({ message: 'Schedule not found' }, { status: 404, statusText: 'Not Found' });
    render();

    expect(component.notFound()).toBe(true);
    expect(component.form()).toBeNull();
    expect(fixture.nativeElement.textContent).toContain('activities.messages.notFound');
  });
});
