import { Component, OnInit, inject, signal } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatSelectModule } from '@angular/material/select';
import {
  ParticipantActivitySignup,
  ParticipantSignups,
  RunSeatRole,
  SignupActivityOption,
  SignupScheduleOption,
  UpdateParticipantSignupsDto,
} from '@shared/types';
import { Card } from '@shared/components';
import { TimeSpanPipe, TranslatePipe } from '@shared/pipes';
import { ParticipantFlashService } from '../participant-flash.service';
import { signupErrorKey } from '../participant-activity-signups/participant-activity-signups';

// Infosys offers priorities 1-4 for a player wish
export const SIGNUP_PRIORITIES = [1, 2, 3, 4] as const;

// The priority a new wish gets: the chosen one for a player, 1 for a game master (imwedit.js setupTilmeldinger)
export function newSignupPriority(role: RunSeatRole, priority: number): number {
  return role === 'GameMaster' ? 1 : priority;
}

// "Tilføj Tilmelding": needs an activity, a time and a role, and the same schedule and role can only be listed once
// (fixed 2026-11-13: Infosys's duplicate check never matched, so it could be added twice)
export function canAddSignup(
  signups: ParticipantActivitySignup[],
  activity: SignupActivityOption | undefined,
  schedule: SignupScheduleOption | undefined,
  role: RunSeatRole | null,
): boolean {
  return (
    !!activity &&
    !!schedule &&
    role !== null &&
    !(role === 'GameMaster' && !activity.hasGameMasters) &&
    !signups.some(s => s.scheduleId === schedule.id && s.role === role)
  );
}

// Infosys deltager/updatetilmelding/:id: ("Opdater Aktivitets-tilmeldinger", the "Ret/slet" link of the
// Aktivitetstilmeldinger box). Pick an activity, a time, a role and, for a player, a priority; add it to the list,
// remove what should go, and "Opdater" saves the list. Removing a wish and adding it again with another priority
// changes its priority (fixed 2026-11-13: Infosys kept the old one).
@Component({
  selector: 'app-participant-signups',
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
  templateUrl: './participant-signups.html',
  styleUrl: './participant-signups.scss',
})
export class ParticipantSignupsComponent implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly flash = inject(ParticipantFlashService);

  public readonly participantId = Number(this.route.snapshot.paramMap.get('id'));
  public readonly page = signal<ParticipantSignups | null>(null);
  public readonly signups = signal<ParticipantActivitySignup[]>([]);
  public readonly notFound = signal(false);
  public readonly busy = signal(false);
  public readonly priorities = SIGNUP_PRIORITIES;

  public activityId: number | null = null;
  public scheduleId: number | null = null;
  public role: RunSeatRole | null = null;
  public priority = 1;

  public ngOnInit(): void {
    this.http.get<ParticipantSignups>(`/api/participants/${this.participantId}/signups`).subscribe({
      next: page => {
        this.page.set(page);
        this.signups.set(page.signups);
      },
      error: (err: HttpErrorResponse) => {
        if (signupErrorKey(err, '') === 'activitiesLocked') {
          this.backToParticipant('error', 'activitiesLocked');
        } else {
          this.notFound.set(true);
        }
      },
    });
  }

  // Infosys offers the activities that can be signed up for
  public activities(page: ParticipantSignups): SignupActivityOption[] {
    return page.activities.filter(a => a.canSignUp);
  }

  public activity(): SignupActivityOption | undefined {
    return this.page()?.activities.find(a => a.id === this.activityId);
  }

  public schedule(): SignupScheduleOption | undefined {
    return this.activity()?.schedules.find(s => s.id === this.scheduleId);
  }

  // Choosing another activity clears the time and the role, like Infosys's selects
  public activityChanged(): void {
    this.scheduleId = null;
    this.role = null;
  }

  public canAdd(): boolean {
    return canAddSignup(this.signups(), this.activity(), this.schedule(), this.role);
  }

  public add(): void {
    const activity = this.activity();
    const schedule = this.schedule();
    const role = this.role;
    if (!activity || !schedule || role === null || !this.canAdd()) return;

    this.signups.update(signups => [
      ...signups,
      {
        activityId: activity.id,
        title: activity.title,
        titleEnglish: activity.titleEnglish,
        scheduleId: schedule.id,
        start: schedule.start,
        end: schedule.end,
        priority: newSignupPriority(role, this.priority),
        role,
      },
    ]);
  }

  public remove(signup: ParticipantActivitySignup): void {
    this.signups.update(signups => signups.filter(s => s !== signup));
  }

  // Infosys goes back to the participant with "Deltageren blev opdateret." or an error
  public submit(): void {
    const dto: UpdateParticipantSignupsDto = {
      signups: this.signups().map(s => ({ scheduleId: s.scheduleId, role: s.role, priority: s.priority })),
    };
    this.busy.set(true);
    this.http.put(`/api/participants/${this.participantId}/signups`, dto).subscribe({
      next: () => {
        this.backToParticipant('success', 'updated');
      },
      error: (err: HttpErrorResponse) => {
        this.backToParticipant('error', signupErrorKey(err, 'activitiesUpdateFailed'));
      },
    });
  }

  private backToParticipant(type: 'success' | 'error', key: string): void {
    this.flash.set(type, key);
    void this.router.navigate(['/participants', this.participantId]);
  }
}
