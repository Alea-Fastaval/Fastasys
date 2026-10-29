import { Component, OnInit, signal, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatRadioModule } from '@angular/material/radio';
import { MatSelectModule } from '@angular/material/select';
import {
  ACTIVITY_LANGUAGES,
  ACTIVITY_TYPES,
  AUTO_SIGNUP_CATEGORIES,
  KARMA_TYPES,
  Activity,
  ActivityLanguage,
  ActivityType,
  AutoSignupCategory,
  KarmaType,
  SaveActivityDto,
} from '@shared/types';
import { Card } from '@shared/components';
import { TranslatePipe } from '@shared/pipes';
import { ActivityFlashService } from '../activity-flash.service';

// Number inputs can be left empty, so they are nullable until the form is submitted
export interface ActivityForm {
  title: string;
  titleEnglish: string;
  author: string;
  type: ActivityType;
  description: string;
  descriptionEnglish: string;
  note: string;
  durationHours: number | null;
  minPlayersPerTeam: number | null;
  maxPlayersPerTeam: number | null;
  gameMastersPerTeam: number | null;
  price: number | null;
  maxSignups: number | null;
  minAge: number | null;
  maxAge: number | null;
  canSignUp: boolean;
  isHidden: boolean;
  isRoomExclusive: boolean;
  isTimeExclusive: boolean;
  isReplayable: boolean;
  playerLanguage: ActivityLanguage;
  gameMasterLanguage: ActivityLanguage;
  karmaType: KarmaType;
  autoSignupCategory: AutoSignupCategory;
  wordPressId: number | null;
}

// The Infosys create form has no preselected value in its ja/nej and type selects, so the browser shows
// the first option: 'ja' (also for Skjult and Kan spilles flere gange) and type 'braet'.
// Sprog radios default to dansk, karmatype to Ingen and max tilmeldinger to 0.
export function newActivityForm(): ActivityForm {
  return {
    title: '',
    titleEnglish: '',
    author: '',
    type: 'BoardGame',
    description: '',
    descriptionEnglish: '',
    note: '',
    durationHours: null,
    minPlayersPerTeam: null,
    maxPlayersPerTeam: null,
    gameMastersPerTeam: null,
    price: null,
    maxSignups: 0,
    minAge: null,
    maxAge: null,
    canSignUp: true,
    isHidden: true,
    isRoomExclusive: true,
    isTimeExclusive: true,
    isReplayable: true,
    playerLanguage: 'Danish',
    gameMasterLanguage: 'Danish',
    karmaType: 'None',
    autoSignupCategory: 'None',
    wordPressId: null,
  };
}

export function toForm(activity: Activity): ActivityForm {
  return {
    title: activity.title,
    titleEnglish: activity.titleEnglish,
    author: activity.author,
    type: activity.type,
    description: activity.description,
    descriptionEnglish: activity.descriptionEnglish,
    note: activity.note,
    durationHours: activity.durationHours,
    minPlayersPerTeam: activity.minPlayersPerTeam,
    maxPlayersPerTeam: activity.maxPlayersPerTeam,
    gameMastersPerTeam: activity.gameMastersPerTeam,
    price: activity.price,
    maxSignups: activity.maxSignups,
    minAge: activity.minAge,
    maxAge: activity.maxAge,
    canSignUp: activity.canSignUp,
    isHidden: activity.isHidden,
    isRoomExclusive: activity.isRoomExclusive,
    isTimeExclusive: activity.isTimeExclusive,
    isReplayable: activity.isReplayable,
    playerLanguage: activity.playerLanguage,
    gameMasterLanguage: activity.gameMasterLanguage,
    karmaType: activity.karmaType,
    autoSignupCategory: activity.autoSignupCategory,
    wordPressId: activity.wordPressId,
  };
}

// Empty number fields become 0 like PHP intval(''); empty ages mean no age requirement.
export function toDto(form: ActivityForm): SaveActivityDto {
  return {
    ...form,
    durationHours: form.durationHours ?? 0,
    minPlayersPerTeam: form.minPlayersPerTeam ?? 0,
    maxPlayersPerTeam: form.maxPlayersPerTeam ?? 0,
    gameMastersPerTeam: form.gameMastersPerTeam ?? 0,
    price: form.price ?? 0,
    maxSignups: form.maxSignups ?? 0,
    wordPressId: form.wordPressId ?? 0,
  };
}

@Component({
  selector: 'app-activity-form',
  imports: [
    FormsModule,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatRadioModule,
    MatSelectModule,
    Card,
    TranslatePipe,
  ],
  templateUrl: './activity-form.html',
  styleUrl: './activity-form.scss',
})
export class ActivityFormComponent implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly flash = inject(ActivityFlashService);

  public readonly types = ACTIVITY_TYPES;
  public readonly languages = ACTIVITY_LANGUAGES;
  public readonly karmaTypes = KARMA_TYPES;
  public readonly autoSignupCategories = AUTO_SIGNUP_CATEGORIES;
  public readonly maxAgeLimit = 200;
  public readonly maxSignupsLimit = 1000;

  public readonly activityId = signal<number | null>(null);
  public readonly activityTitle = signal('');
  public readonly form = signal<ActivityForm | null>(null);
  public readonly notFound = signal(false);
  public readonly saving = signal(false);
  public readonly errorKey = signal<string | null>(null);
  public readonly confirmingDelete = signal(false);

  public ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (!id) {
      this.form.set(newActivityForm());
      return;
    }

    this.activityId.set(Number(id));
    this.http.get<Activity>(`/api/activities/${id}`).subscribe({
      next: activity => {
        this.activityTitle.set(activity.title);
        this.form.set(toForm(activity));
      },
      error: () => this.notFound.set(true),
    });
  }

  public save(): void {
    const form = this.form();
    if (!form) return;

    const id = this.activityId();
    const dto = toDto(form);
    this.saving.set(true);
    this.errorKey.set(null);

    if (id === null) {
      this.http.post<Activity>('/api/activities', dto).subscribe({
        next: created => {
          this.flash.set('success', 'created');
          this.router.navigate(['/activities', created.id]);
        },
        error: () => this.fail('createFailed'),
      });
    } else {
      this.http.put<Activity>(`/api/activities/${id}`, dto).subscribe({
        next: () => {
          this.flash.set('success', 'updated');
          this.router.navigate(['/activities', id]);
        },
        error: () => this.fail('updateFailed'),
      });
    }
  }

  // Infosys "Fortryd" on both the create and the edit form reports "Aktiviteten blev ikke opdateret."
  public cancel(): void {
    const id = this.activityId();
    this.flash.set('error', 'notUpdated');
    this.router.navigate(id === null ? ['/activities'] : ['/activities', id]);
  }

  public askDelete(): void {
    this.confirmingDelete.set(true);
  }

  public confirmDelete(): void {
    const id = this.activityId();
    if (id === null) return;

    this.saving.set(true);
    this.http.delete(`/api/activities/${id}`).subscribe({
      next: () => {
        this.flash.set('success', 'deleted');
        this.router.navigate(['/activities']);
      },
      error: () => {
        this.flash.set('error', 'deleteFailed');
        this.router.navigate(['/activities']);
      },
    });
  }

  // Infosys "Fortryd" on the delete confirmation goes to the list of all activities, without a message
  public cancelDelete(): void {
    this.router.navigate(['/activities']);
  }

  private fail(key: string): void {
    this.saving.set(false);
    this.errorKey.set(key);
  }
}
