import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { Router, provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { describe, beforeEach, afterEach, it, expect } from 'vitest';
import { ActivityType, RunSummary } from '@shared/types';
import { RunFlashService } from './run-flash.service';
import { RunsComponent, filterByType, parseTypeFilter } from './runs';

const summary = (id: number, activityType: ActivityType, activityTitle: string, number = 1): RunSummary => ({
  id,
  number,
  activityId: id + 10,
  activityTitle,
  activityType,
  startTime: '2027-03-24T09:00:00Z',
  endTime: '2027-03-24T13:00:00Z',
  roomId: 1,
  roomName: 'Lokale 1',
  needsPlayers: true,
  needsGameMasters: false,
});

const scenario = summary(3, 'Scenario', 'Testscenarie');
const runs: RunSummary[] = [
  scenario,
  summary(4, 'BoardGame', 'Testbrætspil'),
  summary(6, 'BoardGame', 'Testbrætspil', 2),
];

describe('parseTypeFilter', () => {
  it('accepts the activity types', () => {
    expect(parseTypeFilter('BoardGame')).toBe('BoardGame');
    expect(parseTypeFilter('Scenario')).toBe('Scenario');
  });

  it('shows every run when the type is missing or unknown', () => {
    expect(parseTypeFilter(null)).toBe('All');
    expect(parseTypeFilter('')).toBe('All');
    expect(parseTypeFilter('rolle')).toBe('All');
    expect(parseTypeFilter('boardgame')).toBe('All');
  });
});

describe('filterByType', () => {
  it('keeps every run for "All"', () => {
    expect(filterByType(runs, 'All').map(r => r.id)).toEqual([3, 4, 6]);
  });

  it('keeps only the runs of the chosen type, in their original order', () => {
    expect(filterByType(runs, 'BoardGame').map(r => r.id)).toEqual([4, 6]);
    expect(filterByType(runs, 'Quest')).toEqual([]);
  });
});

describe('RunsComponent', () => {
  let httpMock: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([{ path: 'runs', component: RunsComponent }]),
      ],
    }).compileComponents();

    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  const open = async (url: string, data: RunSummary[]) => {
    const harness = await RouterTestingHarness.create();
    const component = await harness.navigateByUrl(url, RunsComponent);
    httpMock.expectOne('/api/runs').flush(data);
    harness.detectChanges();
    httpMock.match('/assets/i18n/en.json').forEach(req => req.flush({}));
    return { harness, component, element: harness.routeNativeElement as HTMLElement };
  };

  const rowIds = (element: HTMLElement) =>
    Array.from(element.querySelectorAll('tbody tr')).map(tr => tr.querySelector('td')?.textContent?.trim());

  const toggle = (element: HTMLElement, label: string) =>
    Array.from(element.querySelectorAll<HTMLButtonElement>('mat-button-toggle button')).find(
      button => button.textContent?.trim() === label,
    );

  const choose = async (harness: RouterTestingHarness, element: HTMLElement, label: string) => {
    toggle(element, label)?.click();
    await harness.fixture.whenStable();
    harness.detectChanges();
  };

  it('lists the runs with activity, time, number, room and what they are missing', async () => {
    const { element } = await open('/runs', [scenario]);

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
    expect(element.querySelectorAll('td.missing').length).toBe(1);
  });

  it('shows the Infosys no-results text when there are no runs', async () => {
    const { element } = await open('/runs', []);

    expect(element.textContent).toContain('runs.messages.notFound');
    expect(element.querySelector('table')).toBeNull();
    expect(element.querySelector('mat-button-toggle-group')).toBeNull();
  });

  it('shows the message left by the previous page', async () => {
    TestBed.inject(RunFlashService).set('success', 'deleted');
    const { element } = await open('/runs', runs);

    expect(element.textContent).toContain('runs.messages.deleted');
  });

  it('offers "Alle" and every activity type, with "Alle" chosen when the URL has no type', async () => {
    const { component, element } = await open('/runs', runs);

    const labels = Array.from(element.querySelectorAll('mat-button-toggle')).map(t => t.textContent?.trim());
    expect(labels).toEqual([
      'runs.allTypes',
      'activities.types.Scenario',
      'activities.types.Quest',
      'activities.types.BoardGame',
      'activities.types.Junior',
      'activities.types.System',
    ]);
    expect(toggle(element, 'runs.allTypes')?.getAttribute('aria-checked')).toBe('true');
    expect(rowIds(element)).toEqual(['3', '4', '6']);
    expect(component.visibleRuns().length).toBe(3);
  });

  it('shows only the runs of the type in the URL, and counts only those', async () => {
    const { component, element } = await open('/runs?type=BoardGame', runs);

    expect(rowIds(element)).toEqual(['4', '6']);
    expect(component.visibleRuns().length).toBe(2);
    expect(toggle(element, 'activities.types.BoardGame')?.getAttribute('aria-checked')).toBe('true');
  });

  it('puts the chosen type in the URL and filters the list', async () => {
    const { harness, element } = await open('/runs', runs);

    await choose(harness, element, 'activities.types.Scenario');

    expect(TestBed.inject(Router).url).toBe('/runs?type=Scenario');
    expect(rowIds(element)).toEqual(['3']);
  });

  it('removes the type from the URL when "Alle" is chosen', async () => {
    const { harness, element } = await open('/runs?type=BoardGame', runs);

    await choose(harness, element, 'runs.allTypes');

    expect(TestBed.inject(Router).url).toBe('/runs');
    expect(rowIds(element)).toEqual(['3', '4', '6']);
  });

  it('shows the no-results text but keeps the filter when no run has the chosen type', async () => {
    const { component, element } = await open('/runs?type=Quest', runs);

    expect(element.querySelector('table')).toBeNull();
    expect(element.textContent).toContain('runs.messages.notFound');
    expect(element.querySelector('mat-button-toggle-group')).not.toBeNull();
    expect(component.visibleRuns().length).toBe(0);
  });

  it('shows every run when the type in the URL is unknown', async () => {
    const { element } = await open('/runs?type=rolle', runs);

    expect(rowIds(element)).toEqual(['3', '4', '6']);
    expect(toggle(element, 'runs.allTypes')?.getAttribute('aria-checked')).toBe('true');
  });
});
