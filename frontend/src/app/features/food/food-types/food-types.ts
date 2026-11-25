import { Component, OnInit, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { FoodTypeSummary } from '@shared/types';
import { Card } from '@shared/components';
import { TranslatePipe } from '@shared/pipes';
import { AuthService } from '@shared/services';
import { priceText } from '../../participants/participant-price/participant-price';
import { FoodFlashService } from '../food-flash.service';

// Infosys mad/foodtypes ("Mad-typer" in the Mad menu): Id, Skjult, Kategori and Pris, in id order. Infosys's landing
// page mad/ only linked here, to "Opret mad-type" and to the handout; this page links "Opret mad-type" and "Statistik".
@Component({
  selector: 'app-food-types',
  imports: [RouterLink, MatButtonModule, MatIconModule, Card, TranslatePipe],
  templateUrl: './food-types.html',
  styleUrl: './food-types.scss',
})
export class FoodTypesComponent implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly auth = inject(AuthService);

  public readonly flash = inject(FoodFlashService).take();
  public readonly mayEdit = this.auth.hasPrivilege('food_edit');
  public readonly maySeeStats = this.auth.hasPrivilege('food_stats_view', 'food_view', 'food_edit');
  public readonly types = signal<FoodTypeSummary[] | null>(null);
  public readonly failed = signal(false);
  public readonly priceText = priceText;

  public ngOnInit(): void {
    this.http.get<FoodTypeSummary[]>('/api/food-types').subscribe({
      next: types => this.types.set(types),
      error: () => this.failed.set(true),
    });
  }
}
