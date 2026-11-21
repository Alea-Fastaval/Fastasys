import { Component, ElementRef, HostListener, OnInit, ViewChild, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { TranslatePipe } from '@shared/pipes';
import { blobToDataUrl, loadImage } from '../../id-cards/auth-images';
import {
  Crop,
  PHOTO_HEIGHT,
  PHOTO_WIDTH,
  drawCrop,
  initialCrop,
  moveCrop,
  turn,
  zoomRange,
  zoomTo,
} from './crop-geometry';

type Status = 'success' | 'failure' | null;

// Infosys photo/form/:identifier: (PhotoController::showUploadForm, showuploadform.phtml and photo-cropper.js), the
// "Foto upload" button of the participant page: choose an image, cut it out for the ID card (213 x 295), and upload
// the cut-out and the original. A participant's earlier original is loaded so it can be cut out again.
//
// Agreed on 2026-11-18: staff use it from the participant page for now; the link participants get, without login, comes
// later. The texts are Infosys's, which speak to the participant.
@Component({
  selector: 'app-participant-photo',
  imports: [RouterLink, MatButtonModule, MatIconModule, TranslatePipe],
  templateUrl: './participant-photo.html',
  styleUrl: './participant-photo.scss',
})
export class ParticipantPhotoComponent implements OnInit {
  private readonly http = inject(HttpClient);

  @ViewChild('preview') private readonly previewCanvas?: ElementRef<HTMLCanvasElement>;
  @ViewChild('fileInput') private readonly fileInput?: ElementRef<HTMLInputElement>;

  public readonly width = PHOTO_WIDTH;
  public readonly height = PHOTO_HEIGHT;
  public readonly participantId = Number(inject(ActivatedRoute).snapshot.paramMap.get('id'));

  public readonly image = signal<HTMLImageElement | null>(null);
  public readonly crop = signal<Crop | null>(null);
  public readonly status = signal<Status>(null);
  public readonly busy = signal(false);

  // The image as it was chosen (cropit's preview background), uploaded as the original
  private original: string | null = null;
  private drag: { x: number; y: number } | null = null;

  public ngOnInit(): void {
    void this.loadExisting();
  }

  public chooseImage(): void {
    this.fileInput?.nativeElement.click();
  }

  public async imageChosen(event: Event): Promise<void> {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    input.value = '';
    if (file) await this.useImage(await blobToDataUrl(file));
  }

  public zoomRange(): { min: number; max: number } | null {
    const image = this.image();
    const crop = this.crop();
    return image && crop ? zoomRange(image, crop.rotation) : null;
  }

  public zoom(value: string): void {
    this.changeCrop((crop, image) => zoomTo(crop, Number(value), image));
  }

  public rotate(clockwise: boolean): void {
    this.changeCrop((crop, image) => turn(crop, image, clockwise));
  }

  public startDrag(event: MouseEvent): void {
    event.preventDefault();
    this.drag = { x: event.clientX, y: event.clientY };
  }

  @HostListener('document:mousemove', ['$event'])
  public onMouseMove(event: MouseEvent): void {
    const drag = this.drag;
    if (!drag) return;
    this.changeCrop((crop, image) => moveCrop(crop, event.clientX - drag.x, event.clientY - drag.y, image));
    this.drag = { x: event.clientX, y: event.clientY };
  }

  @HostListener('document:mouseup')
  public onMouseUp(): void {
    this.drag = null;
  }

  // "Upload": the cut-out and the original together (Infosys sent them as two requests)
  public async upload(): Promise<void> {
    const cropped = this.exportCrop();
    if (!this.original || !cropped) return;

    this.busy.set(true);
    try {
      await firstValueFrom(
        this.http.put(`/api/participants/${this.participantId}/photo`, { original: this.original, cropped }),
      );
      // Infosys indicateSuccess: the cropper is hidden and the success message shown
      this.status.set('success');
    } catch {
      this.status.set('failure');
    } finally {
      this.busy.set(false);
    }
  }

  // The cut-out as a PNG data URL (cropit's export), or null when there is nothing to cut out
  public exportCrop(): string | null {
    return this.image() && this.crop() ? (this.previewCanvas?.nativeElement.toDataURL('image/png') ?? null) : null;
  }

  // Infosys PhotoModel::getExistingImage: the participant's earlier original, if any
  private async loadExisting(): Promise<void> {
    try {
      const blob = await firstValueFrom(
        this.http.get(`/api/participants/${this.participantId}/photo/original`, { responseType: 'blob' }),
      );
      await this.useImage(await blobToDataUrl(blob));
    } catch {
      // No photo yet
    }
  }

  private async useImage(dataUrl: string): Promise<void> {
    const image = await loadImage(dataUrl);
    this.original = dataUrl;
    this.status.set(null);
    this.image.set(image);
    this.crop.set(initialCrop(image));
    this.redraw();
  }

  private changeCrop(change: (crop: Crop, image: HTMLImageElement) => Crop): void {
    const image = this.image();
    const crop = this.crop();
    if (!image || !crop) return;
    this.crop.set(change(crop, image));
    this.redraw();
  }

  private redraw(): void {
    const image = this.image();
    const crop = this.crop();
    const context = this.previewCanvas?.nativeElement.getContext('2d');
    if (image && crop && context) drawCrop(context, image, crop);
  }
}
