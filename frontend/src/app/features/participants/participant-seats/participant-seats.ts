import { Component, OnInit, inject, signal } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatSelectModule } from '@angular/material/select';
import {
  ParticipantSeat,
  ParticipantSeats,
  RunSeatRole,
  SeatActivityOption,
  SeatRunOption,
  SeatScheduleOption,
  UpdateParticipantSeatsDto,
} from '@shared/types';
import { Card } from '@shared/components';
import { TimeSpanPipe, TranslatePipe } from '@shared/pipes';
import { ParticipantFlashService } from '../participant-flash.service';

// The API refuses an activity-locked participant with 409 and reason "locked" (409 alone can also mean that a seat
// was taken at the same time)
export function isLocked(err: HttpErrorResponse): boolean {
  return err.status === 409 && (err.error as { reason?: string } | null)?.reason === 'locked';
}

// Infosys ActivityController::ajaxGetHold disables a run that can use no more players and needs no more game masters
export function runOptionDisabled(run: SeatRunOption): boolean {
  return !run.canUsePlayers && !run.needsGameMasters;
}

// Infosys ActivityController::ajaxGetHoldNeeds disables Spiller when the run can use no more players and Spilleder
// when it needs no more game masters
export function roleOptionDisabled(run: SeatRunOption, role: RunSeatRole): boolean {
  return role === 'Player' ? !run.canUsePlayers : !run.needsGameMasters;
}

// "Tilføj Aktivitet" (imwedit.js setupAktivitet): needs all four choices, and a run can only be in the list once
export function canAddSeat(
  seats: ParticipantSeat[],
  activity: SeatActivityOption | undefined,
  schedule: SeatScheduleOption | undefined,
  run: SeatRunOption | undefined,
  role: RunSeatRole | null,
): boolean {
  return !!activity && !!schedule && !!run && role !== null && !seats.some(seat => seat.runId === run.id);
}

// Infosys deltager/updateaktivitet/:id: ("Opdater Aktiviteter", the "Ret/slet" link of the Aktiviteter box). Pick an
// activity, a time, a run and a role, add it to the list, remove what should go, and "Opdater" saves the list.
// The list holds the participant's seats; Infosys also lists a designer's rows without a run, which can't be edited.
// Infosys shows the participant's signup wishes below with a "Tilføj" button each; that comes with the signup wishes.
@Component({
  selector: 'app-participant-seats',
  imports: [
    FormsModule,
    RouterLink,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatSelectModule,
    Card,
    TimeSpanPipe,
    TranslatePipe,
  ],
  templateUrl: './participant-seats.html',
  styleUrl: './participant-seats.scss',
})
export class ParticipantSeatsComponent implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly flash = inject(ParticipantFlashService);

  public readonly participantId = Number(this.route.snapshot.paramMap.get('id'));
  public readonly page = signal<ParticipantSeats | null>(null);
  public readonly seats = signal<ParticipantSeat[]>([]);
  public readonly notFound = signal(false);
  public readonly busy = signal(false);

  public activityId: number | null = null;
  public scheduleId: number | null = null;
  public runId: number | null = null;
  public role: RunSeatRole | null = null;

  public readonly roleOptionDisabled = roleOptionDisabled;
  public readonly runOptionDisabled = runOptionDisabled;

  public ngOnInit(): void {
    this.http.get<ParticipantSeats>(`/api/participants/${this.participantId}/seats`).subscribe({
      next: page => {
        this.page.set(page);
        this.seats.set(page.seats);
      },
      error: (err: HttpErrorResponse) => {
        if (isLocked(err)) {
          // Infosys: "Deltagerens aktiviteter kan ikke ændres." on the participant page
          this.backToParticipant('error', 'activitiesLocked');
        } else {
          this.notFound.set(true);
        }
      },
    });
  }

  public activity(): SeatActivityOption | undefined {
    return this.page()?.activities.find(a => a.id === this.activityId);
  }

  public schedule(): SeatScheduleOption | undefined {
    return this.activity()?.schedules.find(s => s.id === this.scheduleId);
  }

  public run(): SeatRunOption | undefined {
    return this.schedule()?.runs.find(r => r.id === this.runId);
  }

  // Choosing again further up clears the choices below it, like Infosys's selects
  public activityChanged(): void {
    this.scheduleId = null;
    this.runId = null;
    this.role = null;
  }

  public scheduleChanged(): void {
    this.runId = null;
    this.role = null;
  }

  public runChanged(): void {
    this.role = null;
  }

  public canAdd(): boolean {
    return canAddSeat(this.seats(), this.activity(), this.schedule(), this.run(), this.role);
  }

  public add(): void {
    const activity = this.activity();
    const schedule = this.schedule();
    const run = this.run();
    const role = this.role;
    if (!activity || !schedule || !run || role === null || !this.canAdd()) return;

    this.seats.update(seats => [
      ...seats,
      {
        runId: run.id,
        runNumber: run.number,
        role,
        title: activity.title,
        titleEnglish: activity.titleEnglish,
        start: schedule.start,
        end: schedule.end,
      },
    ]);
  }

  public remove(seat: ParticipantSeat): void {
    this.seats.update(seats => seats.filter(s => s !== seat));
  }

  // Infosys goes back to the participant with "Deltageren blev opdateret." or an error
  public submit(): void {
    const dto: UpdateParticipantSeatsDto = {
      seats: this.seats().map(seat => ({ runId: seat.runId, role: seat.role })),
    };
    this.busy.set(true);
    this.http.put(`/api/participants/${this.participantId}/seats`, dto).subscribe({
      next: () => {
        this.backToParticipant('success', 'updated');
      },
      error: (err: HttpErrorResponse) => {
        this.backToParticipant('error', isLocked(err) ? 'activitiesLocked' : 'activitiesUpdateFailed');
      },
    });
  }

  private backToParticipant(type: 'success' | 'error', key: string): void {
    this.flash.set(type, key);
    void this.router.navigate(['/participants', this.participantId]);
  }
}
