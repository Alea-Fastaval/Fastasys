import { Component, OnInit, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { FoodTypeDetail } from '@shared/types';
import { Card } from '@shared/components';
import { TranslatePipe } from '@shared/pipes';
import { AuthService, TranslationService } from '@shared/services';
import { priceText } from '../../participants/participant-price/participant-price';
import { translatedShortDay } from '../../tickets/ticket-texts';
import { FoodFlashService } from '../food-flash.service';
import { mealTimeText } from '../food-texts';

// Infosys mad/show/:id: (showfood.phtml): "Mad: {kategori} - ret/slet", Id, Kategori, Engelsk, Pris, Skjult, and each
// meal time with Dag ("Ons 17:00"), Beskrivelse and Engelsk
@Component({
  selector: 'app-food-type-detail',
  imports: [RouterLink, MatButtonModule, MatIconModule, Card, TranslatePipe],
  templateUrl: './food-type-detail.html',
  styleUrl: './food-type-detail.scss',
})
export class FoodTypeDetailComponent implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly route = inject(ActivatedRoute);
  private readonly shortDay = translatedShortDay(inject(TranslationService));

  public readonly flash = inject(FoodFlashService).take();
  public readonly mayEdit = inject(AuthService).hasPrivilege('food_edit');
  public readonly food = signal<FoodTypeDetail | null>(null);
  public readonly notFound = signal(false);
  public readonly priceText = priceText;

  public ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    this.http.get<FoodTypeDetail>(`/api/food-types/${id}`).subscribe({
      next: food => this.food.set(food),
      error: () => this.notFound.set(true),
    });
  }

  public timeText(time: string): string {
    return mealTimeText(time, this.shortDay);
  }
}
