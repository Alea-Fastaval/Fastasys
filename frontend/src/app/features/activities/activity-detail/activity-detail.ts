import { Component, OnInit, signal, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { Observable, concatMap, of } from 'rxjs';
import {
  Activity,
  ActivitySchedule,
  CreateRunDto,
  CreateRunSeatDto,
  CreateScheduleDto,
  Room,
  Run,
  RunSeat,
  ScheduleSignups,
  SignupPoolRow,
} from '@shared/types';
import { Card } from '@shared/components';
import { TimeSpanPipe, TranslatePipe } from '@shared/pipes';
import { AuthService, TranslationService } from '@shared/services';
import { conventionLocalToIso } from '@shared/utils';
import { ActivityFlashService } from '../activity-flash.service';
import { DragItem, SeatChange, canDragRow, canDragSeat, seatChangeForDrop, visibleRows } from './signup-pool';

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

  // The "Tilmeldinger" tables, one per schedule
  public readonly signups = signal<ScheduleSignups[]>([]);
  // Placing participants by dragging needs the right to edit activities (Infosys: Activity-admin, Infonaut, admin)
  public readonly mayPlace = inject(AuthService).hasPrivilege('activities_edit');
  public readonly dragging = signal<DragItem | null>(null);
  public readonly hoveredRunId = signal<number | null>(null);
  // "scheduleId-participantId" of a drop that was refused, for the red flash
  public readonly failed = signal<string | null>(null);

  private activityId = '';
  private dropHandled = false;

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

  // --- The "Tilmeldinger" tables and dragging participants onto runs (Infosys activity.js) ---

  // The rows the table shows (participants who are on a team are hidden)
  public poolRows(scheduleId: number): SignupPoolRow[] {
    return visibleRows(this.allRows(scheduleId));
  }

  public canDragRow(row: SignupPoolRow): boolean {
    return this.mayPlace && canDragRow(row);
  }

  public canDragSeat(scheduleId: number, participantId: number): boolean {
    return this.mayPlace && canDragSeat(this.allRows(scheduleId), participantId);
  }

  // Hovering a row shows the participant's languages, age, wanted activities and note (Infosys's info popup)
  public rowInfo(row: SignupPoolRow): string {
    const t = (key: string): string => this.translation.translate(key);
    const languages = row.activityLanguages.map(l => t(`participants.search.languages.${l}`)).join(', ');
    return [
      `${t('participants.fields.activityLanguages')}: ${languages}`,
      `${t('activities.signups.age')}: ${row.age ?? ''}`,
      `${t('participants.fields.desiredActivities')}: ${row.desiredActivities}`,
      `${t('participants.notes.commentNote')}: ${row.note ?? ''}`,
    ].join('\n');
  }

  public isFailed(scheduleId: number, participantId: number): boolean {
    return this.failed() === `${scheduleId}-${participantId}`;
  }

  public startRowDrag(event: DragEvent, scheduleId: number, row: SignupPoolRow): void {
    if (!this.canDragRow(row)) return;
    this.startDrag(event, { scheduleId, participantId: row.participantId, role: row.role, fromRunId: null });
  }

  // A participant moved to another run keeps the role of their seat (agreed 2026-11-14)
  public startSeatDrag(event: DragEvent, scheduleId: number, run: Run, seat: RunSeat): void {
    if (!this.canDragSeat(scheduleId, seat.participantId)) return;
    this.startDrag(event, { scheduleId, participantId: seat.participantId, role: seat.role, fromRunId: run.id });
  }

  // Only the runs of the dragged participant's schedule take a drop; the other schedules are greyed out
  public dragOverRun(event: DragEvent, scheduleId: number, run: Run): void {
    if (this.dragging()?.scheduleId !== scheduleId) return;
    event.preventDefault();
    if (event.dataTransfer) event.dataTransfer.dropEffect = 'move';
    this.hoveredRunId.set(run.id);
  }

  public dragLeaveRun(run: Run): void {
    if (this.hoveredRunId() === run.id) this.hoveredRunId.set(null);
  }

  public dropOnRun(event: DragEvent, scheduleId: number, run: Run): void {
    const item = this.dragging();
    if (item?.scheduleId !== scheduleId) return;
    event.preventDefault();
    this.dropHandled = true;
    this.applyDrop(item, seatChangeForDrop(item, run.id));
  }

  // The drag ended anywhere but on a run: a participant dragged off a run leaves it (Infosys removeFromGroups)
  public endDrag(): void {
    const item = this.dragging();
    if (item && !this.dropHandled) this.applyDrop(item, seatChangeForDrop(item, null));
    this.dragging.set(null);
    this.hoveredRunId.set(null);
    this.dropHandled = false;
  }

  private allRows(scheduleId: number): SignupPoolRow[] {
    return this.signups().find(s => s.scheduleId === scheduleId)?.signups ?? [];
  }

  private startDrag(event: DragEvent, item: DragItem): void {
    // Firefox only starts a drag with some data
    event.dataTransfer?.setData('text/plain', String(item.participantId));
    if (event.dataTransfer) event.dataTransfer.effectAllowed = 'move';
    this.dropHandled = false;
    this.dragging.set(item);
  }

  // Infosys groups/scheduleparticipant: off the old run, then onto the new one, with the runs' own checks. The page
  // is reloaded either way; a refused drop flashes the row red, as in Infosys.
  private applyDrop(item: DragItem, change: SeatChange | null): void {
    if (!change) return;
    const { participantId } = item;
    const add = change.add;
    const remove$: Observable<unknown> =
      change.removeFromRunId === null
        ? of(null)
        : this.http.delete(`/api/runs/${change.removeFromRunId}/seats/${participantId}`);
    const add$: Observable<unknown> =
      add === null
        ? of(null)
        : this.http.post<Run>(`/api/runs/${add.runId}/seats`, {
            participantId,
            role: add.role,
          } satisfies CreateRunSeatDto);
    this.busy.set(true);
    remove$.pipe(concatMap(() => add$)).subscribe({
      next: () => {
        this.loadSchedules();
      },
      error: () => {
        const key = `${item.scheduleId}-${participantId}`;
        this.failed.set(key);
        setTimeout(() => {
          if (this.failed() === key) this.failed.set(null);
        }, 700);
        this.loadSchedules();
      },
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
    // The tables change with the runs (who is on a team)
    this.http.get<ScheduleSignups[]>(`/api/activities/${this.activityId}/signups`).subscribe({
      next: signups => this.signups.set(signups),
      error: err => console.error('Failed to load signups:', err),
    });
  }

  private show(type: PageMessage['type'], key: string): void {
    this.busy.set(false);
    this.message.set({ type, key });
  }
}
