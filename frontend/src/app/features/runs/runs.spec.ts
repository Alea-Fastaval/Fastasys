import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { Router, provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { describe, beforeEach, afterEach, it, expect, vi } from 'vitest';
import { UnknownQueryParamError } from '@shared/services';
import { ActivityType, RunSummary } from '@shared/types';
import { RunFlashService } from './run-flash.service';
import { RunSortColumn, RunsComponent, filterByType, sortRuns, toRunSort } from './runs';

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

describe('filterByType', () => {
  it('keeps every run for "All"', () => {
    expect(filterByType(runs, 'All').map(r => r.id)).toEqual([3, 4, 6]);
  });

  it('keeps only the runs of the chosen type, in their original order', () => {
    expect(filterByType(runs, 'BoardGame').map(r => r.id)).toEqual([4, 6]);
    expect(filterByType(runs, 'Quest')).toEqual([]);
  });
});

// In id order, as the API returns them. Wednesday 24, Thursday 25 and Friday 26 March 2027.
const sortable: RunSummary[] = [
  {
    ...summary(1, 'Scenario', 'Østerled', 2),
    startTime: '2027-03-25T17:00:00Z',
    endTime: '2027-03-25T21:00:00Z',
    roomName: 'Lokale 10',
    needsPlayers: false,
    needsGameMasters: true,
  },
  {
    ...summary(2, 'BoardGame', 'Brætspil', 1),
    startTime: '2027-03-24T09:00:00Z',
    endTime: '2027-03-24T13:00:00Z',
    roomName: 'Lokale 2',
    needsPlayers: true,
    needsGameMasters: false,
  },
  {
    ...summary(3, 'Scenario', 'Zombier', 3),
    startTime: '2027-03-26T08:00:00Z',
    endTime: '2027-03-26T12:00:00Z',
    roomName: 'Aula',
    needsPlayers: true,
    needsGameMasters: true,
  },
  {
    ...summary(4, 'BoardGame', 'Brætspil', 1),
    startTime: '2027-03-24T09:00:00Z',
    endTime: '2027-03-24T11:00:00Z',
    roomName: 'Lokale 2',
    needsPlayers: false,
    needsGameMasters: false,
  },
];

describe('toRunSort', () => {
  it('combines the column and the direction', () => {
    expect(toRunSort('activity', 'asc')).toEqual({ column: 'activity', direction: 'asc' });
    expect(toRunSort('room', 'desc')).toEqual({ column: 'room', direction: 'desc' });
  });

  it('sorts A-Z when there is no direction', () => {
    expect(toRunSort('time', null)).toEqual({ column: 'time', direction: 'asc' });
  });

  it('does not sort without a column', () => {
    expect(toRunSort(null, 'desc')).toBeNull();
    expect(toRunSort(null, null)).toBeNull();
  });
});

describe('sortRuns', () => {
  const ids = (list: RunSummary[]) => list.map(r => r.id);

  it.each<[RunSortColumn, number[], number[]]>([
    // Equal values keep id order in both directions
    ['id', [1, 2, 3, 4], [4, 3, 2, 1]],
    // Danish alphabetical order: Ø after Z
    ['activity', [2, 4, 3, 1], [1, 3, 2, 4]],
    // Chronological, not by day name; the same start is ordered by end
    ['time', [4, 2, 1, 3], [3, 1, 2, 4]],
    ['number', [2, 4, 1, 3], [3, 1, 2, 4]],
    // Numbers in names in numeric order: Lokale 2 before Lokale 10
    ['room', [3, 2, 4, 1], [1, 2, 4, 3]],
    // ja before nej, as in A-Z
    ['needsPlayers', [2, 3, 1, 4], [1, 4, 2, 3]],
    ['needsGameMasters', [1, 3, 2, 4], [2, 4, 1, 3]],
  ])('sorts %s A-Z and Z-A', (column, ascending, descending) => {
    expect(ids(sortRuns(sortable, { column, direction: 'asc' }))).toEqual(ascending);
    expect(ids(sortRuns(sortable, { column, direction: 'desc' }))).toEqual(descending);
  });

  it('keeps the id order when nothing is sorted', () => {
    expect(ids(sortRuns(sortable, null))).toEqual([1, 2, 3, 4]);
  });

  it('does not change the list it is given', () => {
    const list = [...sortable];
    sortRuns(list, { column: 'id', direction: 'desc' });
    expect(ids(list)).toEqual([1, 2, 3, 4]);
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

  // Lets the page remove unknown values from the URL and show the result
  const settle = async (harness: RouterTestingHarness) => {
    await harness.fixture.whenStable();
    harness.detectChanges();
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

  // ---- Unknown values in the URL ----

  it('reports an unknown type, removes it from the URL and shows every run', async () => {
    const consoleError = vi.spyOn(console, 'error').mockImplementation(() => undefined);
    const { harness, component, element } = await open('/runs?type=rolle', runs);
    await settle(harness);

    expect(TestBed.inject(Router).url).toBe('/runs');
    expect(consoleError).toHaveBeenCalledWith(expect.any(UnknownQueryParamError));
    expect(component.unknownQueryValues()).toBe('type=rolle');
    expect(element.querySelector('.unknown-query')?.textContent).toContain('common.unknownQueryValues');
    expect(rowIds(element)).toEqual(['3', '4', '6']);
    expect(toggle(element, 'runs.allTypes')?.getAttribute('aria-checked')).toBe('true');
    consoleError.mockRestore();
  });

  it('shows no message when the URL has only known values', async () => {
    const { harness, component, element } = await open('/runs?type=BoardGame&sort=room&dir=desc', runs);
    await settle(harness);

    expect(TestBed.inject(Router).url).toBe('/runs?type=BoardGame&sort=room&dir=desc');
    expect(component.unknownQueryValues()).toBe('');
    expect(element.querySelector('.unknown-query')).toBeNull();
  });

  it('removes the message once a filter is chosen', async () => {
    const consoleError = vi.spyOn(console, 'error').mockImplementation(() => undefined);
    const { harness, element } = await open('/runs?type=rolle', runs);
    await settle(harness);
    expect(element.querySelector('.unknown-query')).not.toBeNull();

    await choose(harness, element, 'activities.types.Scenario');

    expect(TestBed.inject(Router).url).toBe('/runs?type=Scenario');
    expect(element.querySelector('.unknown-query')).toBeNull();
    consoleError.mockRestore();
  });

  // ---- Sorting by clicking a column heading ----

  const header = (element: HTMLElement, label: string) =>
    Array.from(element.querySelectorAll<HTMLElement>('thead th')).find(th => th.textContent?.trim() === label);

  const sortedBy = (element: HTMLElement) =>
    Array.from(element.querySelectorAll('thead th'))
      .map(th => [th.textContent?.trim(), th.getAttribute('aria-sort')])
      .filter(([, sort]) => sort !== 'none');

  const clickHeader = async (harness: RouterTestingHarness, element: HTMLElement, label: string) => {
    header(element, label)?.click();
    await harness.fixture.whenStable();
    harness.detectChanges();
  };

  it('makes every column heading sortable, with nothing sorted and the runs in id order at first', async () => {
    const { element } = await open('/runs', sortable);

    const headers = Array.from(element.querySelectorAll('thead th'));
    expect(headers.map(th => th.textContent?.trim())).toEqual([
      'common.id',
      'runs.fields.activity',
      'runs.fields.time',
      'runs.fields.number',
      'runs.fields.room',
      'runs.fields.needsPlayers',
      'runs.fields.needsGameMasters',
    ]);
    expect(headers.every(th => th.classList.contains('mat-sort-header'))).toBe(true);
    expect(sortedBy(element)).toEqual([]);
    expect(rowIds(element)).toEqual(['1', '2', '3', '4']);
  });

  it('sorts A-Z on the first click and Z-A on the second, and keeps it in the URL', async () => {
    const { harness, element } = await open('/runs', sortable);

    await clickHeader(harness, element, 'runs.fields.activity');
    expect(TestBed.inject(Router).url).toBe('/runs?sort=activity&dir=asc');
    expect(rowIds(element)).toEqual(['2', '4', '3', '1']);
    expect(sortedBy(element)).toEqual([['runs.fields.activity', 'ascending']]);

    await clickHeader(harness, element, 'runs.fields.activity');
    expect(TestBed.inject(Router).url).toBe('/runs?sort=activity&dir=desc');
    expect(rowIds(element)).toEqual(['1', '3', '2', '4']);
    expect(sortedBy(element)).toEqual([['runs.fields.activity', 'descending']]);

    // A third click goes back to A-Z, never to unsorted
    await clickHeader(harness, element, 'runs.fields.activity');
    expect(TestBed.inject(Router).url).toBe('/runs?sort=activity&dir=asc');
  });

  it('sorts another column A-Z when it is clicked', async () => {
    const { harness, element } = await open('/runs?sort=activity&dir=desc', sortable);

    await clickHeader(harness, element, 'runs.fields.room');

    expect(TestBed.inject(Router).url).toBe('/runs?sort=room&dir=asc');
    expect(rowIds(element)).toEqual(['3', '2', '4', '1']);
    expect(sortedBy(element)).toEqual([['runs.fields.room', 'ascending']]);
  });

  it('sorts as the URL says when the page is opened', async () => {
    const { element } = await open('/runs?sort=time&dir=desc', sortable);

    expect(rowIds(element)).toEqual(['3', '1', '2', '4']);
    expect(sortedBy(element)).toEqual([['runs.fields.time', 'descending']]);
  });

  it('reports an unknown sort column, removes it from the URL and keeps the id order', async () => {
    const consoleError = vi.spyOn(console, 'error').mockImplementation(() => undefined);
    const { harness, component, element } = await open('/runs?sort=karma&dir=desc', sortable);
    await settle(harness);

    expect(TestBed.inject(Router).url).toBe('/runs?dir=desc');
    expect(component.unknownQueryValues()).toBe('sort=karma');
    expect(rowIds(element)).toEqual(['1', '2', '3', '4']);
    expect(sortedBy(element)).toEqual([]);
    consoleError.mockRestore();
  });

  it('reports an unknown direction, removes it and sorts A-Z', async () => {
    const consoleError = vi.spyOn(console, 'error').mockImplementation(() => undefined);
    const { harness, component, element } = await open('/runs?sort=time&dir=up', sortable);
    await settle(harness);

    expect(TestBed.inject(Router).url).toBe('/runs?sort=time');
    expect(component.unknownQueryValues()).toBe('dir=up');
    expect(rowIds(element)).toEqual(['4', '2', '1', '3']);
    expect(sortedBy(element)).toEqual([['runs.fields.time', 'ascending']]);
    consoleError.mockRestore();
  });

  it('sorts within the type filter, and keeps the sorting when the filter changes', async () => {
    const { harness, element } = await open('/runs?type=Scenario', sortable);
    expect(rowIds(element)).toEqual(['1', '3']);

    await clickHeader(harness, element, 'runs.fields.activity');
    expect(TestBed.inject(Router).url).toBe('/runs?type=Scenario&sort=activity&dir=asc');
    expect(rowIds(element)).toEqual(['3', '1']);

    await choose(harness, element, 'runs.allTypes');
    expect(TestBed.inject(Router).url).toBe('/runs?sort=activity&dir=asc');
    expect(rowIds(element)).toEqual(['2', '4', '3', '1']);
  });
});
