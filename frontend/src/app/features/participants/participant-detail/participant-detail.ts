import { Component, HostListener, WritableSignal, computed, inject, signal } from '@angular/core';
import { NgTemplateOutlet } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { Participant, ParticipantDetails, ParticipantOptions } from '@shared/types';
import { Card } from '@shared/components';
import { TranslatePipe } from '@shared/pipes';
import { ParticipantFlashService } from '../participant-flash.service';
import {
  PRONOUN_CODES,
  PRONOUN_FIRST_CHOICES,
  PronounChoice,
  changeFirstPronoun,
  formatBirthDate,
  formatConventionDate,
  formatConventionDateTime,
  pronounText,
  pronounValue,
  pronounWord,
  sortByDanish,
  toPronounChoice,
} from '../participant-format';

export type FieldKind = 'text' | 'number' | 'optionalNumber' | 'yesNo' | 'date' | 'select' | 'pronouns' | 'textarea';

export interface SelectOption {
  value: string | number | null;
  // Shown as is; null means translate labelKey instead
  label: string | null;
  labelKey?: string;
}

// A field on the Infosys participant page that can be edited by clicking it
export interface EditableField {
  // The API field name (PUT /api/participants/:id/fields/:field)
  field: string;
  // Translation key of the label
  label: string;
  kind: FieldKind;
  value: (p: Participant) => string | number | boolean | null;
  // The shown text, for the kinds that don't format themselves
  text?: (p: Participant) => string;
  options?: () => SelectOption[];
}

export interface FieldEditor {
  field: string;
  value: string | number | boolean | null;
  pronouns: PronounChoice;
}

// A field from Infosys that is only shown here
export interface ShownField {
  label: string;
  text: (p: Participant, d: ParticipantDetails) => string;
}

export interface NoteField extends EditableField {
  // Signup notes are only shown when the participant gave one (the key exists in Infosys deltager_note)
  optional: boolean;
}

const details = (p: Participant): ParticipantDetails => p.details as ParticipantDetails;

// The value sent to the API for what is in the editor
export function editorValue(kind: FieldKind, editor: FieldEditor): string | number | boolean | null {
  switch (kind) {
    case 'number':
      return editor.value === '' || editor.value === null ? 0 : Number(editor.value);
    case 'optionalNumber':
      return editor.value === '' || editor.value === null ? null : Number(editor.value);
    case 'date':
      return editor.value === '' ? null : editor.value;
    case 'pronouns':
      return pronounValue(editor.pronouns);
    default:
      return editor.value;
  }
}

// Infosys deltager/visdeltager/:id:. Only the sections that belong to ported features are shown; the sections for
// entrance/food, ID card, sleeping, wear, price breakdown, activities and hero force come with those features.
@Component({
  selector: 'app-participant-detail',
  imports: [FormsModule, NgTemplateOutlet, RouterLink, MatButtonModule, MatIconModule, Card, TranslatePipe],
  templateUrl: './participant-detail.html',
  styleUrl: './participant-detail.scss',
})
export class ParticipantDetailComponent {
  private readonly http = inject(HttpClient);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly flashService = inject(ParticipantFlashService);

  public readonly flash = signal(this.flashService.take());
  public readonly participant = signal<Participant | null>(null);
  public readonly notFound = signal(false);
  public readonly options = signal<ParticipantOptions | null>(null);
  public readonly editor = signal<FieldEditor | null>(null);
  public readonly busy = signal(false);
  // The field that was just saved or failed, for Infosys' green/red flash
  public readonly saved = signal<string | null>(null);
  public readonly failed = signal<string | null>(null);
  public readonly errorKey = signal<string | null>(null);

  public readonly pronounFirstChoices = PRONOUN_FIRST_CHOICES;
  public readonly pronounCodes = PRONOUN_CODES;
  public readonly pronounWord = pronounWord;
  public readonly formatBirthDate = formatBirthDate;

  private readonly categoryOptions = computed<SelectOption[]>(() =>
    (this.options()?.categories ?? []).map(c => ({ value: c.id, label: c.name })),
  );
  private readonly countryOptions = computed<SelectOption[]>(() =>
    sortByDanish(this.options()?.countries ?? [], c => c.nameDanish).map(c => ({ value: c.code, label: c.nameDanish })),
  );
  private readonly areaOptions = computed<SelectOption[]>(() => [
    { value: null, label: null, labelKey: 'participants.noOrganizerArea' },
    ...sortByDanish(this.options()?.organizerAreas ?? [], a => a.nameDanish).map(a => ({
      value: a.id,
      label: a.nameDanish,
    })),
  ]);
  private readonly gameOptions = computed<SelectOption[]>(() => [
    { value: null, label: null, labelKey: 'participants.noGame' },
    ...sortByDanish(this.options()?.games ?? [], g => g.title).map(g => ({ value: g.id, label: g.title })),
  ]);
  private readonly languageOptions: SelectOption[] = [
    { value: 'Danish', label: null, labelKey: 'participants.preferredLanguages.Danish' },
    { value: 'English', label: null, labelKey: 'participants.preferredLanguages.English' },
  ];

  // Infosys "Personlig info", the part every user with access sees. Navn and Adresse are one field each in
  // Infosys; they are split so a name or address with spaces is not split wrongly (agreed 2026-11-11).
  public readonly personalFields: EditableField[] = [
    { field: 'birthDate', label: 'participants.fields.birthDate', kind: 'date', value: p => p.birthDate ?? '' },
    {
      field: 'categoryId',
      label: 'participants.fields.category',
      kind: 'select',
      value: p => p.categoryId,
      text: p => p.categoryName,
      options: () => this.categoryOptions(),
    },
    { field: 'amountPaid', label: 'participants.fields.amountPaid', kind: 'number', value: p => p.amountPaid },
    { field: 'firstName', label: 'participants.fields.firstName', kind: 'text', value: p => p.firstName },
    { field: 'lastName', label: 'participants.fields.lastName', kind: 'text', value: p => p.lastName },
    { field: 'addressLine1', label: 'participants.fields.addressLine1', kind: 'text', value: p => p.addressLine1 },
    { field: 'addressLine2', label: 'participants.fields.addressLine2', kind: 'text', value: p => p.addressLine2 },
    { field: 'zipCode', label: 'participants.fields.zipCode', kind: 'text', value: p => p.zipCode },
    { field: 'city', label: 'participants.fields.city', kind: 'text', value: p => p.city },
    { field: 'phoneNumber', label: 'participants.fields.phoneNumber', kind: 'text', value: p => p.phoneNumber },
    { field: 'smsSignup', label: 'participants.fields.smsSignup', kind: 'yesNo', value: p => p.smsSignup },
    {
      field: 'countryCode',
      label: 'participants.fields.country',
      kind: 'select',
      value: p => p.countryCode,
      text: p => p.countryName,
      options: () => this.countryOptions(),
    },
    { field: 'email', label: 'participants.fields.email', kind: 'text', value: p => p.email },
    { field: 'nickname', label: 'participants.fields.nickname', kind: 'text', value: p => p.nickname },
    {
      field: 'pronouns',
      label: 'participants.fields.pronouns',
      kind: 'pronouns',
      value: p => p.pronouns,
      text: p => pronounText(p.pronouns, p.preferredLanguage),
    },
    // Infosys shows the stored code (da, en); its select says dansk and engelsk
    {
      field: 'preferredLanguage',
      label: 'participants.fields.preferredLanguage',
      kind: 'select',
      value: p => p.preferredLanguage,
      text: p => (p.preferredLanguage === 'Danish' ? 'da' : 'en'),
      options: () => this.languageOptions,
    },
  ];

  public readonly organizerFields: EditableField[] = [
    {
      field: 'organizerAreaId',
      label: 'participants.fields.organizerArea',
      kind: 'select',
      value: p => details(p).organizerAreaId,
      text: p => details(p).organizerAreaName ?? '',
      options: () => this.areaOptions(),
    },
    {
      field: 'isAuthorOrDesigner',
      label: 'participants.fields.isAuthorOrDesigner',
      kind: 'yesNo',
      value: p => details(p).isAuthorOrDesigner,
    },
    {
      field: 'gameActivityId',
      label: 'participants.fields.game',
      kind: 'select',
      value: p => details(p).gameActivityId,
      text: p => details(p).gameActivityTitle ?? '',
      options: () => this.gameOptions(),
    },
    {
      field: 'extraVouchers',
      label: 'participants.fields.extraVouchers',
      kind: 'number',
      value: p => details(p).extraVouchers,
    },
  ];

  // Infosys shows the raw set values
  public readonly languageFields: ShownField[] = [
    {
      label: 'participants.fields.activityLanguages',
      text: (_, d) => d.activityLanguages.map(l => LANGUAGE_VALUES[l]).join(', '),
    },
    {
      label: 'participants.fields.sleepingAreas',
      text: (_, d) => d.sleepingAreas.map(a => a.toLowerCase()).join(', '),
    },
  ];

  public readonly preferenceFields: EditableField[] = [
    {
      field: 'desiredHeroTasks',
      label: 'participants.fields.desiredHeroTasks',
      kind: 'number',
      value: p => details(p).desiredHeroTasks,
    },
    {
      field: 'desiredActivities',
      label: 'participants.fields.desiredActivities',
      kind: 'number',
      value: p => details(p).desiredActivities,
    },
    {
      field: 'superGameMaster',
      label: 'participants.fields.superGameMaster',
      kind: 'yesNo',
      value: p => details(p).superGameMaster,
    },
    {
      field: 'maxGameMasterRuns',
      label: 'participants.fields.maxGameMasterRuns',
      kind: 'optionalNumber',
      value: p => details(p).maxGameMasterRuns,
    },
    {
      field: 'juniorGameMaster',
      label: 'participants.fields.juniorGameMaster',
      kind: 'yesNo',
      value: p => details(p).juniorGameMaster,
    },
    {
      field: 'superRulesGuide',
      label: 'participants.fields.superRulesGuide',
      kind: 'yesNo',
      value: p => details(p).superRulesGuide,
    },
    {
      field: 'maxRulesGuideGames',
      label: 'participants.fields.maxRulesGuideGames',
      kind: 'optionalNumber',
      value: p => details(p).maxRulesGuideGames,
    },
    {
      field: 'setupHelpMonday',
      label: 'participants.fields.setupHelpMonday',
      kind: 'yesNo',
      value: p => details(p).setupHelpMonday,
    },
    {
      field: 'setupHelpTuesday',
      label: 'participants.fields.setupHelpTuesday',
      kind: 'yesNo',
      value: p => details(p).setupHelpTuesday,
    },
    {
      field: 'financialSupport',
      label: 'participants.fields.financialSupport',
      kind: 'yesNo',
      value: p => details(p).financialSupport,
    },
  ];

  public readonly statusFields: EditableField[] = [
    { field: 'isNoShow', label: 'participants.fields.isNoShow', kind: 'yesNo', value: p => details(p).isNoShow },
    { field: 'isAnnulled', label: 'participants.fields.isAnnulled', kind: 'yesNo', value: p => details(p).isAnnulled },
    {
      field: 'isActivityLocked',
      label: 'participants.fields.isActivityLocked',
      kind: 'yesNo',
      value: p => details(p).isActivityLocked,
    },
  ];

  public readonly otherFields: EditableField[] = [
    { field: 'youthClub', label: 'participants.fields.youthClub', kind: 'text', value: p => details(p).youthClub },
    {
      field: 'photoConsent',
      label: 'participants.fields.photoConsent',
      kind: 'yesNo',
      value: p => details(p).photoConsent,
    },
  ];

  // Infosys "Admin-noter (internt)", "Beskeder til deltageren (fra Fastaval)", "Økonomi-note" and the notes from
  // the signup, in the order of Deltagere::$note_names
  public readonly noteFields: NoteField[] = [
    {
      field: 'adminNote',
      label: 'participants.notes.adminNote',
      kind: 'textarea',
      optional: false,
      value: p => details(p).adminNote,
    },
    {
      field: 'messageToParticipant',
      label: 'participants.notes.messageToParticipant',
      kind: 'textarea',
      optional: false,
      value: p => details(p).messageToParticipant,
    },
    {
      field: 'paymentNote',
      label: 'participants.notes.paymentNote',
      kind: 'textarea',
      optional: false,
      value: p => details(p).paymentNote,
    },
    {
      field: 'commentNote',
      label: 'participants.notes.commentNote',
      kind: 'textarea',
      optional: true,
      value: p => details(p).commentNote,
    },
    {
      field: 'heroForceNote',
      label: 'participants.notes.heroForceNote',
      kind: 'textarea',
      optional: true,
      value: p => details(p).heroForceNote,
    },
    {
      field: 'juniorContact',
      label: 'participants.notes.juniorContact',
      kind: 'textarea',
      optional: true,
      value: p => details(p).juniorContact,
    },
    {
      field: 'juniorComment',
      label: 'participants.notes.juniorComment',
      kind: 'textarea',
      optional: true,
      value: p => details(p).juniorComment,
    },
    {
      field: 'sleepingNote',
      label: 'participants.notes.sleepingNote',
      kind: 'textarea',
      optional: true,
      value: p => details(p).sleepingNote,
    },
  ];

  public readonly formatConventionDate = formatConventionDate;
  public readonly formatConventionDateTime = formatConventionDateTime;

  constructor() {
    // The same component shows the previous and next participant, so it reloads when the id changes. The flash
    // message belongs to the first participant shown.
    let first = true;
    this.route.paramMap.pipe(takeUntilDestroyed()).subscribe(params => {
      if (!first) this.flash.set(null);
      first = false;
      this.load(params.get('id') ?? '');
    });
  }

  public visibleNotes(p: Participant): NoteField[] {
    return this.noteFields.filter(note => !note.optional || note.value(p) !== null);
  }

  public selectedLabel(def: EditableField, p: Participant): string {
    if (def.text) return def.text(p);
    const option = def.options?.().find(o => o.value === def.value(p));
    return option?.label ?? '';
  }

  public startEdit(def: EditableField, p: Participant): void {
    if (!p.canEdit || this.busy()) return;
    const value = def.value(p);
    this.editor.set({
      field: def.field,
      value: value ?? (def.kind === 'textarea' ? '' : value),
      pronouns: toPronounChoice(p.pronouns),
    });
  }

  public cancelEdit(): void {
    this.editor.set(null);
  }

  public changeFirstPronoun(first: string): void {
    const editor = this.editor();
    if (editor) this.editor.set({ ...editor, pronouns: changeFirstPronoun(editor.pronouns, first) });
  }

  public changeSecondPronoun(second: string): void {
    const editor = this.editor();
    if (editor) this.editor.set({ ...editor, pronouns: { ...editor.pronouns, second } });
  }

  public submitEdit(def: EditableField): void {
    const editor = this.editor();
    const participant = this.participant();
    if (!editor || !participant) return;

    this.busy.set(true);
    this.http
      .put<Participant>(`/api/participants/${participant.id}/fields/${def.field}`, {
        value: editorValue(def.kind, editor),
      })
      .subscribe({
        next: updated => {
          this.participant.set(updated);
          this.editor.set(null);
          this.busy.set(false);
          this.errorKey.set(null);
          this.blink(this.saved, def.field);
        },
        error: () => {
          this.busy.set(false);
          this.errorKey.set('fieldUpdateFailed');
          this.blink(this.failed, def.field);
        },
      });
  }

  // Infosys: the left and right arrow keys show the previous and next participant, unless the user is typing
  @HostListener('document:keyup', ['$event'])
  public onKeyUp(event: KeyboardEvent): void {
    if ((event.key !== 'ArrowLeft' && event.key !== 'ArrowRight') || event.altKey || event.ctrlKey) return;
    const target = event.target as HTMLElement | null;
    if (target?.closest('textarea, input, select, mat-select') || this.editor()) return;

    const participant = this.participant();
    const id = event.key === 'ArrowLeft' ? participant?.previousId : participant?.nextId;
    if (id) this.router.navigate(['/participants', id]);
  }

  private load(id: string): void {
    this.participant.set(null);
    this.notFound.set(false);
    this.editor.set(null);
    this.http.get<Participant>(`/api/participants/${id}`).subscribe({
      next: participant => {
        this.participant.set(participant);
        if (participant.canEdit && !this.options()) this.loadOptions();
      },
      error: () => this.notFound.set(true),
    });
  }

  private loadOptions(): void {
    this.http.get<ParticipantOptions>('/api/participants/options').subscribe({
      next: options => this.options.set(options),
      error: err => console.error('Failed to load participant options:', err),
    });
  }

  private blink(target: WritableSignal<string | null>, field: string): void {
    target.set(field);
    setTimeout(() => {
      if (target() === field) target.set(null);
    }, 2000);
  }
}

// Infosys deltagere.sprog values
const LANGUAGE_VALUES: Record<string, string> = { Danish: 'dansk', English: 'engelsk', Scandinavian: 'skandinavisk' };
