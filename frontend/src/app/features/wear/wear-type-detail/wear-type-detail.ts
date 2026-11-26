import { Component, OnInit, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { WearAttribute, WearTypeDetail } from '@shared/types';
import { Card } from '@shared/components';
import { TranslatePipe } from '@shared/pipes';
import { AuthService } from '@shared/services';
import { priceText } from '../../participants/participant-price/participant-price';
import { WearFlashService } from '../wear-flash.service';
import { KindGroup, byKind, kindKey } from '../wear-texts';

// Infosys wear/viswear/:id: (showwear.phtml): "Wear: {navn} - ret/slet", Id, Navn, Engelsk titel, Beskrivelse,
// Engelsk beskrivelse, Maks bestilling and Påkrævet; each variant with its attributes by kind; and the prices (Id,
// Brugerkategori, Pris)
@Component({
  selector: 'app-wear-type-detail',
  imports: [RouterLink, MatButtonModule, MatIconModule, Card, TranslatePipe],
  templateUrl: './wear-type-detail.html',
  styleUrl: './wear-type-detail.scss',
})
export class WearTypeDetailComponent implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly route = inject(ActivatedRoute);

  public readonly flash = inject(WearFlashService).take();
  public readonly mayEdit = inject(AuthService).hasPrivilege('wear_edit');
  public readonly wear = signal<WearTypeDetail | null>(null);
  public readonly notFound = signal(false);
  public readonly priceText = priceText;
  public readonly kindKey = kindKey;

  public ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    this.http.get<WearTypeDetail>(`/api/wear-types/${id}`).subscribe({
      next: wear => this.wear.set(wear),
      error: () => this.notFound.set(true),
    });
  }

  public groups(variant: WearAttribute[]): KindGroup[] {
    return byKind(variant);
  }
}
