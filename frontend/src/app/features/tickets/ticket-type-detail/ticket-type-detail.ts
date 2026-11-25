import { Component, OnInit, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { TicketType } from '@shared/types';
import { Card } from '@shared/components';
import { TranslatePipe } from '@shared/pipes';
import { AuthService, TranslationService } from '@shared/services';
import { priceText } from '../../participants/participant-price/participant-price';
import { TicketFlashService } from '../ticket-flash.service';
import { dayWithDate, translatedLongDay } from '../ticket-texts';

// Infosys indgang/entry/:id: (showtype.phtml): Id, Type, Pris and Start, with a "ret/slet" link. Infosys's heading
// was a description made from words in the name ("Indgang Alea Ung Partout"); the name is shown instead, and the fields
// that now say what the type is (agreed 2026-11-24) are listed too.
@Component({
  selector: 'app-ticket-type-detail',
  imports: [RouterLink, MatButtonModule, MatIconModule, Card, TranslatePipe],
  templateUrl: './ticket-type-detail.html',
  styleUrl: './ticket-type-detail.scss',
})
export class TicketTypeDetailComponent implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly route = inject(ActivatedRoute);
  private readonly longDay = translatedLongDay(inject(TranslationService));

  public readonly flash = inject(TicketFlashService).take();
  public readonly mayEdit = inject(AuthService).hasPrivilege('entrance_edit');
  public readonly type = signal<TicketType | null>(null);
  public readonly notFound = signal(false);
  public readonly priceText = priceText;

  public ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    this.http.get<TicketType>(`/api/ticket-types/${id}`).subscribe({
      next: type => this.type.set(type),
      error: () => this.notFound.set(true),
    });
  }

  // "Onsdag 24/03", like the form's "Dag"
  public dayText(day: string): string {
    return dayWithDate(day, this.longDay);
  }
}
