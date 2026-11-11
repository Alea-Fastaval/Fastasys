import { Component, OnInit, computed, signal, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { RouterLink } from '@angular/router';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatSortModule, Sort } from '@angular/material/sort';
import { ACTIVITY_TYPES, ActivityType, RunSummary } from '@shared/types';
import { TimeSpanPipe, TranslatePipe } from '@shared/pipes';
import { QueryParamsService } from '@shared/services';
import { RunFlashService } from './run-flash.service';

// The "Alle" choice of the type filter. It is what the list shows when the URL has no ?type=.
export const ALL_TYPES = 'All';
export type RunTypeFilter = ActivityType | typeof ALL_TYPES;

export function filterByType(runs: RunSummary[], filter: RunTypeFilter): RunSummary[] {
  return filter === ALL_TYPES ? runs : runs.filter(run => run.activityType === filter);
}

// The columns of the list, in the order they are shown; each can be sorted
export const RUN_SORT_COLUMNS = [
  'id',
  'activity',
  'time',
  'number',
  'room',
  'needsPlayers',
  'needsGameMasters',
] as const;
export type RunSortColumn = (typeof RUN_SORT_COLUMNS)[number];
export const RUN_SORT_DIRECTIONS = ['asc', 'desc'] as const;
export type RunSortDirection = (typeof RUN_SORT_DIRECTIONS)[number];

export interface RunSort {
  column: RunSortColumn;
  direction: RunSortDirection;
}

// The sorting from ?sort= and ?dir=. Without a column the list stays in id order; without a direction it is A-Z.
export function toRunSort(column: RunSortColumn | null, direction: RunSortDirection | null): RunSort | null {
  return column ? { column, direction: direction ?? 'asc' } : null;
}

// Danish alphabetical order (æ, ø, å after z), with numbers in numeric order ("Lokale 2" before "Lokale 10")
const collator = new Intl.Collator('da', { numeric: true });
const instant = (value: string) => Date.parse(value);

// The A-Z order of each column. Times sort chronologically. In the ja/nej columns "ja" comes first, as in A-Z.
const ascending: Record<RunSortColumn, (a: RunSummary, b: RunSummary) => number> = {
  id: (a, b) => a.id - b.id,
  activity: (a, b) => collator.compare(a.activityTitle, b.activityTitle),
  time: (a, b) => instant(a.startTime) - instant(b.startTime) || instant(a.endTime) - instant(b.endTime),
  number: (a, b) => a.number - b.number,
  room: (a, b) => collator.compare(a.roomName, b.roomName),
  needsPlayers: (a, b) => Number(b.needsPlayers) - Number(a.needsPlayers),
  needsGameMasters: (a, b) => Number(b.needsGameMasters) - Number(a.needsGameMasters),
};

// Runs that are equal in the sorted column keep their id order, also when sorting Z-A
export function sortRuns(runs: RunSummary[], sort: RunSort | null): RunSummary[] {
  if (!sort) return runs;
  const compare = ascending[sort.column];
  const sign = sort.direction === 'desc' ? -1 : 1;
  return [...runs].sort((a, b) => sign * compare(a, b));
}

// Infosys hold/visalle: every run, in id order.
// The type filter and the sortable columns are new; Infosys has neither on this list.
@Component({
  selector: 'app-runs',
  imports: [RouterLink, MatButtonToggleModule, MatSortModule, TimeSpanPipe, TranslatePipe],
  providers: [QueryParamsService],
  templateUrl: './runs.html',
  styleUrl: './runs.scss',
})
export class RunsComponent implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly query = inject(QueryParamsService);
  private readonly type = this.query.oneOf('type', ACTIVITY_TYPES);
  private readonly sortColumn = this.query.oneOf('sort', RUN_SORT_COLUMNS);
  private readonly sortDirection = this.query.oneOf('dir', RUN_SORT_DIRECTIONS);

  public readonly activityTypes = ACTIVITY_TYPES;
  public readonly allTypes = ALL_TYPES;
  public readonly columns: { column: RunSortColumn; label: string }[] = [
    { column: 'id', label: 'common.id' },
    { column: 'activity', label: 'runs.fields.activity' },
    { column: 'time', label: 'runs.fields.time' },
    { column: 'number', label: 'runs.fields.number' },
    { column: 'room', label: 'runs.fields.room' },
    { column: 'needsPlayers', label: 'runs.fields.needsPlayers' },
    { column: 'needsGameMasters', label: 'runs.fields.needsGameMasters' },
  ];
  public readonly flash = inject(RunFlashService).take();
  public readonly runs = signal<RunSummary[] | null>(null);
  public readonly typeFilter = computed(() => this.type() ?? ALL_TYPES);
  public readonly sort = computed(() => toRunSort(this.sortColumn(), this.sortDirection()));
  public readonly visibleRuns = computed(() =>
    sortRuns(filterByType(this.runs() ?? [], this.typeFilter()), this.sort()),
  );
  // The unknown values that were removed from the URL, e.g. "type=rolle", or '' when there were none
  public readonly unknownQueryValues = computed(() =>
    this.query
      .unknownValues()
      .map(error => `${error.param}=${error.value}`)
      .join(', '),
  );

  public ngOnInit(): void {
    this.http.get<RunSummary[]>('/api/runs').subscribe({
      next: data => this.runs.set(data),
      error: err => console.error('Failed to load runs:', err),
    });
  }

  // The filter and the sorting live in the URL, so going back from a run returns to the same list
  public filterBy(filter: RunTypeFilter): void {
    this.query.update({ type: filter === ALL_TYPES ? null : filter });
  }

  // Clicking a column sorts it A-Z, clicking it again Z-A (matSortDisableClear: never back to unsorted)
  public sortBy(sort: Sort): void {
    this.query.update({ sort: sort.active, dir: sort.direction || null });
  }
}
