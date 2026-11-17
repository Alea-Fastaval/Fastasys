import { Component, OnInit, signal, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { CreateRunSeatDto, RUN_SEAT_ROLES, Run, RunSeatRole } from '@shared/types';
import { Card } from '@shared/components';
import { TimeSpanPipe, TranslatePipe } from '@shared/pipes';
import { RunFlashService } from '../run-flash.service';

// Infosys hold/vis/:id:, plus adding and removing participants (Infosys groups/scheduleparticipant)
@Component({
  selector: 'app-run-detail',
  imports: [
    FormsModule,
    RouterLink,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatSelectModule,
    Card,
    TimeSpanPipe,
    TranslatePipe,
  ],
  templateUrl: './run-detail.html',
  styleUrl: './run-detail.scss',
})
export class RunDetailComponent implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly route = inject(ActivatedRoute);

  public readonly roles = RUN_SEAT_ROLES;
  public readonly flash = signal(inject(RunFlashService).take());
  public readonly run = signal<Run | null>(null);
  public readonly notFound = signal(false);
  public readonly busy = signal(false);

  public participantId: number | null = null;
  public role: RunSeatRole | null = null;

  public ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    this.http.get<Run>(`/api/runs/${id}`).subscribe({
      next: data => this.run.set(data),
      error: () => this.notFound.set(true),
    });
  }

  // Infosys only offers the roles the team has room for (aktivitet/getholdneeds)
  public canAdd(run: Run, role: RunSeatRole): boolean {
    return role === 'Player' ? run.canUsePlayers : run.needsGameMasters;
  }

  public create(): void {
    const run = this.run();
    if (!run || this.participantId === null || this.role === null) return;

    const dto: CreateRunSeatDto = { participantId: this.participantId, role: this.role };
    this.busy.set(true);
    this.http.post<Run>(`/api/runs/${run.id}/seats`, dto).subscribe({
      next: updated => {
        this.run.set(updated);
        this.participantId = null;
        this.role = null;
        this.busy.set(false);
        this.flash.set(null);
      },
      error: () => this.fail('participantAddFailed'),
    });
  }

  public delete(participantId: number): void {
    const run = this.run();
    if (!run) return;

    this.busy.set(true);
    this.http.delete<Run>(`/api/runs/${run.id}/seats/${participantId}`).subscribe({
      next: updated => {
        this.run.set(updated);
        this.busy.set(false);
        this.flash.set(null);
      },
      error: () => this.fail('participantRemoveFailed'),
    });
  }

  private fail(key: string): void {
    this.busy.set(false);
    this.flash.set({ type: 'error', key });
  }
}
