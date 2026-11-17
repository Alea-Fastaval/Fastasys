import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { ActivatedRoute, Router, convertToParamMap, provideRouter } from '@angular/router';
import { describe, afterEach, it, expect, vi } from 'vitest';
import { Run, RunRoom } from '@shared/types';
import { RunFlashService } from '../run-flash.service';
import { RunFormComponent, roomLabel } from './run-form';

const run: Run = {
  id: 7,
  number: 2,
  scheduleId: 4,
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

const rooms: RunRoom[] = [
  { id: 1, name: 'Lokale 1', location: 'Bygning A' },
  { id: 3, name: 'Lokale 3', location: '' },
];

describe('roomLabel', () => {
  it('joins name and location and trims spaces and commas at the ends, like Infosys', () => {
    expect(roomLabel({ id: 1, name: 'Lokale 1', location: 'Bygning A' })).toBe('Lokale 1, Bygning A');
    expect(roomLabel({ id: 1, name: 'Lokale 1', location: '' })).toBe('Lokale 1');
    expect(roomLabel({ id: 1, name: '', location: 'Bygning A' })).toBe('Bygning A');
  });
});

describe('RunFormComponent', () => {
  let httpMock: HttpTestingController;

  afterEach(() => {
    httpMock.verify();
  });

  const setup = async () => {
    await TestBed.configureTestingModule({
      imports: [RunFormComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: convertToParamMap({ id: '7' }) } } },
      ],
    }).compileComponents();

    httpMock = TestBed.inject(HttpTestingController);
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
    const flash = TestBed.inject(RunFlashService);
    const fixture = TestBed.createComponent(RunFormComponent);
    fixture.detectChanges();
    httpMock.match('/assets/i18n/en.json').forEach(req => req.flush({}));

    return { component: fixture.componentInstance, fixture, navigate, flash };
  };

  const load = () => {
    httpMock.expectOne('/api/runs/7').flush(run);
    httpMock.expectOne('/api/runs/7/available-rooms').flush(rooms);
  };

  it('offers the available rooms with the current room selected', async () => {
    const { component } = await setup();
    load();

    expect(component.rooms()).toEqual(rooms);
    expect(component.roomId).toBe(1);
  });

  it('changes the room and opens the run with a success message', async () => {
    const { component, navigate, flash } = await setup();
    load();

    component.roomId = 3;
    component.submit();

    const req = httpMock.expectOne('/api/runs/7');
    expect(req.request.method).toBe('PUT');
    expect(req.request.body).toEqual({ roomId: 3 });
    req.flush({ ...run, roomId: 3 });

    expect(navigate).toHaveBeenCalledWith(['/runs', 7]);
    expect(flash.take()).toEqual({ type: 'success', key: 'updated' });
  });

  it('opens the run with an error message when the update fails', async () => {
    const { component, navigate, flash } = await setup();
    load();

    component.submit();
    httpMock.expectOne('/api/runs/7').flush({ message: 'busy' }, { status: 400, statusText: 'Bad Request' });

    expect(navigate).toHaveBeenCalledWith(['/runs', 7]);
    expect(flash.take()).toEqual({ type: 'error', key: 'updateFailed' });
  });

  it('cancelling goes to the runs without a message', async () => {
    const { component, navigate, flash } = await setup();
    load();

    component.cancel();

    expect(navigate).toHaveBeenCalledWith(['/runs']);
    expect(flash.take()).toBeNull();
  });

  it('deletes after confirmation, removing the participants too, and goes to the list', async () => {
    const { component, navigate, flash } = await setup();
    load();

    component.askDelete();
    expect(component.confirmingDelete()).toBe(true);
    component.confirmDelete();

    const req = httpMock.expectOne(r => r.url === '/api/runs/7' && r.method === 'DELETE');
    expect(req.request.params.get('removeParticipants')).toBe('true');
    req.flush({ message: 'Run deleted successfully' });

    expect(navigate).toHaveBeenCalledWith(['/runs']);
    expect(flash.take()).toEqual({ type: 'success', key: 'deleted' });
  });

  it('goes to the list with an error when the delete fails', async () => {
    const { component, navigate, flash } = await setup();
    load();

    component.confirmDelete();
    httpMock
      .expectOne(r => r.url === '/api/runs/7' && r.method === 'DELETE')
      .flush({ message: 'nope' }, { status: 500, statusText: 'Server Error' });

    expect(navigate).toHaveBeenCalledWith(['/runs']);
    expect(flash.take()).toEqual({ type: 'error', key: 'deleteFailed' });
  });

  it('cancelling the delete confirmation goes to the list without a message', async () => {
    const { component, navigate, flash } = await setup();
    load();

    component.askDelete();
    component.cancelDelete();

    expect(navigate).toHaveBeenCalledWith(['/runs']);
    expect(flash.take()).toBeNull();
  });

  it('shows the not-found text when the run does not exist', async () => {
    const { component, fixture } = await setup();

    httpMock.expectOne('/api/runs/7').flush({}, { status: 404, statusText: 'Not Found' });
    httpMock.expectOne('/api/runs/7/available-rooms').flush({}, { status: 404, statusText: 'Not Found' });
    fixture.detectChanges();
    httpMock.match('/assets/i18n/en.json').forEach(req => req.flush({}));

    expect(component.notFound()).toBe(true);
    expect(fixture.nativeElement.textContent).toContain('runs.messages.notFound');
  });
});
