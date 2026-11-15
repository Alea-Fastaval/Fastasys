import { Component, EventEmitter, Input, Output, inject, signal } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { CdkDrag, CdkDragDrop, CdkDragHandle, CdkDropList, moveItemInArray } from '@angular/cdk/drag-drop';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import {
  ParticipantActivitySignup,
  ParticipantSignups,
  RunSeatRole,
  SignupActivityOption,
  SignupPrioritiesDto,
  SignupScheduleOption,
  SignupUpdateResult,
  UpdateSignupDto,
} from '@shared/types';
import { Card } from '@shared/components';
import { TimeSpanPipe, TranslatePipe } from '@shared/pipes';
import { TranslationService } from '@shared/services';

// The message (a key under participants.messages) for a refused signup request. The API gives a reason; Infosys's
// messages are kept where it had one.
export function signupErrorKey(err: HttpErrorResponse, fallback: string): string {
  switch ((err.error as { reason?: string } | null)?.reason) {
    case 'locked':
      return 'activitiesLocked';
    case 'alreadySignedUp':
      return 'signupAlreadySignedUp';
    case 'notSignedUp':
      return 'signupNotFound';
    case 'noGameMasters':
      return 'signupNoGameMasters';
    default:
      return fallback;
  }
}

// The row being changed in the box: the wish as it is, and the new schedule, priority and role
export interface SignupEditor {
  original: ParticipantActivitySignup;
  scheduleId: number;
  priority: number;
  role: RunSeatRole;
}

// The Aktivitetstilmeldinger box of the Infosys participant page (visdeltager.phtml, .activity-signup): the
// participant's signup wishes with Aktivitet, Tid, Prio and S / SL. "Ret/slet" opens the edit page
// (deltager/updatetilmelding). Clicking a row lets the wish's schedule, priority and role be changed, or the wish be
// deleted (Infosys participant.js opens a dialog; here the row turns into the editor, like the page's other fields).
// When player wishes at the same time then share a priority, the box asks to sort them ("Sorter efter prioritet").
// Neither works for an activity-locked participant (fixed 2026-11-13: Infosys's dialog ignored the lock).
@Component({
  selector: 'app-participant-activity-signups',
  imports: [
    FormsModule,
    RouterLink,
    CdkDropList,
    CdkDrag,
    CdkDragHandle,
    MatButtonModule,
    MatIconModule,
    Card,
    TimeSpanPipe,
    TranslatePipe,
  ],
  templateUrl: './participant-activity-signups.html',
  styleUrl: './participant-activity-signups.scss',
})
export class ParticipantActivitySignupsComponent {
  private readonly http = inject(HttpClient);
  private readonly translation = inject(TranslationService);

  // Decorator inputs, like Card's: the unit tests run Angular in JIT mode, which doesn't know signal inputs
  @Input({ required: true }) public signups: ParticipantActivitySignup[] = [];
  @Input({ required: true }) public participantId = 0;
  // The participant page allows changes unless the participant is activity-locked
  @Input() public canEdit = false;

  // A wish was changed or deleted; the page reloads the participant (Infosys reloads the page). A decorator output:
  // the JIT-mode unit tests don't know output() either, so a template's (changed) would never fire there.
  @Output() public readonly changed = new EventEmitter<void>();

  public readonly options = signal<ParticipantSignups | null>(null);
  public readonly editor = signal<SignupEditor | null>(null);
  public readonly conflicts = signal<ParticipantActivitySignup[] | null>(null);
  public readonly errorKey = signal<string | null>(null);
  public readonly busy = signal(false);

  public startEdit(signup: ParticipantActivitySignup): void {
    if (!this.canEdit || this.busy()) return;
    this.errorKey.set(null);
    const open = (): void => {
      this.editor.set({
        original: signup,
        scheduleId: signup.scheduleId,
        priority: signup.priority,
        role: signup.role,
      });
    };
    if (this.options()) {
      open();
      return;
    }
    this.http.get<ParticipantSignups>(`/api/participants/${this.participantId}/signups`).subscribe({
      next: options => {
        this.options.set(options);
        open();
      },
      error: (err: HttpErrorResponse) => {
        this.errorKey.set(signupErrorKey(err, 'signupUpdateFailed'));
      },
    });
  }

  public isEditing(signup: ParticipantActivitySignup): boolean {
    const original = this.editor()?.original;
    return original?.scheduleId === signup.scheduleId && original.role === signup.role;
  }

  public activity(editor: SignupEditor): SignupActivityOption | undefined {
    return this.options()?.activities.find(a => a.id === editor.original.activityId);
  }

  // The wish's activity's schedules (Infosys aktiviteter/ajax/activity-schedules)
  public schedules(editor: SignupEditor): SignupScheduleOption[] {
    return this.activity(editor)?.schedules ?? [];
  }

  // An activity without game masters can't get a new game master wish (fixed 2026-11-13: Infosys dropped it silently)
  public gameMasterDisabled(editor: SignupEditor): boolean {
    return this.activity(editor)?.hasGameMasters === false && editor.original.role !== 'GameMaster';
  }

  public cancel(): void {
    this.editor.set(null);
  }

  // Infosys deltager/ajax/updateschedule
  public update(): void {
    const editor = this.editor();
    if (!editor) return;
    const { original } = editor;
    const dto: UpdateSignupDto = {
      scheduleId: editor.scheduleId,
      role: editor.role,
      priority: Number(editor.priority),
    };
    this.busy.set(true);
    this.http.put<SignupUpdateResult>(`${this.signupsUrl()}/${original.scheduleId}/${original.role}`, dto).subscribe({
      next: result => {
        this.busy.set(false);
        this.editor.set(null);
        if (result.status === 'conflict') {
          this.conflicts.set([...result.conflicts]);
        } else {
          this.changed.emit();
        }
      },
      error: (err: HttpErrorResponse) => {
        this.busy.set(false);
        this.errorKey.set(signupErrorKey(err, 'signupUpdateFailed'));
      },
    });
  }

  // Infosys deltager/ajax/removeschedule, after "Er du sikker?"
  public remove(): void {
    const editor = this.editor();
    if (!editor || !confirm(this.translation.translate('participants.signups.confirmDelete'))) return;
    const { original } = editor;
    this.busy.set(true);
    this.http.delete(`${this.signupsUrl()}/${original.scheduleId}/${original.role}`).subscribe({
      next: () => {
        this.busy.set(false);
        this.editor.set(null);
        this.changed.emit();
      },
      error: (err: HttpErrorResponse) => {
        this.busy.set(false);
        this.errorKey.set(signupErrorKey(err, 'signupDeleteFailed'));
      },
    });
  }

  public drop(event: CdkDragDrop<ParticipantActivitySignup[]>): void {
    const list = [...(this.conflicts() ?? [])];
    moveItemInArray(list, event.previousIndex, event.currentIndex);
    this.conflicts.set(list);
  }

  // "Sorter efter prioritet" (Infosys deltager/ajax/updateschedulepriorities): the order gives priorities 1, 2, 3, 4
  public savePriorities(): void {
    const list = this.conflicts();
    if (!list) return;
    const dto: SignupPrioritiesDto = { scheduleIds: list.map(signup => signup.scheduleId) };
    this.busy.set(true);
    this.http.put(`${this.signupsUrl()}/priorities`, dto).subscribe({
      next: () => {
        this.busy.set(false);
        this.conflicts.set(null);
        this.changed.emit();
      },
      error: (err: HttpErrorResponse) => {
        this.busy.set(false);
        this.errorKey.set(signupErrorKey(err, 'signupUpdateFailed'));
      },
    });
  }

  private signupsUrl(): string {
    return `/api/participants/${this.participantId}/signups`;
  }
}
