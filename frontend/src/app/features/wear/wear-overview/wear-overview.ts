import { Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Card } from '@shared/components';
import { TranslatePipe } from '@shared/pipes';
import { AuthService } from '@shared/services';

export interface WearLink {
  // Translation key under wear.links
  key: string;
  path: string;
  privileges: string[];
}

// Infosys wear/ ("Oversigt", main.phtml) lists six of its Wear menu's pages after "Du kan:"; the menu also has
// "Rediger wear egenskaber" and "Upload billede til wear", which are listed here too, as Fastasys has one Wear menu
// item. Infosys's link to the orders not handed out went to all orders; it goes to the right list here. Each link is
// shown to the users who may open the page.
export const WEAR_LINKS: WearLink[] = [
  { key: 'breakdown', path: '/resources/wear/breakdown', privileges: ['wear_view', 'wear_edit'] },
  { key: 'orders', path: '/resources/wear/orders', privileges: ['wear_list_view', 'wear_distribute_edit'] },
  { key: 'unfilled', path: '/resources/wear/orders/unfilled', privileges: ['wear_list_view', 'wear_distribute_edit'] },
  { key: 'types', path: '/resources/wear/types', privileges: ['wear_view', 'wear_edit'] },
  { key: 'create', path: '/resources/wear/types/new', privileges: ['wear_edit'] },
  { key: 'attributes', path: '/resources/wear/attributes', privileges: ['wear_edit'] },
  { key: 'uploadImage', path: '/resources/wear/images/upload', privileges: ['wear_edit'] },
  { key: 'handout', path: '/resources/wear/handout', privileges: ['wear_distribute_edit'] },
];

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
