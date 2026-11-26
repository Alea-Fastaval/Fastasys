import { Component, OnInit, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { Observable, forkJoin } from 'rxjs';
import {
  FoodTypeOption,
  MealTimeOption,
  ParticipantMealRow,
  ParticipantMeals,
  ParticipantTickets,
  ParticipantWear,
  ParticipantWearOrderFieldsDto,
  TicketTypeOption,
  UpdateParticipantMealsDto,
  UpdateParticipantTicketsDto,
  UpdateParticipantWearDto,
  WearAttribute,
  WearPriceOption,
  WearTypeOption,
} from '@shared/types';
import { Card } from '@shared/components';
import { TranslatePipe } from '@shared/pipes';
import { AuthService, TranslationService } from '@shared/services';
import { ParticipantFlashService } from '../participant-flash.service';
import { ticketOptionText, translatedShortDay } from '../../tickets/ticket-texts';
import { mealDayText } from '../../food/food-texts';
import {
  AttributeChoice,
  WearOrderRow,
  addOrder,
  allowedOptions,
  attributeChoices,
  fixChoices,
  kindKey,
  orderName,
  orderRowText,
  priceOptionText,
  sizeName,
} from '../../wear/wear-texts';

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

// The participant's wear order as an editable row
export function toWearRow(order: ParticipantWear['orders'][number]): WearOrderRow {
  return {
    id: order.id,
    wearPriceId: order.wearPriceId,
    attributeIds: order.attributeIds,
    name: order.name,
    size: order.size,
    categoryName: order.categoryName,
    unitPrice: order.unitPrice,
    amount: order.amount,
    isReceived: order.isReceived,
  };
}

// A kept order is sent with its id and amount, a new one with its price and attributes
export function toWearDto(row: WearOrderRow): ParticipantWearOrderFieldsDto {
  return row.id === null
    ? { id: null, wearPriceId: row.wearPriceId, attributeIds: row.attributeIds, amount: row.amount }
    : { id: row.id, wearPriceId: null, attributeIds: null, amount: row.amount };
}

// imwedit.js setupWear: the row "Tilføj" makes from the chosen wear, price, attributes and amount
export function newWearRow(
  type: WearTypeOption,
  price: WearPriceOption,
  attributes: WearAttribute[],
  amount: number,
): WearOrderRow {
  return {
    id: null,
    wearPriceId: price.id,
    attributeIds: attributes.map(a => a.id),
    name: orderName(type.nameDanish, attributes),
    size: sizeName(attributes),
    categoryName: price.categoryName,
    unitPrice: price.price,
    amount,
    isReceived: false,
  };
}

// Infosys's "Rediger indgang, mad og wear" (deltager/updatemadwear/:id:, the "Ret/slet" link of the Indgang/Mad and
// Wear boxes). Indgang: pick a ticket type and "Tilføj" it. Mad: pick a food type, then the day of one of its meal
// times, and "Tilføj" it. Wear: pick a wear type, one of its prices (any category, as in Infosys), an attribute of each
// kind and Antal, and "Tilføj" it; the same wear, price and attributes add to the row already listed. "Slet" removes a
// row, and "Opdater" saves the lists. Each part is shown to the users who may change it (entrance_participant_edit,
// food_participant_edit, wear_participant_edit).
@Component({
  selector: 'app-participant-entrance-food-wear',
  imports: [
    FormsModule,
    RouterLink,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
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
  public readonly mayEditWear = this.auth.hasPrivilege('wear_participant_edit');
  public readonly ticketsPage = signal<ParticipantTickets | null>(null);
  public readonly tickets = signal<TicketTypeOption[]>([]);
  public readonly mealsPage = signal<ParticipantMeals | null>(null);
  public readonly meals = signal<ParticipantMealRow[]>([]);
  public readonly wearPage = signal<ParticipantWear | null>(null);
  public readonly wearRows = signal<WearOrderRow[]>([]);
  public readonly notFound = signal(false);
  public readonly busy = signal(false);
  public readonly kindKey = kindKey;
  public readonly priceOptionText = priceOptionText;

  // The ticket select starts on the first type, like Infosys's; the food and wear selects start on "Vælg"
  public chosenTicketId: number | null = null;
  public chosenFoodId: number | null = null;
  public chosenMealTimeId: number | null = null;
  public chosenWearTypeId: number | null = null;
  public chosenPriceId: number | null = null;
  // The choice of each attribute select (see wearChoices)
  public chosenAttributeIds: (number | null)[] = [];
  // Infosys's Antal starts at 0
  public wearAmount: number | null = 0;

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
    if (this.mayEditWear) {
      this.http.get<ParticipantWear>(`/api/participants/${this.participantId}/wear`).subscribe({
        next: page => {
          this.wearPage.set(page);
          this.wearRows.set(page.orders.map(toWearRow));
        },
        error: () => this.notFound.set(true),
      });
    }
  }

  // The page is shown when every part the user may change has loaded
  public isLoaded(): boolean {
    return (
      (this.mayEditTickets || this.mayEditMeals || this.mayEditWear) &&
      (!this.mayEditTickets || this.ticketsPage() !== null) &&
      (!this.mayEditMeals || this.mealsPage() !== null) &&
      (!this.mayEditWear || this.wearPage() !== null)
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

  public chosenWearType(): WearTypeOption | undefined {
    return this.wearPage()?.wearTypes.find(w => w.id === this.chosenWearTypeId);
  }

  // The attribute selects of the chosen wear type
  public wearChoices(): AttributeChoice[] {
    const type = this.chosenWearType();
    return type ? attributeChoices(type.variants) : [];
  }

  // The options a select shows; the others don't fit the choices before it (imwedit.js hid them)
  public attributeOptions(index: number): AttributeChoice['options'] {
    return allowedOptions(this.wearChoices(), this.chosenAttributeIds, index);
  }

  // Choosing another wear type fills the price select and the attribute selects anew, each on its first option, like
  // the browser did with Infosys's
  public wearTypeChanged(): void {
    this.chosenPriceId = this.chosenWearType()?.prices[0]?.id ?? null;
    this.chosenAttributeIds = fixChoices(this.wearChoices(), []);
  }

  public attributeChanged(): void {
    this.chosenAttributeIds = fixChoices(this.wearChoices(), this.chosenAttributeIds);
  }

  public wearRowText(row: WearOrderRow): string {
    return orderRowText(row, {
      pieces: this.translation.translate('participants.wear.pieces'),
      size: this.translation.translate('participants.wear.size'),
    });
  }

  // imwedit.js "Tilføj": nothing happens without a price
  public addWear(): void {
    const type = this.chosenWearType();
    const price = type?.prices.find(p => p.id === this.chosenPriceId);
    if (!type || !price) return;

    const attributes = this.wearChoices()
      .map((choice, index) => choice.options.find(o => o.attribute.id === this.chosenAttributeIds[index])?.attribute)
      .filter(attribute => attribute !== undefined);
    const row = newWearRow(type, price, attributes, Number(this.wearAmount));
    this.wearRows.update(rows => addOrder(rows, row));
  }

  public removeWear(row: WearOrderRow): void {
    this.wearRows.update(rows => rows.filter(r => r !== row));
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
    if (this.wearPage()) {
      const dto: UpdateParticipantWearDto = { orders: this.wearRows().map(toWearDto) };
      requests.push(this.http.put(`/api/participants/${this.participantId}/wear`, dto));
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
