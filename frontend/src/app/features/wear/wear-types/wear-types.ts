import { Component, OnInit, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MoveWearTypeDto, WearTypeSummary } from '@shared/types';
import { Card } from '@shared/components';
import { TranslatePipe } from '@shared/pipes';
import { AuthService } from '@shared/services';
import { WearFlashService } from '../wear-flash.service';

// Infosys wear/showwear ("Wear-typer"): Id, Navn, Beskrivelse and Rækkefølge, by position. Infosys moved a type with
// its up and down arrows (or by clicking a row and using the arrow keys), and the change was saved at once.
@Component({
  selector: 'app-wear-types',
  imports: [RouterLink, MatButtonModule, MatIconModule, Card, TranslatePipe],
  templateUrl: './wear-types.html',
  styleUrl: './wear-types.scss',
})
export class WearTypesComponent implements OnInit {
  private readonly http = inject(HttpClient);

  public readonly flash = inject(WearFlashService).take();
  public readonly mayEdit = inject(AuthService).hasPrivilege('wear_edit');
  public readonly types = signal<WearTypeSummary[] | null>(null);
  public readonly failed = signal(false);
  public readonly moveFailed = signal(false);
  public readonly busy = signal(false);

  public ngOnInit(): void {
    this.load();
  }

  public move(type: WearTypeSummary, direction: MoveWearTypeDto['direction']): void {
    if (this.busy()) return;
    this.busy.set(true);
    this.moveFailed.set(false);
    const dto: MoveWearTypeDto = { direction };
    this.http.put(`/api/wear-types/${type.id}/position`, dto).subscribe({
      next: () => this.load(),
      error: () => {
        this.busy.set(false);
        this.moveFailed.set(true);
      },
    });
  }

  private load(): void {
    this.http.get<WearTypeSummary[]>('/api/wear-types').subscribe({
      next: types => {
        this.types.set(types);
        this.busy.set(false);
      },
      error: () => {
        this.failed.set(true);
        this.busy.set(false);
      },
    });
  }
}
