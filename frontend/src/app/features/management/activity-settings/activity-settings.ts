import { Component, OnInit, inject, signal } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { Card } from '@shared/components';
import { TranslatePipe } from '@shared/pipes';
import { ActivitySettings } from '@shared/types';

@Component({
  selector: 'app-activity-settings',
  imports: [FormsModule, MatButtonModule, MatFormFieldModule, MatInputModule, Card, TranslatePipe],
  templateUrl: './activity-settings.html',
  styleUrl: './activity-settings.scss',
})
export class ActivitySettingsComponent implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly url = '/api/management/activity-settings';
  public readonly loaded = signal(false);
  public readonly loading = signal(false);
  public readonly saving = signal(false);
  public readonly error = signal<string | null>(null);
  public readonly saved = signal(false);
  public form: ActivitySettings = { smallKarma: 7, mediumKarma: 0, bigKarma: 10 };

  public ngOnInit(): void {
    this.load();
  }

  public load(): void {
    this.loading.set(true);
    this.error.set(null);
    this.http.get<ActivitySettings>(this.url).subscribe({
      next: settings => {
        this.form = settings;
        this.loaded.set(true);
        this.loading.set(false);
      },
      error: (error: HttpErrorResponse) => {
        this.error.set(error.status === 403 ? 'forbidden' : 'loadFailed');
        this.loading.set(false);
      },
    });
  }

  public canSave(): boolean {
    return (
      this.loaded() &&
      !this.loading() &&
      !this.saving() &&
      [this.form.smallKarma, this.form.mediumKarma, this.form.bigKarma].every(
        value => Number.isInteger(value) && value >= 0 && value <= 1000,
      )
    );
  }

  public save(): void {
    if (!this.canSave()) return;
    this.saving.set(true);
    this.error.set(null);
    this.saved.set(false);
    this.http.put<ActivitySettings>(this.url, this.form).subscribe({
      next: settings => {
        this.form = settings;
        this.saving.set(false);
        this.saved.set(true);
      },
      error: (error: HttpErrorResponse) => {
        this.error.set(error.status === 403 ? 'forbidden' : 'saveFailed');
        this.saving.set(false);
      },
    });
  }
}
