import { Component, OnInit, computed, signal, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { HttpClient } from '@angular/common/http';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { map } from 'rxjs';
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

// Infosys hold/visalle: every run, in id order.
// The filter on activity type is new; Infosys has no filter on this list.
@Component({
  selector: 'app-runs',
  imports: [RouterLink, MatButtonToggleModule, TimeSpanPipe, TranslatePipe],
  templateUrl: './runs.html',
  styleUrl: './runs.scss',
})
export class RunsComponent implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  public readonly activityTypes = ACTIVITY_TYPES;
  public readonly allTypes = ALL_TYPES;
  public readonly flash = inject(RunFlashService).take();
  public readonly runs = signal<RunSummary[] | null>(null);
  public readonly typeFilter = toSignal(
    this.route.queryParamMap.pipe(map(params => parseTypeFilter(params.get('type')))),
    { requireSync: true },
  );
  public readonly visibleRuns = computed(() => filterByType(this.runs() ?? [], this.typeFilter()));

  public ngOnInit(): void {
    this.http.get<RunSummary[]>('/api/runs').subscribe({
      next: data => this.runs.set(data),
      error: err => console.error('Failed to load runs:', err),
    });
  }

  // The filter lives in the URL, so going back from a run returns to the same filter
  public filterBy(filter: RunTypeFilter): void {
    this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { type: filter === ALL_TYPES ? null : filter },
      replaceUrl: true,
    });
  }
}
