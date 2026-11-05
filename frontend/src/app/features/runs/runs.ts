import { Component, OnInit, computed, signal, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { HttpClient } from '@angular/common/http';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatSortModule, Sort } from '@angular/material/sort';
import { ACTIVITY_TYPES, ActivityType, RunSummary } from '@shared/types';
import { TimeSpanPipe, TranslatePipe } from '@shared/pipes';
import { RunFlashService } from './run-flash.service';

// The "Alle" choice of the type filter
export const ALL_TYPES = 'All';
export type RunTypeFilter = ActivityType | typeof ALL_TYPES;

// Reads ?type= from the URL. A missing or unknown type shows every run.
export function parseTypeFilter(value: string | null): RunTypeFilter {
  return ACTIVITY_TYPES.find(type => type === value) ?? ALL_TYPES;
}

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
export type RunSortDirection = 'asc' | 'desc';

export interface RunSort {
  column: RunSortColumn;
  direction: RunSortDirection;
}

// Reads ?sort= and ?dir= from the URL. Without a known column the list stays in id order.
export function parseSort(column: string | null, direction: string | null): RunSort | null {
  const known = RUN_SORT_COLUMNS.find(c => c === column);
  return known ? { column: known, direction: direction === 'desc' ? 'desc' : 'asc' } : null;
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
  templateUrl: './runs.html',
  styleUrl: './runs.scss',
})
export class RunsComponent implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly queryParams = toSignal(this.route.queryParamMap, { requireSync: true });

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
  public readonly typeFilter = computed(() => parseTypeFilter(this.queryParams().get('type')));
  public readonly sort = computed(() => parseSort(this.queryParams().get('sort'), this.queryParams().get('dir')));
  public readonly visibleRuns = computed(() =>
    sortRuns(filterByType(this.runs() ?? [], this.typeFilter()), this.sort()),
  );

  public ngOnInit(): void {
    this.http.get<RunSummary[]>('/api/runs').subscribe({
      next: data => this.runs.set(data),
      error: err => console.error('Failed to load runs:', err),
    });
  }

  // The filter and the sorting live in the URL, so going back from a run returns to the same list
  public filterBy(filter: RunTypeFilter): void {
    this.updateQuery({ type: filter === ALL_TYPES ? null : filter });
  }

  // Clicking a column sorts it A-Z, clicking it again Z-A (matSortDisableClear: never back to unsorted)
  public sortBy(sort: Sort): void {
    this.updateQuery({ sort: sort.active, dir: sort.direction || null });
  }

  private updateQuery(queryParams: Record<string, string | null>): void {
    this.router.navigate([], { relativeTo: this.route, queryParams, queryParamsHandling: 'merge', replaceUrl: true });
  }
}
