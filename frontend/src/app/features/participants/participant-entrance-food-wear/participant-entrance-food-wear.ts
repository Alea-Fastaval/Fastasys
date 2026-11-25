import { Component, OnInit, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatSelectModule } from '@angular/material/select';
import { Observable, forkJoin } from 'rxjs';
import {
  FoodTypeOption,
  MealTimeOption,
  ParticipantMealRow,
  ParticipantMeals,
  ParticipantTickets,
  TicketTypeOption,
  UpdateParticipantMealsDto,
  UpdateParticipantTicketsDto,
} from '@shared/types';
import { Card } from '@shared/components';
import { TranslatePipe } from '@shared/pipes';
import { AuthService, TranslationService } from '@shared/services';
import { ParticipantFlashService } from '../participant-flash.service';
import { ticketOptionText, translatedShortDay } from '../../tickets/ticket-texts';
import { mealDayText } from '../../food/food-texts';

// imwedit.js setupIndgang: "Tilføj" adds the chosen type unless it is already listed
export function addTicket(tickets: TicketTypeOption[], chosen: TicketTypeOption | undefined): TicketTypeOption[] {
  if (!chosen || tickets.some(t => t.id === chosen.id)) return tickets;
  return [...tickets, chosen];
}

// imwedit.js setupMad: "Tilføj" adds the chosen meal time unless it is already listed
export function addMeal(
  meals: ParticipantMealRow[],
  food: FoodTypeOption | undefined,
  mealTime: MealTimeOption | undefined,
): ParticipantMealRow[] {
  if (!food || !mealTime || meals.some(m => m.mealTimeId === mealTime.id)) return meals;
  return [...meals, { mealTimeId: mealTime.id, foodName: food.nameDanish, time: mealTime.time, isReceived: false }];
}

// Infosys's "Rediger indgang, mad og wear" (deltager/updatemadwear/:id:, the "Ret/slet" link of the Indgang/Mad box).
// Indgang: pick a ticket type and "Tilføj" it. Mad: pick a food type, then the day of one of its meal times, and
// "Tilføj" it. "Slet" removes a row, and "Opdater" saves both lists. Each part is shown to the users who may change it
// (entrance_participant_edit, food_participant_edit). Wear comes with its own port.
@Component({
  selector: 'app-participant-entrance-food-wear',
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
  templateUrl: './participant-entrance-food-wear.html',
  styleUrl: './participant-entrance-food-wear.scss',
})
export class ParticipantEntranceFoodWearComponent implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly flash = inject(ParticipantFlashService);
  private readonly auth = inject(AuthService);
  private readonly translation = inject(TranslationService);
  private readonly shortDay = translatedShortDay(this.translation);

  public readonly participantId = Number(this.route.snapshot.paramMap.get('id'));
  public readonly mayEditTickets = this.auth.hasPrivilege('entrance_participant_edit');
  public readonly mayEditMeals = this.auth.hasPrivilege('food_participant_edit');
  public readonly ticketsPage = signal<ParticipantTickets | null>(null);
  public readonly tickets = signal<TicketTypeOption[]>([]);
  public readonly mealsPage = signal<ParticipantMeals | null>(null);
  public readonly meals = signal<ParticipantMealRow[]>([]);
  public readonly notFound = signal(false);
  public readonly busy = signal(false);

  // The ticket select starts on the first type, like Infosys's; the food selects start on "Vælg"
  public chosenTicketId: number | null = null;
  public chosenFoodId: number | null = null;
  public chosenMealTimeId: number | null = null;

  public ngOnInit(): void {
    if (this.mayEditTickets) {
      this.http.get<ParticipantTickets>(`/api/participants/${this.participantId}/tickets`).subscribe({
        next: page => {
          this.ticketsPage.set(page);
          this.tickets.set(page.tickets);
          this.chosenTicketId = page.ticketTypes[0]?.id ?? null;
        },
        error: () => this.notFound.set(true),
      });
    }
    if (this.mayEditMeals) {
      this.http.get<ParticipantMeals>(`/api/participants/${this.participantId}/meals`).subscribe({
        next: page => {
          this.mealsPage.set(page);
          this.meals.set(page.meals);
        },
        error: () => this.notFound.set(true),
      });
    }
  }

  // The page is shown when every part the user may change has loaded
  public isLoaded(): boolean {
    return (
      (this.mayEditTickets || this.mayEditMeals) &&
      (!this.mayEditTickets || this.ticketsPage() !== null) &&
      (!this.mayEditMeals || this.mealsPage() !== null)
    );
  }

  public ticketText(option: TicketTypeOption): string {
    return ticketOptionText(option, this.shortDay, this.translation.translate('tickets.partout'));
  }

  public addTicket(): void {
    const chosen = this.ticketsPage()?.ticketTypes.find(t => t.id === this.chosenTicketId);
    this.tickets.update(tickets => addTicket(tickets, chosen));
  }

  public removeTicket(ticket: TicketTypeOption): void {
    this.tickets.update(tickets => tickets.filter(t => t !== ticket));
  }

  public chosenFood(): FoodTypeOption | undefined {
    return this.mealsPage()?.foodTypes.find(f => f.id === this.chosenFoodId);
  }

  // Choosing another food type empties the day select, like Infosys's
  public foodChanged(): void {
    this.chosenMealTimeId = null;
  }

  // "Ons" (Infosys mad/ajaxgetmadtider)
  public dayText(time: string): string {
    return mealDayText(time, this.shortDay);
  }

  public addMeal(): void {
    const food = this.chosenFood();
    const mealTime = food?.mealTimes.find(m => m.id === this.chosenMealTimeId);
    this.meals.update(meals => addMeal(meals, food, mealTime));
  }

  public removeMeal(meal: ParticipantMealRow): void {
    this.meals.update(meals => meals.filter(m => m !== meal));
  }

  // Infosys goes back to the participant with "Deltageren blev opdateret." or "Kunne ikke opdatere deltageren."
  public submit(): void {
    const requests: Observable<unknown>[] = [];
    if (this.ticketsPage()) {
      const dto: UpdateParticipantTicketsDto = { ticketTypeIds: this.tickets().map(t => t.id) };
      requests.push(this.http.put(`/api/participants/${this.participantId}/tickets`, dto));
    }
    if (this.mealsPage()) {
      const dto: UpdateParticipantMealsDto = { mealTimeIds: this.meals().map(m => m.mealTimeId) };
      requests.push(this.http.put(`/api/participants/${this.participantId}/meals`, dto));
    }
    if (requests.length === 0) return;

    this.busy.set(true);
    forkJoin(requests).subscribe({
      next: () => this.backToParticipant('success', 'updated'),
      error: () => this.backToParticipant('error', 'entranceFoodWearUpdateFailed'),
    });
  }

  private backToParticipant(type: 'success' | 'error', key: string): void {
    this.flash.set(type, key);
    void this.router.navigate(['/participants', this.participantId]);
  }
}
