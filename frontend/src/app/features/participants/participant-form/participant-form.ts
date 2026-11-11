import { Component, OnInit, inject, signal } from '@angular/core';
import { NgTemplateOutlet } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import {
  PARTICIPANT_LANGUAGES,
  PREFERRED_LANGUAGES,
  Participant,
  ParticipantCategory,
  ParticipantFieldsDto,
  ParticipantLanguage,
  ParticipantOptions,
  PreferredLanguage,
  UpdateParticipantDto,
} from '@shared/types';
import { Card } from '@shared/components';
import { TranslatePipe } from '@shared/pipes';
import { ParticipantFlashService } from '../participant-flash.service';

// Number inputs can be left empty, so they are nullable until the form is submitted
export interface ParticipantForm {
  birthDate: string;
  categoryId: number;
  amountPaid: number | null;
  firstName: string;
  lastName: string;
  email: string;
  addressLine1: string;
  addressLine2: string;
  zipCode: string;
  city: string;
  phoneNumber: string;
  smsSignup: boolean;
  preferredLanguage: PreferredLanguage;
  activityLanguages: ParticipantLanguage[];
  superHero: boolean;
  isAuthorOrDesigner: boolean;
  setupHelpMonday: boolean;
  setupHelpTuesday: boolean;
  desiredActivities: number | null;
  desiredHeroTasks: number | null;
  isNoShow: boolean;
  isAnnulled: boolean;
  superGameMaster: boolean;
  youthClub: string;
}

// The Infosys create form has no preselected values, so the browser shows the first option of each select:
// nej, da and the category with the lowest id. Empty number fields become 0 (PHP intval).
export function newParticipantForm(categories: ParticipantCategory[]): ParticipantForm {
  return {
    birthDate: '',
    categoryId: categories[0]?.id ?? 1,
    amountPaid: null,
    firstName: '',
    lastName: '',
    email: '',
    addressLine1: '',
    addressLine2: '',
    zipCode: '',
    city: '',
    phoneNumber: '',
    smsSignup: false,
    preferredLanguage: 'Danish',
    activityLanguages: ['Danish'],
    superHero: false,
    isAuthorOrDesigner: false,
    setupHelpMonday: false,
    setupHelpTuesday: false,
    desiredActivities: null,
    desiredHeroTasks: null,
    isNoShow: false,
    isAnnulled: false,
    superGameMaster: false,
    youthClub: '',
  };
}

export function toForm(p: Participant): ParticipantForm {
  const d = p.details;
  return {
    birthDate: p.birthDate ?? '',
    categoryId: p.categoryId,
    amountPaid: p.amountPaid,
    firstName: p.firstName,
    lastName: p.lastName,
    email: p.email,
    addressLine1: p.addressLine1,
    addressLine2: p.addressLine2,
    zipCode: p.zipCode,
    city: p.city,
    phoneNumber: p.phoneNumber,
    smsSignup: p.smsSignup,
    preferredLanguage: p.preferredLanguage,
    activityLanguages: d?.activityLanguages ?? [],
    superHero: d?.superHero ?? false,
    isAuthorOrDesigner: d?.isAuthorOrDesigner ?? false,
    setupHelpMonday: d?.setupHelpMonday ?? false,
    setupHelpTuesday: d?.setupHelpTuesday ?? false,
    desiredActivities: d?.desiredActivities ?? 0,
    desiredHeroTasks: d?.desiredHeroTasks ?? 0,
    isNoShow: d?.isNoShow ?? false,
    isAnnulled: d?.isAnnulled ?? false,
    superGameMaster: d?.superGameMaster ?? false,
    youthClub: d?.youthClub ?? '',
  };
}

export function toFieldsDto(form: ParticipantForm): ParticipantFieldsDto {
  return {
    birthDate: form.birthDate === '' ? null : form.birthDate,
    categoryId: form.categoryId,
    amountPaid: form.amountPaid ?? 0,
    firstName: form.firstName,
    lastName: form.lastName,
    email: form.email,
    addressLine1: form.addressLine1,
    addressLine2: form.addressLine2,
    zipCode: form.zipCode,
    city: form.city,
    phoneNumber: form.phoneNumber,
    smsSignup: form.smsSignup,
    preferredLanguage: form.preferredLanguage,
    superHero: form.superHero,
    isAuthorOrDesigner: form.isAuthorOrDesigner,
    setupHelpMonday: form.setupHelpMonday,
    setupHelpTuesday: form.setupHelpTuesday,
    desiredActivities: form.desiredActivities ?? 0,
    desiredHeroTasks: form.desiredHeroTasks ?? 0,
    isNoShow: form.isNoShow,
    superGameMaster: form.superGameMaster,
    youthClub: form.youthClub,
  };
}

export function toUpdateDto(form: ParticipantForm): UpdateParticipantDto {
  return { ...toFieldsDto(form), activityLanguages: form.activityLanguages, isAnnulled: form.isAnnulled };
}

// Infosys deltager/opret (create) and deltager/retdeltager/:id: (edit). Land, Arrangørområde, Scenarie and, on
// create, the activity languages are left out: Infosys ignores them in these forms (reported 2026-11-11). They
// can be changed on the participant page.
@Component({
  selector: 'app-participant-form',
  imports: [
    FormsModule,
    NgTemplateOutlet,
    MatButtonModule,
    MatCheckboxModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    Card,
    TranslatePipe,
  ],
  templateUrl: './participant-form.html',
  styleUrl: './participant-form.scss',
})
export class ParticipantFormComponent implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly flash = inject(ParticipantFlashService);

  public readonly preferredLanguages = PREFERRED_LANGUAGES;
  public readonly participantLanguages = PARTICIPANT_LANGUAGES;
  // Infosys number inputs for the desired activities and hero tasks
  public readonly desiredMin = 0;
  public readonly desiredMax = 10;

  public readonly participantId = signal<number | null>(null);
  public readonly participantName = signal('');
  // Whether the participant is annulled as stored, which decides between "Slet deltager" and "Annuller tilmelding"
  public readonly storedAnnulled = signal(false);
  public readonly categories = signal<ParticipantCategory[]>([]);
  public readonly form = signal<ParticipantForm | null>(null);
  public readonly notFound = signal(false);
  public readonly busy = signal(false);
  public readonly errorKey = signal<string | null>(null);
  public readonly confirmingDelete = signal(false);

  public ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    this.http.get<ParticipantOptions>('/api/participants/options').subscribe({
      next: options => {
        this.categories.set(options.categories);
        if (!id) this.form.set(newParticipantForm(options.categories));
      },
      error: err => console.error('Failed to load participant options:', err),
    });
    if (!id) return;

    this.participantId.set(Number(id));
    this.http.get<Participant>(`/api/participants/${id}`).subscribe({
      next: participant => {
        // Infosys Deltagere::getName: the nickname, or else the legal name
        this.participantName.set(participant.nickname || `${participant.firstName} ${participant.lastName}`.trim());
        this.storedAnnulled.set(participant.details?.isAnnulled ?? false);
        this.form.set(toForm(participant));
      },
      error: () => this.notFound.set(true),
    });
  }

  public hasLanguage(form: ParticipantForm, language: ParticipantLanguage): boolean {
    return form.activityLanguages.includes(language);
  }

  public toggleLanguage(form: ParticipantForm, language: ParticipantLanguage, checked: boolean): void {
    const others = form.activityLanguages.filter(l => l !== language);
    form.activityLanguages = checked ? [...others, language] : others;
  }

  public submit(): void {
    const form = this.form();
    if (!form) return;

    const id = this.participantId();
    this.busy.set(true);
    this.errorKey.set(null);

    if (id === null) {
      this.http.post<Participant>('/api/participants', toFieldsDto(form)).subscribe({
        next: created => {
          this.flash.set('success', 'created');
          this.router.navigate(['/participants', created.id]);
        },
        error: () => {
          // Infosys goes to the participants overview with the error
          this.flash.set('error', 'createFailed');
          this.router.navigate(['/participants']);
        },
      });
      return;
    }

    this.http.put<Participant>(`/api/participants/${id}`, toUpdateDto(form)).subscribe({
      next: () => {
        this.flash.set('success', 'updated');
        this.router.navigate(['/participants', id]);
      },
      error: () => {
        // Infosys shows the edit form again with the error
        this.busy.set(false);
        this.errorKey.set('updateFailed');
      },
    });
  }

  // Infosys "Fortryd" on the create form posts the form without "Opret deltager", which reports
  // "Kunne ikke oprette deltageren." on the overview (kept, as for activities)
  public cancel(): void {
    this.flash.set('error', 'createFailed');
    this.router.navigate(['/participants']);
  }

  // Infosys "Annuller tilmelding": only sets annulled
  public annul(): void {
    const id = this.participantId();
    if (id === null) return;

    this.busy.set(true);
    this.http.put<Participant>(`/api/participants/${id}/fields/isAnnulled`, { value: true }).subscribe({
      next: () => {
        this.flash.set('success', 'annulled');
        this.router.navigate(['/participants', id]);
      },
      error: () => {
        this.busy.set(false);
        this.errorKey.set('updateFailed');
      },
    });
  }

  // Infosys "Slet deltager" shows a confirmation first
  public askDelete(): void {
    this.confirmingDelete.set(true);
  }

  // Infosys "Slet for pokker": goes to the participants overview, or back to the participant with an error
  public confirmDelete(): void {
    const id = this.participantId();
    if (id === null) return;

    this.busy.set(true);
    this.http.delete(`/api/participants/${id}`).subscribe({
      next: () => {
        this.router.navigate(['/participants']);
      },
      error: () => {
        this.flash.set('error', 'deleteFailed');
        this.router.navigate(['/participants', id]);
      },
    });
  }

  // Infosys "Nej, live and let live" posts the confirmation without deleting; it goes back to the participant
  public cancelDelete(): void {
    this.router.navigate(['/participants', this.participantId()]);
  }
}
