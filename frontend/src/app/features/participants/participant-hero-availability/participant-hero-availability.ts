import { Component, OnInit, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatSelectModule } from '@angular/material/select';
import {
  HERO_PERIOD_HOURS,
  HERO_PERIODS,
  HeroAvailability,
  HeroPeriod,
  HeroTaskCategory,
  ParticipantHeroAvailability,
  SignupStatus,
  UpdateParticipantHeroAvailabilityDto,
} from '@shared/types';
import { Card } from '@shared/components';
import { TranslatePipe } from '@shared/pipes';
import { TranslationService } from '@shared/services';
import { ParticipantFlashService } from '../participant-flash.service';
import { dayWithDate, ticketDayOptions, translatedLongDay } from '../../tickets/ticket-texts';
import { heroSignupPeriodText } from '../participant-hero-signups/participant-hero-signups';

// The category select's choice for any category ("Alle")
export const ANY_CATEGORY = 'any';
export type CategoryChoice = number | typeof ANY_CATEGORY | null;

// The days the "Dag" select offers: the convention's days, and the days the participant has periods on outside them
// (so saving keeps them)
export function heroDayOptions(start: string | null, end: string | null, rows: HeroAvailability[]): string[] {
  const days = new Set(ticketDayOptions(start, end, null));
  rows.forEach(row => days.add(row.date));
  return [...days].sort();
}

function samePeriod(a: HeroAvailability, b: HeroAvailability): boolean {
  return a.date === b.date && a.period === b.period && a.categoryId === b.categoryId;
}

// imwedit.js setupGDSShifts "Tilføj vagt": adds the period unless it is already listed. Infosys's check looked for
// the wrong field, so the same period could be added twice and the save then failed (fixed 2026-11-26).
export function addHeroPeriod(rows: HeroAvailability[], row: HeroAvailability): HeroAvailability[] {
  return rows.some(r => samePeriod(r, row)) ? rows : [...rows, row];
}

// Infosys deltager/updategdstilmeldinger/:id: ("Opdater Heltestyrke Tilmeldinger", the "Ret/slet" link of the
// Heltestyrken, Tilmeldinger box): pick a category, a day and a part of the day and "Tilføj vagt"; "Slet" removes a
// row and "Opdater" saves the list. Infosys offered only the periods of the category's hero force shifts (and greyed
// out the ones the participant was busy in); the hero force isn't ported, so any convention day and part of a day can
// be chosen, like in the signup, and "Alle" is the signup's any category (agreed 2026-11-26).
@Component({
  selector: 'app-participant-hero-availability',
  imports: [
    FormsModule,
    RouterLink,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatSelectModule,
    Card,
    TranslatePipe,
  ],
  templateUrl: './participant-hero-availability.html',
  styleUrl: './participant-hero-availability.scss',
})
export class ParticipantHeroAvailabilityComponent implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly flash = inject(ParticipantFlashService);
  private readonly translation = inject(TranslationService);
  private readonly longDay = translatedLongDay(this.translation);

  public readonly participantId = Number(this.route.snapshot.paramMap.get('id'));
  public readonly anyCategory = ANY_CATEGORY;
  public readonly periods = HERO_PERIODS;
  public readonly hours = HERO_PERIOD_HOURS;
  public readonly page = signal<ParticipantHeroAvailability | null>(null);
  public readonly rows = signal<HeroAvailability[]>([]);
  public readonly notFound = signal(false);
  public readonly busy = signal(false);
  private readonly conventionStart = signal<string | null>(null);
  private readonly conventionEnd = signal<string | null>(null);

  // The selects start on "Vælg", like Infosys's
  public chosenCategory: CategoryChoice = null;
  public chosenDay: string | null = null;
  public chosenPeriod: HeroPeriod | null = null;

  public ngOnInit(): void {
    this.http.get<SignupStatus>('/api/signup/status').subscribe({
      next: status => {
        this.conventionStart.set(status.conventionStart);
        this.conventionEnd.set(status.conventionEnd);
      },
      error: () => undefined,
    });
    this.http.get<ParticipantHeroAvailability>(`/api/participants/${this.participantId}/hero-availability`).subscribe({
      next: page => {
        this.page.set(page);
        this.rows.set(page.availability);
      },
      error: () => this.notFound.set(true),
    });
  }

  public days(): string[] {
    return heroDayOptions(this.conventionStart(), this.conventionEnd(), this.page()?.availability ?? []);
  }

  // "Onsdag 24/03"
  public dayText(day: string): string {
    return dayWithDate(day, this.longDay);
  }

  // "Forplejning Onsdag 7-12", or "Alle Onsdag 7-12" for any category
  public rowText(row: HeroAvailability): string {
    const category = row.categoryName ?? this.translation.translate('participants.heroSignups.anyCategory');
    const period = heroSignupPeriodText({ date: row.date, hours: HERO_PERIOD_HOURS[row.period] }, weekday =>
      this.longDay(weekday),
    );
    return `${category} ${period}`;
  }

  public canAdd(): boolean {
    return this.chosenCategory !== null && this.chosenDay !== null && this.chosenPeriod !== null;
  }

  // "Tilføj vagt"
  public add(): void {
    const categories: HeroTaskCategory[] = this.page()?.categories ?? [];
    if (!this.canAdd() || this.chosenDay === null || this.chosenPeriod === null) return;

    const category = this.chosenCategory === ANY_CATEGORY ? null : categories.find(c => c.id === this.chosenCategory);
    if (category === undefined) return;

    const row: HeroAvailability = {
      categoryId: category?.id ?? null,
      categoryName: category?.nameDanish ?? null,
      date: this.chosenDay,
      period: this.chosenPeriod,
    };
    this.rows.update(rows => addHeroPeriod(rows, row));
  }

  public remove(row: HeroAvailability): void {
    this.rows.update(rows => rows.filter(r => r !== row));
  }

  // "Opdater": Infosys goes back to the participant with "Deltageren blev opdateret." or "Kunne ikke opdatere
  // deltageren."
  public submit(): void {
    if (this.busy()) return;
    this.busy.set(true);
    const dto: UpdateParticipantHeroAvailabilityDto = {
      availability: this.rows().map(r => ({ categoryId: r.categoryId, date: r.date, period: r.period })),
    };
    this.http.put(`/api/participants/${this.participantId}/hero-availability`, dto).subscribe({
      next: () => this.backToParticipant('success', 'updated'),
      error: () => this.backToParticipant('error', 'heroAvailabilityUpdateFailed'),
    });
  }

  // "Fortryd": Infosys goes back to the participant with "Deltagerens GDS vagt tilmeldinger blev ikke opdateret."
  public cancel(): void {
    this.backToParticipant('error', 'heroAvailabilityNotUpdated');
  }

  private backToParticipant(type: 'success' | 'error', key: string): void {
    this.flash.set(type, key);
    void this.router.navigate(['/participants', this.participantId]);
  }
}
