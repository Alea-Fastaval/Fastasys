import { Component, OnInit, inject, signal } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { Card } from '@shared/components';
import { TranslatePipe } from '@shared/pipes';
import { SignupSettings, UpdateSignupSettings } from '@shared/types';
import { formatConventionDateTime } from '../../participants/participant-format';

export type SignupStatusKey = 'open' | 'openLate' | 'closedSwitchedOff' | 'closedNoStart' | 'closedBeforeStart';

// Why the signup is open or closed (agreed 2026-11-24: open only when switched on AND the opening day has begun).
// When it's switched on but closed, the opening day hasn't begun yet, or it isn't set.
export function signupStatusKey(settings: SignupSettings): SignupStatusKey {
  if (settings.status.isSignupOpen) return settings.status.isLateSignup ? 'openLate' : 'open';
  if (!settings.isOpen) return 'closedSwitchedOff';
  return settings.status.signupStart ? 'closedBeforeStart' : 'closedNoStart';
}

@Component({
  selector: 'app-signup-settings',
  imports: [FormsModule, MatButtonModule, MatSlideToggleModule, Card, TranslatePipe],
  templateUrl: './signup-settings.html',
  styleUrl: './signup-settings.scss',
})
export class SignupSettingsComponent implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly url = '/api/management/signup-settings';
  public readonly loaded = signal(false);
  public readonly loading = signal(false);
  public readonly busy = signal(false);
  public readonly error = signal<string | null>(null);
  public readonly updated = signal(false);
  public readonly settings = signal<SignupSettings | null>(null);
  // The switch in the form; sent with update()
  public isOpen = false;

  public readonly statusKey = signupStatusKey;

  public ngOnInit(): void {
    this.load();
  }

  public load(): void {
    this.loading.set(true);
    this.error.set(null);
    this.http.get<SignupSettings>(this.url).subscribe({
      next: settings => {
        this.show(settings);
        this.loaded.set(true);
        this.loading.set(false);
      },
      error: (error: HttpErrorResponse) => {
        this.error.set(error.status === 403 ? 'forbidden' : 'loadFailed');
        this.loading.set(false);
      },
    });
  }

  // Only a change can be sent
  public canSubmit(): boolean {
    return this.loaded() && !this.loading() && !this.busy() && this.isOpen !== this.settings()?.isOpen;
  }

  public submit(): void {
    if (!this.canSubmit()) return;
    this.update();
  }

  public update(): void {
    const values: UpdateSignupSettings = { isOpen: this.isOpen };
    this.busy.set(true);
    this.error.set(null);
    this.updated.set(false);
    this.http.put<SignupSettings>(this.url, values).subscribe({
      next: settings => {
        this.show(settings);
        this.busy.set(false);
        this.updated.set(true);
      },
      error: (error: HttpErrorResponse) => {
        this.error.set(error.status === 403 ? 'forbidden' : 'updateFailed');
        this.busy.set(false);
      },
    });
  }

  // The junior opening in Danish time, e.g. "2027-01-15 10:00"; null while it isn't set
  public juniorStartText(settings: SignupSettings): string | null {
    const start = settings.status.juniorSignupStart;
    return start ? formatConventionDateTime(start) : null;
  }

  private show(settings: SignupSettings): void {
    this.settings.set(settings);
    this.isOpen = settings.isOpen;
  }
}
