import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { describe, beforeEach, afterEach, it, expect } from 'vitest';
import { Run } from '@shared/types';
import { RunDetailComponent } from './run-detail';

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
  needsGameMasters: false,
  canUsePlayers: true,
  seats: [
    { seatNumber: 1, participantId: 11, participantName: 'Test Spilleder', karma: null, role: 'GameMaster' },
    { seatNumber: 2, participantId: 12, participantName: 'Test Spiller', karma: null, role: 'Player' },
  ],
};

describe('RunDetailComponent', () => {
  let httpMock: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [RunDetailComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: convertToParamMap({ id: '7' }) } } },
      ],
    }).compileComponents();

    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  const render = (data: Run = run) => {
    const fixture = TestBed.createComponent(RunDetailComponent);
    fixture.detectChanges();
    httpMock.match('/assets/i18n/en.json').forEach(req => req.flush({}));
    httpMock.expectOne('/api/runs/7').flush(data);
    fixture.detectChanges();
    return fixture;
  };

  it('shows id, number, activity, time, room and what the run is missing', () => {
    const fixture = render();

    const text: string = fixture.nativeElement.textContent;
    expect(text).toContain('Testscenarie');
    expect(text).toContain('common.weekdays.Wednesday 10:00-14:00');
    expect(text).toContain('Lokale 1');
    const element = fixture.nativeElement as HTMLElement;
    const missing = Array.from(element.querySelectorAll('dd.missing')).map(dd => dd.textContent?.trim());
    expect(missing).toEqual(['activities.yes']);
  });

  it('lists the participants with id, name, a blank karma and their role', () => {
    const fixture = render();

    const element = fixture.nativeElement as HTMLElement;
    const rows = Array.from(element.querySelectorAll('.seats-table tbody tr')).map(tr =>
      Array.from(tr.querySelectorAll('td'))
        .slice(0, 4)
        .map(td => td.textContent?.trim()),
    );
    expect(rows).toEqual([
      ['11', 'Test Spilleder', '', 'runs.roles.GameMaster'],
      ['12', 'Test Spiller', '', 'runs.roles.Player'],
    ]);
  });

  it('only offers the roles the run has room for, like Infosys', () => {
    const fixture = render();
    const component = fixture.componentInstance;

    expect(component.canAdd(run, 'Player')).toBe(true);
    expect(component.canAdd(run, 'GameMaster')).toBe(false);
    expect(component.canAdd({ ...run, canUsePlayers: false, needsGameMasters: true }, 'Player')).toBe(false);
    expect(component.canAdd({ ...run, canUsePlayers: false, needsGameMasters: true }, 'GameMaster')).toBe(true);
  });

  it('adds a participant and shows the updated run', () => {
    const fixture = render();
    const component = fixture.componentInstance;

    component.participantId = 13;
    component.role = 'Player';
    component.create();

    const req = httpMock.expectOne('/api/runs/7/seats');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ participantId: 13, role: 'Player' });
    const updated: Run = {
      ...run,
      seats: [
        ...run.seats,
        { seatNumber: 3, participantId: 13, participantName: 'Ny Deltager', karma: null, role: 'Player' },
      ],
    };
    req.flush(updated);

    expect(component.run()).toEqual(updated);
    expect(component.participantId).toBeNull();
    expect(component.role).toBeNull();
    expect(component.busy()).toBe(false);
  });

  it('shows an error when the participant cannot be added', () => {
    const fixture = render();
    const component = fixture.componentInstance;

    component.participantId = 13;
    component.role = 'GameMaster';
    component.create();
    httpMock.expectOne('/api/runs/7/seats').flush({ message: 'full' }, { status: 409, statusText: 'Conflict' });

    expect(component.flash()).toEqual({ type: 'error', key: 'participantAddFailed' });
    expect(component.run()).toEqual(run);
  });

  it('removes a participant', () => {
    const fixture = render();
    const component = fixture.componentInstance;

    component.delete(12);

    const req = httpMock.expectOne('/api/runs/7/seats/12');
    expect(req.request.method).toBe('DELETE');
    req.flush({ ...run, seats: [run.seats[0]] });

    expect(component.run()?.seats.map(s => s.participantId)).toEqual([11]);
  });

  it('shows the Infosys not-found text for an unknown id', () => {
    const fixture = TestBed.createComponent(RunDetailComponent);
    fixture.detectChanges();
    httpMock.match('/assets/i18n/en.json').forEach(req => req.flush({}));

    httpMock.expectOne('/api/runs/7').flush({ message: 'Run not found' }, { status: 404, statusText: 'Not Found' });
    fixture.detectChanges();

    expect(fixture.componentInstance.notFound()).toBe(true);
    expect(fixture.nativeElement.textContent).toContain('runs.messages.notFound');
  });
});
