import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { describe, beforeEach, afterEach, it, expect } from 'vitest';
import { RunSummary } from '@shared/types';
import { RunFlashService } from './run-flash.service';
import { RunsComponent } from './runs';

const runs: RunSummary[] = [
  {
    id: 3,
    number: 1,
    activityId: 5,
    activityTitle: 'Testscenarie',
    startTime: '2027-03-24T09:00:00Z',
    endTime: '2027-03-24T13:00:00Z',
    roomId: 1,
    roomName: 'Lokale 1',
    needsPlayers: true,
    needsGameMasters: false,
  },
];

describe('RunsComponent', () => {
  let httpMock: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [RunsComponent],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    }).compileComponents();

    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  const render = () => {
    const fixture = TestBed.createComponent(RunsComponent);
    fixture.detectChanges();
    return fixture;
  };

  it('lists the runs with activity, time, number, room and what they are missing', () => {
    const fixture = render();

    httpMock.expectOne('/api/runs').flush(runs);
    fixture.detectChanges();
    httpMock.match('/assets/i18n/en.json').forEach(req => req.flush({}));

    const element = fixture.nativeElement as HTMLElement;
    const cells = Array.from(element.querySelectorAll('tbody td')).map(td => td.textContent?.trim());
    expect(cells).toEqual([
      '3',
      'Testscenarie',
      'common.weekdays.Wednesday 10:00-14:00',
      '1',
      'Lokale 1',
      'activities.yes',
      'activities.no',
    ]);
    const missing = fixture.nativeElement.querySelectorAll('td.missing');
    expect(missing.length).toBe(1);
  });

  it('shows the Infosys no-results text when there are no runs', () => {
    const fixture = render();

    httpMock.expectOne('/api/runs').flush([]);
    fixture.detectChanges();
    httpMock.match('/assets/i18n/en.json').forEach(req => req.flush({}));

    expect(fixture.nativeElement.textContent).toContain('runs.messages.notFound');
    expect(fixture.nativeElement.querySelector('table')).toBeNull();
  });

  it('shows the message left by the previous page', () => {
    TestBed.inject(RunFlashService).set('success', 'deleted');
    const fixture = render();

    httpMock.expectOne('/api/runs').flush(runs);
    fixture.detectChanges();
    httpMock.match('/assets/i18n/en.json').forEach(req => req.flush({}));

    expect(fixture.nativeElement.textContent).toContain('runs.messages.deleted');
  });
});
