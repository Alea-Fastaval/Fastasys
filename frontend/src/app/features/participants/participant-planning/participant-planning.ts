import { Component, DestroyRef, OnInit, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';
import { forkJoin, Subscription } from 'rxjs';
import { Card } from '@shared/components';
import { TimeSpanPipe, TranslatePipe } from '@shared/pipes';
import { AuthService, TranslationService } from '@shared/services';
import {
  ActivitySchedule,
  ActivitySignup,
  ActivitySignupFields,
  ActivitySummary,
  ParticipantKarma,
  RunSeatRole,
} from '@shared/types';

interface PreferenceForm {
  activityId: number | null;
  scheduleId: number | null;
  role: RunSeatRole | null;
  priority: number | null;
}

@Component({
  selector: 'app-participant-planning',
  imports: [
    FormsModule,
    RouterLink,
    MatButtonModule,
    MatFormFieldModule,
    MatSelectModule,
    Card,
    TimeSpanPipe,
    TranslatePipe,
  ],
  templateUrl: './participant-planning.html',
  styleUrl: './participant-planning.scss',
})
export class ParticipantPlanningComponent implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly route = inject(ActivatedRoute);
  private readonly auth = inject(AuthService);
  private readonly translation = inject(TranslationService);
  private readonly destroyRef = inject(DestroyRef);
  private scheduleRequest?: Subscription;
  private activityRequest?: Subscription;
  private loadRequest?: Subscription;
  private mutationRequest?: Subscription;
  public readonly report = signal<ParticipantKarma | null>(null);
  public readonly signups = signal<ActivitySignup[]>([]);
  public readonly activities = signal<ActivitySummary[]>([]);
  public readonly schedules = signal<ActivitySchedule[]>([]);
  public readonly loading = signal(false);
  public readonly saving = signal(false);
  public readonly schedulesLoading = signal(false);
  public readonly loadError = signal<string | null>(null);
  public readonly saveError = signal<string | null>(null);
  public readonly message = signal<string | null>(null);
  public readonly formOpen = signal(false);
  public readonly canEdit = computed(() => this.auth.hasAnyPrivilege('activities_edit'));
  public editingId: number | null = null;
  public form: PreferenceForm = this.emptyForm();
  private participantId = 0;

  public ngOnInit(): void {
    this.route.paramMap.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(params => {
      this.mutationRequest?.unsubscribe();
      this.cancel();
      this.saving.set(false);
      this.message.set(null);
      this.report.set(null);
      this.signups.set([]);
      this.participantId = Number(params.get('id'));
      this.load();
    });
  }

  public refresh(): void {
    if (this.loading() || this.saving() || this.formOpen()) return;
    this.message.set(null);
    this.load(true);
  }

  public load(refreshed = false): void {
    this.loadRequest?.unsubscribe();
    this.loading.set(true);
    this.loadError.set(null);
    this.loadRequest = forkJoin({
      report: this.http.get<ParticipantKarma>(`/api/participants/${this.participantId}/karma`),
      signups: this.http.get<ActivitySignup[]>(this.signupUrl()),
    })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: data => {
          this.report.set(data.report);
          this.signups.set(data.signups);
          if (refreshed) this.message.set('refreshed');
          this.loading.set(false);
        },
        error: (err: HttpErrorResponse) => {
          this.report.set(null);
          this.signups.set([]);
          this.loadError.set(err.status === 403 ? 'forbidden' : err.status === 404 ? 'notFound' : 'loadFailed');
          this.loading.set(false);
        },
      });
  }

  public create(): void {
    if (!this.canEdit() || this.saving()) return;
    this.cancel();
    this.editingId = null;
    this.form = this.emptyForm();
    this.openForm();
  }

  public edit(signup: ActivitySignup): void {
    if (!this.canEdit() || this.saving()) return;
    this.cancel();
    this.editingId = signup.id;
    this.form = {
      activityId: signup.activityId,
      scheduleId: signup.scheduleId,
      role: signup.role === 'Player' || signup.role === 'GameMaster' ? signup.role : null,
      priority: signup.priority,
    };
    this.openForm();
    this.loadSchedules();
  }

  private openForm(): void {
    this.formOpen.set(true);
    this.saveError.set(null);
    this.message.set(null);
    this.activityRequest = this.http
      .get<ActivitySummary[]>('/api/activities')
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: data => this.activities.set(data),
        error: () => this.saveError.set('optionsFailed'),
      });
  }

  public selectActivity(): void {
    this.form.scheduleId = null;
    this.loadSchedules();
  }

  private loadSchedules(): void {
    this.scheduleRequest?.unsubscribe();
    this.schedules.set([]);
    this.schedulesLoading.set(false);
    if (this.form.activityId === null) return;
    this.schedulesLoading.set(true);
    this.scheduleRequest = this.http
      .get<ActivitySchedule[]>(`/api/activities/${this.form.activityId}/schedules`)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: data => {
          this.schedules.set(data);
          this.schedulesLoading.set(false);
        },
        error: () => {
          this.saveError.set('optionsFailed');
          this.schedulesLoading.set(false);
        },
      });
  }

  public selectRole(): void {
    if (this.form.role === 'GameMaster' && this.form.priority === null) this.form.priority = 0;
    if (this.form.role === 'Player' && this.form.priority === 0) this.form.priority = null;
  }

  public canSave(): boolean {
    const f = this.form;
    return (
      this.canEdit() &&
      !this.saving() &&
      !this.schedulesLoading() &&
      f.activityId !== null &&
      this.schedules().some(s => s.id === f.scheduleId && s.activityId === f.activityId && !s.isCancelled) &&
      (f.role === 'Player' || f.role === 'GameMaster') &&
      f.priority !== null &&
      Number.isInteger(f.priority) &&
      f.priority >= (f.role === 'GameMaster' ? 0 : 1) &&
      f.priority <= 4
    );
  }

  public save(): void {
    if (!this.canSave()) return;
    const { scheduleId, role, priority } = this.form;
    if (scheduleId === null || role === null || priority === null) return;
    const dto: ActivitySignupFields = { scheduleId, role, priority };
    this.saving.set(true);
    this.saveError.set(null);
    const request =
      this.editingId === null
        ? this.http.post<ActivitySignup>(this.signupUrl(), dto)
        : this.http.put<ActivitySignup>(`${this.signupUrl()}/${this.editingId}`, dto);
    this.mutationRequest = request.pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => {
        this.saving.set(false);
        this.cancel();
        this.message.set('saved');
        this.load();
      },
      error: (err: HttpErrorResponse) => {
        this.saving.set(false);
        this.saveError.set(err.status === 409 ? 'conflict' : 'saveFailed');
      },
    });
  }

  public remove(signup: ActivitySignup): void {
    if (!this.canEdit() || this.saving() || !confirm(this.translation.translate('karma.removeConfirm'))) return;
    this.saving.set(true);
    this.saveError.set(null);
    this.mutationRequest = this.http
      .delete(`${this.signupUrl()}/${signup.id}`)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.saving.set(false);
          this.cancel();
          this.message.set('removed');
          this.load();
        },
        error: () => {
          this.saving.set(false);
          this.saveError.set('removeFailed');
        },
      });
  }

  public cancel(): void {
    this.activityRequest?.unsubscribe();
    this.scheduleRequest?.unsubscribe();
    this.schedulesLoading.set(false);
    this.schedules.set([]);
    this.formOpen.set(false);
    this.editingId = null;
    this.saveError.set(null);
  }

  private signupUrl(): string {
    return `/api/participants/${this.participantId}/activity-signups`;
  }
  private emptyForm(): PreferenceForm {
    return { activityId: null, scheduleId: null, role: 'Player', priority: null };
  }
}
