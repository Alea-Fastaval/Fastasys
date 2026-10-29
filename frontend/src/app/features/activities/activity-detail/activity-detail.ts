import { Component, OnInit, signal, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { Activity } from '@shared/types';
import { Card } from '@shared/components';
import { TranslatePipe } from '@shared/pipes';
import { ActivityFlashService } from '../activity-flash.service';

@Component({
  selector: 'app-activity-detail',
  imports: [RouterLink, MatButtonModule, MatIconModule, Card, TranslatePipe],
  templateUrl: './activity-detail.html',
  styleUrl: './activity-detail.scss',
})
export class ActivityDetailComponent implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly route = inject(ActivatedRoute);

  public readonly flash = inject(ActivityFlashService).take();
  public readonly activity = signal<Activity | null>(null);
  public readonly notFound = signal(false);

  public ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    this.http.get<Activity>(`/api/activities/${id}`).subscribe({
      next: data => this.activity.set(data),
      error: () => this.notFound.set(true),
    });
  }
}
