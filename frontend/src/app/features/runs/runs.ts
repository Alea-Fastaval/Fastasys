import { Component, OnInit, signal, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { RouterLink } from '@angular/router';
import { RunSummary } from '@shared/types';
import { TimeSpanPipe, TranslatePipe } from '@shared/pipes';
import { RunFlashService } from './run-flash.service';

// Infosys hold/visalle: every run, in id order
@Component({
  selector: 'app-runs',
  imports: [RouterLink, TimeSpanPipe, TranslatePipe],
  templateUrl: './runs.html',
  styleUrl: './runs.scss',
})
export class RunsComponent implements OnInit {
  private readonly http = inject(HttpClient);

  public readonly flash = inject(RunFlashService).take();
  public readonly runs = signal<RunSummary[] | null>(null);

  public ngOnInit(): void {
    this.http.get<RunSummary[]>('/api/runs').subscribe({
      next: data => this.runs.set(data),
      error: err => console.error('Failed to load runs:', err),
    });
  }
}
