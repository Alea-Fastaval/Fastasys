import { Component, OnDestroy, inject, signal } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { CreateWearImageDto, WearImage } from '@shared/types';
import { Card } from '@shared/components';
import { TranslatePipe } from '@shared/pipes';

// The file as a data URL, for the API
export function readAsDataUrl(file: Blob): Promise<string> {
  return new Promise((resolve, reject) => {
    const reader = new FileReader();
    reader.onload = () => resolve(reader.result as string);
    reader.onerror = () => reject(reader.error);
    reader.readAsDataURL(file);
  });
}

// Infosys wear/upload-image ("Upload billede til wear"): "Vælg billede" and "Upload". After an upload it says
// "Billedet {navn} blev uploaded." and shows the image. Infosys's messages are kept, "Infosys" included.
@Component({
  selector: 'app-wear-image-upload',
  imports: [RouterLink, MatButtonModule, MatIconModule, Card, TranslatePipe],
  templateUrl: './wear-image-upload.html',
  styleUrl: './wear-image-upload.scss',
})
export class WearImageUploadComponent implements OnDestroy {
  private readonly http = inject(HttpClient);

  public readonly file = signal<File | null>(null);
  public readonly busy = signal(false);
  public readonly uploaded = signal<WearImage | null>(null);
  public readonly imageUrl = signal<string | null>(null);
  public readonly errorKey = signal<string | null>(null);

  public ngOnDestroy(): void {
    this.revoke();
  }

  public choose(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.file.set(input.files?.[0] ?? null);
  }

  public async upload(): Promise<void> {
    const file = this.file();
    if (!file || this.busy()) return;

    this.busy.set(true);
    this.errorKey.set(null);
    let data: string;
    try {
      data = await readAsDataUrl(file);
    } catch {
      this.fail('uploadFailed');
      return;
    }

    const dto: CreateWearImageDto = { fileName: file.name, data };
    this.http.post<WearImage>('/api/wear-images', dto).subscribe({
      next: image => {
        this.revoke();
        this.uploaded.set(image);
        this.imageUrl.set(URL.createObjectURL(file));
        this.busy.set(false);
      },
      error: (err: HttpErrorResponse) => {
        const reason = String(err.error?.reason ?? '');
        this.fail(reason === 'notImage' || reason === 'nameTaken' ? reason : 'uploadFailed');
      },
    });
  }

  private fail(key: string): void {
    this.errorKey.set(key);
    this.busy.set(false);
  }

  private revoke(): void {
    const url = this.imageUrl();
    if (url) URL.revokeObjectURL(url);
    this.imageUrl.set(null);
  }
}
