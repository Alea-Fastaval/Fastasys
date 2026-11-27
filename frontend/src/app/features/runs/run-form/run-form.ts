import { Component, OnInit, signal, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';
import { Run, RunRoom, UpdateRunDto } from '@shared/types';
import { Card } from '@shared/components';
import { TranslatePipe } from '@shared/pipes';
import { RunFlashService } from '../run-flash.service';

// Infosys shows a room as trim("beskrivelse, omraade, skole", ' ,'); Fastasys rooms have a name and a location
export function roomLabel(room: Pick<RunRoom, 'name' | 'location'>): string {
  return `${room.name}, ${room.location}`.replace(/^[ ,]+|[ ,]+$/g, '');
}

// Infosys hold/edit/:id: (only the room can be changed) and its delete confirmation
@Component({
  selector: 'app-run-form',
  imports: [FormsModule, MatButtonModule, MatFormFieldModule, MatSelectModule, Card, TranslatePipe],
  templateUrl: './run-form.html',
  styleUrl: './run-form.scss',
})
export class RunFormComponent implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly flash = inject(RunFlashService);

  public readonly run = signal<Run | null>(null);
  public readonly rooms = signal<RunRoom[]>([]);
  public readonly notFound = signal(false);
  public readonly busy = signal(false);
  public readonly confirmingDelete = signal(false);

  public roomId: number | null = null;

  public ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    this.http.get<Run>(`/api/runs/${id}`).subscribe({
      next: run => {
        this.run.set(run);
        this.roomId = run.roomId;
      },
      error: () => this.notFound.set(true),
    });
    this.http.get<RunRoom[]>(`/api/runs/${id}/available-rooms`).subscribe({
      next: rooms => this.rooms.set(rooms),
      error: err => console.error('Failed to load rooms:', err),
    });
  }

  public roomLabel(room: RunRoom): string {
    return roomLabel(room);
  }

  // Infosys redirects to the run with a success or error message either way
  public submit(): void {
    const run = this.run();
    if (!run || this.roomId === null) return;

    const dto: UpdateRunDto = { roomId: this.roomId };
    this.busy.set(true);
    this.http.put<Run>(`/api/runs/${run.id}`, dto).subscribe({
      next: () => {
        this.flash.set('success', 'updated');
        this.router.navigate(['/runs', run.id]);
      },
      error: () => {
        this.flash.set('error', 'updateFailed');
        this.router.navigate(['/runs', run.id]);
      },
    });
  }

  // Infosys "Fortryd" goes to the hold overview page, without a message
  public cancel(): void {
    this.router.navigate(['/runs']);
  }

  public askDelete(): void {
    this.confirmingDelete.set(true);
  }

  // The Infosys delete confirmation always removes the participants' seats together with the run
  public confirmDelete(): void {
    const run = this.run();
    if (!run) return;

    this.busy.set(true);
    this.http.delete(`/api/runs/${run.id}`, { params: { removeParticipants: true } }).subscribe({
      next: () => {
        this.flash.set('success', 'deleted');
        this.router.navigate(['/runs']);
      },
      error: () => {
        this.flash.set('error', 'deleteFailed');
        this.router.navigate(['/runs']);
      },
    });
  }

  // Infosys "Fortryd" on the delete confirmation goes to the list of all runs, without a message
  public cancelDelete(): void {
    this.router.navigate(['/runs']);
  }
}
