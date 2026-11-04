import { Component, OnInit, computed, signal, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { ACTIVITY_TYPES, ActivitySummary, ActivityType } from '@shared/types';
import { Card } from '@shared/components';
import { TranslatePipe } from '@shared/pipes';
import { ActivityFlashService } from './activity-flash.service';

export interface ActivityGroup {
  type: ActivityType;
  activities: ActivitySummary[];
}

// Infosys "Alle aktiviteter" shows one list per activity type. The API already returns the activities sorted by title.
export function groupByType(activities: ActivitySummary[]): ActivityGroup[] {
  return ACTIVITY_TYPES.map(type => ({ type, activities: activities.filter(a => a.type === type) })).filter(
    group => group.activities.length > 0,
  );
}

@Component({
  selector: 'app-activities',
  imports: [RouterLink, MatButtonModule, MatIconModule, Card, TranslatePipe],
  templateUrl: './activities.html',
  styleUrl: './activities.scss',
})
export class ActivitiesComponent implements OnInit {
  private readonly http = inject(HttpClient);

  public readonly flash = inject(ActivityFlashService).take();
  public readonly activities = signal<ActivitySummary[] | null>(null);
  public readonly groups = computed(() => groupByType(this.activities() ?? []));

  public ngOnInit(): void {
    this.http.get<ActivitySummary[]>('/api/activities').subscribe({
      next: data => this.activities.set(data),
      error: err => console.error('Failed to load activities:', err),
    });
  }
}
