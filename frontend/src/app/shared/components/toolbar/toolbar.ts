import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterLink, RouterLinkActive } from '@angular/router';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule, MatMenuTrigger } from '@angular/material/menu';
import { AuthService, TranslationService, SupportedLang } from '@shared/services';
import { TranslatePipe } from '@shared/pipes';
// Infosys has the participant search field in its page header
import { ParticipantQuickSearchComponent } from '../../../features/participants/participant-quick-search/participant-quick-search';
import { WEAR_LINKS, WEAR_PRIVILEGES, WearLink } from '../../../features/wear/wear-links';

@Component({
  selector: 'app-toolbar',
  standalone: true,
  imports: [
    CommonModule,
    RouterLink,
    RouterLinkActive,
    MatToolbarModule,
    MatButtonModule,
    MatIconModule,
    MatMenuModule,
    TranslatePipe,
    ParticipantQuickSearchComponent,
  ],
  templateUrl: './toolbar.html',
  styleUrl: './toolbar.scss',
})
export class Toolbar {
  readonly authService = inject(AuthService);
  readonly translationService = inject(TranslationService);
  private readonly router = inject(Router);

  private closeTimer: ReturnType<typeof setTimeout> | null = null;

  public isResourcesActive(): boolean {
    return this.router.url.startsWith('/resources');
  }

  // The Wear menu (Infosys has Wear as its own menu): shown with any wear privilege
  public mayUseWear(): boolean {
    return this.authService.hasPrivilege(...WEAR_PRIVILEGES);
  }

  // The wear pages the user may open, after "Oversigt"
  public wearLinks(): WearLink[] {
    return WEAR_LINKS.filter(link => this.authService.hasPrivilege(...link.privileges));
  }

  public isWearActive(): boolean {
    const url = this.router.url;
    return url === '/wear' || url.startsWith('/wear/') || url.startsWith('/wear?');
  }

  public isManagementActive(): boolean {
    const url = this.router.url;
    return url.startsWith('/management') || url.startsWith('/user-management');
  }

  public openMenu(trigger: MatMenuTrigger): void {
    this.cancelClose();
    if (!trigger.menuOpen) {
      trigger.openMenu();
    }
  }

  public closeMenu(trigger: MatMenuTrigger): void {
    this.closeTimer = setTimeout(() => {
      if (trigger.menuOpen) {
        trigger.closeMenu();
      }
    }, 150);
  }

  public cancelClose(): void {
    if (this.closeTimer) {
      clearTimeout(this.closeTimer);
      this.closeTimer = null;
    }
  }

  public setLang(lang: SupportedLang): void {
    void this.translationService.setLanguage(lang);
  }

  public logout(): void {
    this.authService.logout();
    this.router.navigate(['/login']);
  }
}
