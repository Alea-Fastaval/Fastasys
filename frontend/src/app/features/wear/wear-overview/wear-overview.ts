import { Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Card } from '@shared/components';
import { TranslatePipe } from '@shared/pipes';
import { AuthService } from '@shared/services';
import { WEAR_LINKS } from '../wear-links';

// Infosys wear/ ("Oversigt", main.phtml): "Du kan:" and a link to each wear page the user may open (see WEAR_LINKS)
@Component({
  selector: 'app-wear-overview',
  imports: [RouterLink, Card, TranslatePipe],
  templateUrl: './wear-overview.html',
  styleUrl: './wear-overview.scss',
})
export class WearOverviewComponent {
  private readonly auth = inject(AuthService);

  public readonly links = WEAR_LINKS.filter(link => this.auth.hasPrivilege(...link.privileges));
}
