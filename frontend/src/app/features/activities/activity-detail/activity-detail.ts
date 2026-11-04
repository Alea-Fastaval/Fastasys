import { Component, OnInit, signal, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { Activity, ActivitySchedule, CreateRunDto, CreateScheduleDto, Room, Run } from '@shared/types';
import { Card } from '@shared/components';
import { TimeSpanPipe, TranslatePipe } from '@shared/pipes';
import { TranslationService } from '@shared/services';
import { conventionLocalToIso } from '@shared/utils';
import { ActivityFlashService } from '../activity-flash.service';

// datetime-local values in Copenhagen time
export interface ScheduleForm {
  start: string;
  end: string;
  roomId: number | null;
}

export interface PageMessage {
  type: 'success' | 'error';
  // Full translation key
  key: string;
}

@Component({
  selector: 'app-activity-detail',
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
  templateUrl: './activity-detail.html',
  styleUrl: './activity-detail.scss',
})
export class ActivityDetailComponent implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly route = inject(ActivatedRoute);
  private readonly translation = inject(TranslationService);

  public readonly flash = inject(ActivityFlashService).take();
  public readonly activity = signal<Activity | null>(null);
  public readonly notFound = signal(false);
  public readonly schedules = signal<ActivitySchedule[]>([]);
  public readonly rooms = signal<Room[]>([]);
  public readonly scheduleForm = signal<ScheduleForm | null>(null);
  public readonly message = signal<PageMessage | null>(null);
  public readonly busy = signal(false);

  private activityId = '';

  public ngOnInit(): void {
    this.activityId = this.route.snapshot.paramMap.get('id') ?? '';
    this.http.get<Activity>(`/api/activities/${this.activityId}`).subscribe({
      next: data => {
        this.activity.set(data);
        this.loadSchedules();
      },
      error: () => this.notFound.set(true),
    });
  }

  // Infosys "Opret afvikling" opens a form with start, end and a meeting room; the first room is preselected
  public openScheduleForm(): void {
    this.scheduleForm.set({ start: '', end: '', roomId: null });
    this.http.get<Room[]>('/api/rooms').subscribe({
      next: rooms => {
        this.rooms.set(rooms);
        const form = this.scheduleForm();
        if (form && form.roomId === null && rooms.length > 0) {
          this.scheduleForm.set({ ...form, roomId: rooms[0].id });
        }
      },
      error: err => console.error('Failed to load rooms:', err),
    });
  }

  public cancelScheduleForm(): void {
    this.scheduleForm.set(null);
  }

  public createSchedule(): void {
    const form = this.scheduleForm();
    if (!form) return;

    const startTime = conventionLocalToIso(form.start);
    const endTime = conventionLocalToIso(form.end);
    if (!startTime || !endTime) {
      this.show('error', 'activities.messages.scheduleCreateFailed');
      return;
    }

    const dto: CreateScheduleDto = { startTime, endTime, roomId: form.roomId };
    this.busy.set(true);
    this.http.post<ActivitySchedule>(`/api/activities/${this.activityId}/schedules`, dto).subscribe({
      next: () => {
        this.scheduleForm.set(null);
        this.show('success', 'activities.messages.scheduleCreated');
        this.loadSchedules();
      },
      error: () => this.show('error', 'activities.messages.scheduleCreateFailed'),
    });
  }

  // Infosys "Tilføj hold": the server picks the next number and the first free room
  public createRun(schedule: ActivitySchedule): void {
    const dto: CreateRunDto = { scheduleId: schedule.id };
    this.busy.set(true);
    this.http.post<Run>('/api/runs', dto).subscribe({
      next: () => {
        this.message.set(null);
        this.loadSchedules();
      },
      error: () => this.show('error', 'runs.messages.createFailed'),
    });
  }

  // Infosys "Slet hold" deletes an empty run straight away; with participants it asks first and then removes
  // their seats too
  public deleteRun(run: Run): void {
    const removeParticipants = run.seats.length > 0;
    if (removeParticipants && !confirm(this.translation.translate('runs.messages.deleteWithParticipants'))) {
      return;
    }

    this.busy.set(true);
    this.http.delete(`/api/runs/${run.id}`, { params: { removeParticipants } }).subscribe({
      next: () => {
        this.message.set(null);
        this.loadSchedules();
      },
      error: () => this.show('error', 'runs.messages.deleteFromActivityFailed'),
    });
  }

  private loadSchedules(): void {
    this.http.get<ActivitySchedule[]>(`/api/activities/${this.activityId}/schedules`).subscribe({
      next: schedules => {
        this.schedules.set(schedules);
        this.busy.set(false);
      },
      error: err => {
        this.busy.set(false);
        console.error('Failed to load schedules:', err);
      },
    });
  }

  private show(type: PageMessage['type'], key: string): void {
    this.busy.set(false);
    this.message.set({ type, key });
  }
}
