import { Component, OnInit, inject, signal } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { CreateFoodTypeDto, FoodTypeDetail, FoodTypeFieldsDto, SignupStatus, UpdateFoodTypeDto } from '@shared/types';
import { Card } from '@shared/components';
import { TranslatePipe } from '@shared/pipes';
import { TranslationService } from '@shared/services';
import { dayWithDate, ticketDayOptions, translatedLongDay } from '../../tickets/ticket-texts';
import { FoodFlashService } from '../food-flash.service';
import { MealTimeRow, newMealTimeRow, toMealTimeDto, toMealTimeRow } from '../food-texts';

// mad.kategori and title_en are varchar(100), madtider.description_da/_en varchar(128)
export const MAX_FOOD_NAME_LENGTH = 100;
export const MAX_MEAL_DESCRIPTION_LENGTH = 128;

export interface FoodTypeForm {
  nameDanish: string;
  nameEnglish: string;
  price: number | null;
  isHidden: boolean;
  mealTimes: MealTimeRow[];
}

// Infosys's create form starts empty, without meal times
export function newFoodTypeForm(): FoodTypeForm {
  return { nameDanish: '', nameEnglish: '', price: null, isHidden: false, mealTimes: [] };
}

export function toForm(food: FoodTypeDetail): FoodTypeForm {
  return {
    nameDanish: food.nameDanish,
    nameEnglish: food.nameEnglish,
    price: food.price,
    isHidden: food.isHidden,
    mealTimes: food.mealTimes.map(toMealTimeRow),
  };
}

export function toDto(form: FoodTypeForm): FoodTypeFieldsDto {
  return {
    nameDanish: form.nameDanish,
    nameEnglish: form.nameEnglish,
    price: form.price,
    isHidden: form.isHidden,
    mealTimes: form.mealTimes.map(toMealTimeDto),
  };
}

// The rules the API checks (FoodTypesController.Validate): a kategori that isn't empty or "0" (PHP empty()), names of
// at most 100 characters, a whole price of 0 or more (Infosys took any number; fixed 2026-11-25), and every meal time
// with a day and a time, at most once per time
export function canSubmitFoodType(form: FoodTypeForm): boolean {
  const times = form.mealTimes.map(row => `${row.day}T${row.clock}`);
  return (
    form.nameDanish !== '' &&
    form.nameDanish !== '0' &&
    form.nameDanish.length <= MAX_FOOD_NAME_LENGTH &&
    form.nameEnglish.length <= MAX_FOOD_NAME_LENGTH &&
    form.price !== null &&
    Number.isInteger(form.price) &&
    form.price >= 0 &&
    form.mealTimes.every(row => row.day !== '' && row.clock !== '') &&
    new Set(times).size === times.length
  );
}

// Infosys mad/create and mad/edit/:id: ("Opret mad-kategori" / "Opdater mad-kategori") with the meal times ("Mad
// tider"), and the delete confirmation (displaydelete.phtml, "Slet mad-type?"). Infosys typed a meal time as text
// ("YYYY-MM-DD HH:MM:SS"); here it is "Dag", a dropdown of the convention's days, and "Tid" (agreed 2026-11-25).
@Component({
  selector: 'app-food-type-form',
  imports: [
    FormsModule,
    RouterLink,
    MatButtonModule,
    MatCheckboxModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatSelectModule,
    Card,
    TranslatePipe,
  ],
  templateUrl: './food-type-form.html',
  styleUrl: './food-type-form.scss',
})
export class FoodTypeFormComponent implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly flash = inject(FoodFlashService);
  private readonly longDay = translatedLongDay(inject(TranslationService));

  public readonly maxNameLength = MAX_FOOD_NAME_LENGTH;
  public readonly maxDescriptionLength = MAX_MEAL_DESCRIPTION_LENGTH;
  public readonly foodId = signal<number | null>(null);
  public readonly form = signal<FoodTypeForm | null>(null);
  public readonly notFound = signal(false);
  public readonly busy = signal(false);
  public readonly errorKey = signal<string | null>(null);
  public readonly confirmingDelete = signal(false);
  private readonly conventionStart = signal<string | null>(null);
  private readonly conventionEnd = signal<string | null>(null);
  // Whether the convention's dates have been asked for (they may not be set, or the request may fail)
  public readonly datesLoaded = signal(false);

  public ngOnInit(): void {
    this.http.get<SignupStatus>('/api/signup/status').subscribe({
      next: status => {
        this.conventionStart.set(status.conventionStart);
        this.conventionEnd.set(status.conventionEnd);
        this.datesLoaded.set(true);
      },
      error: () => this.datesLoaded.set(true),
    });

    const id = this.route.snapshot.paramMap.get('id');
    if (!id) {
      this.form.set(newFoodTypeForm());
      return;
    }

    this.foodId.set(Number(id));
    this.http.get<FoodTypeDetail>(`/api/food-types/${id}`).subscribe({
      next: food => this.form.set(toForm(food)),
      error: () => this.notFound.set(true),
    });
  }

  // The convention's days, and the meal time's own day when it is outside them
  public days(row: MealTimeRow): string[] {
    return ticketDayOptions(this.conventionStart(), this.conventionEnd(), row.originalDay);
  }

  public hasNoDays(): boolean {
    return this.datesLoaded() && ticketDayOptions(this.conventionStart(), this.conventionEnd(), null).length === 0;
  }

  // "Onsdag 24/03"
  public dayText(day: string): string {
    return dayWithDate(day, this.longDay);
  }

  // Infosys's "Tilføj" under the meal times
  public addMealTime(): void {
    this.form.update(form => form && { ...form, mealTimes: [...form.mealTimes, newMealTimeRow()] });
  }

  // A meal time that participants have ordered can't be removed
  public removeMealTime(row: MealTimeRow): void {
    if (row.orderCount > 0) return;
    this.form.update(form => form && { ...form, mealTimes: form.mealTimes.filter(r => r !== row) });
  }

  public canSubmit(form: FoodTypeForm): boolean {
    return !this.busy() && canSubmitFoodType(form);
  }

  public submit(): void {
    const form = this.form();
    if (!form || !this.canSubmit(form)) return;

    const id = this.foodId();
    this.busy.set(true);
    this.errorKey.set(null);

    if (id === null) {
      this.create(toDto(form));
    } else {
      this.update(id, toDto(form));
    }
  }

  // Infosys "Fortryd" on the edit form reports "Mad-typen blev ikke opdateret." on the type's page
  public cancel(): void {
    this.flash.set('error', 'notUpdated');
    void this.router.navigate(['/resources/food', this.foodId()]);
  }

  public askDelete(): void {
    this.confirmingDelete.set(true);
  }

  // Infosys goes to its Mad page after a delete, and back to the type when it fails
  public confirmDelete(): void {
    const id = this.foodId();
    if (id === null) return;

    this.busy.set(true);
    this.http.delete(`/api/food-types/${id}`).subscribe({
      next: () => {
        this.flash.set('success', 'deleted');
        void this.router.navigate(['/resources/food']);
      },
      error: () => {
        this.flash.set('error', 'deleteFailed');
        void this.router.navigate(['/resources/food', id]);
      },
    });
  }

  // Infosys "Fortryd" on the delete confirmation goes back to the type, without a message
  public cancelDelete(): void {
    void this.router.navigate(['/resources/food', this.foodId()]);
  }

  private create(dto: CreateFoodTypeDto): void {
    this.http.post<FoodTypeDetail>('/api/food-types', dto).subscribe({
      next: created => {
        this.flash.set('success', 'created');
        void this.router.navigate(['/resources/food', created.id]);
      },
      error: () => this.fail('createFailed'),
    });
  }

  private update(id: number, dto: UpdateFoodTypeDto): void {
    this.http.put<FoodTypeDetail>(`/api/food-types/${id}`, dto).subscribe({
      next: () => {
        this.flash.set('success', 'updated');
        void this.router.navigate(['/resources/food', id]);
      },
      error: (err: HttpErrorResponse) =>
        this.fail(err.error?.reason === 'mealTimeOrdered' ? 'mealTimeOrdered' : 'updateFailed'),
    });
  }

  // The form keeps what was typed, like the activity form (Infosys went to another page)
  private fail(key: string): void {
    this.busy.set(false);
    this.errorKey.set(key);
  }
}
