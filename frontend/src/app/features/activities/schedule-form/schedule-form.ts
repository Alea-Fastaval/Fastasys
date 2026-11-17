import { Component, OnInit, signal, inject } from '@angular/core';
import { HttpClient, HttpErrorResponse, HttpStatusCode } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { forkJoin } from 'rxjs';
import { Activity, ActivitySchedule, Room, UpdateScheduleDto } from '@shared/types';
import { Card } from '@shared/components';
import { TranslatePipe } from '@shared/pipes';
import { conventionLocalToIso, toConventionLocalInput } from '@shared/utils';
import { ActivityFlashService } from '../activity-flash.service';
import { ScheduleForm } from '../activity-detail/activity-detail';

// Infosys fills in the schedule's start and end (Copenhagen time) and selects its meeting room
export function toScheduleForm(schedule: ActivitySchedule): ScheduleForm {
  return {
    start: toConventionLocalInput(schedule.startTime),
    end: toConventionLocalInput(schedule.endTime),
    roomId: schedule.roomId,
  };
}

// The Infosys room select has no empty option, so for a schedule without a meeting room the browser shows the
// first room, and "Opdater" saves that room
export function withDefaultRoom(form: ScheduleForm, rooms: Room[]): ScheduleForm {
  return form.roomId === null && rooms.length > 0 ? { ...form, roomId: rooms[0].id } : form;
}

// Infosys aktivitet/editafvikling/:id: (start, slut and mødelokale; Opdater, Fortryd and Slet) and its delete
// confirmation. Every outcome returns to the activity page.
@Component({
  selector: 'app-schedule-form',
  imports: [FormsModule, MatButtonModule, MatFormFieldModule, MatInputModule, MatSelectModule, Card, TranslatePipe],
  templateUrl: './schedule-form.html',
  styleUrl: './schedule-form.scss',
})
export class ScheduleFormComponent implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly flash = inject(ActivityFlashService);

  public readonly activityTitle = signal('');
  public readonly form = signal<ScheduleForm | null>(null);
  public readonly rooms = signal<Room[]>([]);
  public readonly notFound = signal(false);
  public readonly busy = signal(false);
  public readonly confirmingDelete = signal(false);

  public activityId = '';
  public scheduleId = '';

  public ngOnInit(): void {
    this.activityId = this.route.snapshot.paramMap.get('id') ?? '';
    this.scheduleId = this.route.snapshot.paramMap.get('scheduleId') ?? '';

    forkJoin({
      activity: this.http.get<Activity>(`/api/activities/${this.activityId}`),
      schedule: this.http.get<ActivitySchedule>(this.scheduleUrl()),
    }).subscribe({
      next: ({ activity, schedule }) => {
        this.activityTitle.set(activity.title);
        this.form.set(toScheduleForm(schedule));
        this.loadRooms();
      },
      error: () => this.notFound.set(true),
    });
  }

  // Infosys returns to the activity with a success or an error message
  public submit(): void {
    const form = this.form();
    if (!form) return;

    const startTime = conventionLocalToIso(form.start);
    const endTime = conventionLocalToIso(form.end);
    if (!startTime || !endTime) {
      this.returnToActivity('error', 'scheduleUpdateFailed');
      return;
    }

    const dto: UpdateScheduleDto = { startTime, endTime, roomId: form.roomId };
    this.busy.set(true);
    this.http.put<ActivitySchedule>(this.scheduleUrl(), dto).subscribe({
      next: () => this.returnToActivity('success', 'scheduleUpdated'),
      error: () => this.returnToActivity('error', 'scheduleUpdateFailed'),
    });
  }

  // Infosys "Fortryd" returns to the activity with "Afviklingen blev ikke opdateret."
  public cancel(): void {
    this.returnToActivity('error', 'scheduleNotUpdated');
  }

  public askDelete(): void {
    this.confirmingDelete.set(true);
  }

  // Infosys can't delete a schedule that has runs and then shows a generic error page.
  // Here the message says why (agreed 2026-11-05).
  public confirmDelete(): void {
    this.busy.set(true);
    this.http.delete(this.scheduleUrl()).subscribe({
      next: () => this.returnToActivity('success', 'scheduleDeleted'),
      error: (err: HttpErrorResponse) =>
        this.returnToActivity(
          'error',
          err.status === HttpStatusCode.Conflict ? 'scheduleDeleteHasRuns' : 'scheduleDeleteFailed',
        ),
    });
  }

  // Infosys "Fortryd" on the delete confirmation returns to the activity, without a message
  public cancelDelete(): void {
    this.router.navigate(['/activities', this.activityId]);
  }

  private loadRooms(): void {
    this.http.get<Room[]>('/api/rooms').subscribe({
      next: rooms => {
        this.rooms.set(rooms);
        const form = this.form();
        if (form) this.form.set(withDefaultRoom(form, rooms));
      },
      error: err => console.error('Failed to load rooms:', err),
    });
  }

  private returnToActivity(type: 'success' | 'error', key: string): void {
    this.flash.set(type, key);
    this.router.navigate(['/activities', this.activityId]);
  }

  private scheduleUrl(): string {
    return `/api/activities/${this.activityId}/schedules/${this.scheduleId}`;
  }
}
