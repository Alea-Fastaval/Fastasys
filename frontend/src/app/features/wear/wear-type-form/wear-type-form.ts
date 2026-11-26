import { Component, OnDestroy, OnInit, inject, signal } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { forkJoin } from 'rxjs';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import {
  ParticipantCategory,
  WearAttribute,
  WearImage,
  WearImageLink,
  WearTypeDetail,
  WearTypeFieldsDto,
} from '@shared/types';
import { Card } from '@shared/components';
import { TranslatePipe } from '@shared/pipes';
import { WearFlashService } from '../wear-flash.service';
import { KindGroup, byKind, kindKey } from '../wear-texts';

// wear.navn and title_en are varchar(64)
export const MAX_WEAR_NAME_LENGTH = 64;

// A row of "Wear priser": the category, the price, and how many orders were made at it (an ordered price can't be
// removed)
export interface WearPriceRow {
  participantCategoryId: number;
  categoryName: string;
  price: number | null;
  orderCount: number;
}

export interface WearTypeForm {
  nameDanish: string;
  descriptionDanish: string;
  nameEnglish: string;
  descriptionEnglish: string;
  maxOrder: number | null;
  isRequired: boolean;
  // Each variant's ticked attribute ids
  variants: number[][];
  prices: WearPriceRow[];
  imageLinks: WearImageLink[];
}

// Infosys's create form starts with one empty variant and Maks bestilling 0
export function newWearTypeForm(): WearTypeForm {
  return {
    nameDanish: '',
    descriptionDanish: '',
    nameEnglish: '',
    descriptionEnglish: '',
    maxOrder: 0,
    isRequired: false,
    variants: [[]],
    prices: [],
    imageLinks: [],
  };
}

export function toForm(wear: WearTypeDetail): WearTypeForm {
  return {
    nameDanish: wear.nameDanish,
    descriptionDanish: wear.descriptionDanish,
    nameEnglish: wear.nameEnglish,
    descriptionEnglish: wear.descriptionEnglish,
    maxOrder: wear.maxOrder,
    isRequired: wear.isRequired,
    variants: wear.variants.map(variant => variant.map(a => a.id)),
    prices: wear.prices.map(p => ({
      participantCategoryId: p.participantCategoryId,
      categoryName: p.categoryName,
      price: p.price,
      orderCount: p.orderCount,
    })),
    imageLinks: wear.imageLinks.map(l => ({ ...l })),
  };
}

export function toDto(form: WearTypeForm): WearTypeFieldsDto {
  return {
    nameDanish: form.nameDanish,
    nameEnglish: form.nameEnglish,
    descriptionDanish: form.descriptionDanish,
    descriptionEnglish: form.descriptionEnglish,
    maxOrder: form.maxOrder,
    isRequired: form.isRequired,
    variants: form.variants,
    prices: form.prices.map(p => ({ participantCategoryId: p.participantCategoryId, price: p.price })),
    imageLinks: form.imageLinks,
  };
}

function isWholeAtLeastZero(value: number | null): boolean {
  return value !== null && Number.isInteger(value) && value >= 0;
}

// The rules the API checks (WearTypesController.Validate): a navn that isn't empty or "0" (PHP empty()), names of at
// most 64 characters, and Maks bestilling and the prices whole numbers of 0 or more (Infosys took any text; fixed
// 2026-11-26). An empty Maks bestilling is saved as 0, like Infosys.
export function canSubmitWearType(form: WearTypeForm): boolean {
  return (
    form.nameDanish !== '' &&
    form.nameDanish !== '0' &&
    form.nameDanish.length <= MAX_WEAR_NAME_LENGTH &&
    form.nameEnglish.length <= MAX_WEAR_NAME_LENGTH &&
    (form.maxOrder === null || isWholeAtLeastZero(form.maxOrder)) &&
    form.prices.every(p => isWholeAtLeastZero(p.price))
  );
}

// wear.js removePrice/addPrice: the categories without a price, sorted by name like wear.js sortCategories
export function freeCategories(categories: ParticipantCategory[], prices: WearPriceRow[]): ParticipantCategory[] {
  return categories
    .filter(c => !prices.some(p => p.participantCategoryId === c.id))
    .sort((a, b) => (a.name < b.name ? -1 : a.name > b.name ? 1 : 0));
}

// The images on the form's list: those with links, in the order of their first link
export function linkedImageIds(links: WearImageLink[]): number[] {
  return [...new Set(links.map(l => l.wearImageId))];
}

// Infosys wear/createwear and wear/editwear/:id: ("Opret wear" / "Opdater wear"): the fields, "Varianter" (a block of
// checkboxes per variant, every attribute by kind; "Tilføj variant"), "Indstillinger for billeder" (add an uploaded
// image and tick the attributes it shows, or "Alle") and "Wear priser" (a price per participant category); and the
// delete confirmation (displaydelete.phtml, "Slet wear typen {navn}?"). Infosys folded the variants and images away
// until "Fold ud" was clicked; here they are always shown.
@Component({
  selector: 'app-wear-type-form',
  imports: [
    FormsModule,
    RouterLink,
    MatButtonModule,
    MatCheckboxModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatSelectModule,
    Card,
    TranslatePipe,
  ],
  templateUrl: './wear-type-form.html',
  styleUrl: './wear-type-form.scss',
})
export class WearTypeFormComponent implements OnInit, OnDestroy {
  private readonly http = inject(HttpClient);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly flash = inject(WearFlashService);
  private readonly objectUrls: string[] = [];

  public readonly maxNameLength = MAX_WEAR_NAME_LENGTH;
  public readonly kindKey = kindKey;
  public readonly wearId = signal<number | null>(null);
  public readonly form = signal<WearTypeForm | null>(null);
  public readonly attributes = signal<WearAttribute[]>([]);
  public readonly categories = signal<ParticipantCategory[]>([]);
  public readonly images = signal<WearImage[]>([]);
  public readonly notFound = signal(false);
  public readonly busy = signal(false);
  public readonly errorKey = signal<string | null>(null);
  public readonly errorParams = signal<Record<string, string> | undefined>(undefined);
  public readonly confirmingDelete = signal(false);

  // "Wear priser": the category and price of the row being added
  public newCategoryId: number | null = null;
  public newPrice: number | null = null;

  // "Indstillinger for billeder": the images on the list, the one chosen there, and the "Tilføj billede" dialog
  public readonly addedImageIds = signal<number[]>([]);
  public readonly selectedImageId = signal<number | null>(null);
  public readonly dialogOpen = signal(false);
  public readonly dialogImageId = signal<number | null>(null);
  public readonly previewUrls = signal<Record<number, string>>({});

  public ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) this.wearId.set(Number(id));

    forkJoin({
      attributes: this.http.get<WearAttribute[]>('/api/wear-attributes'),
      categories: this.http.get<ParticipantCategory[]>('/api/wear-types/categories'),
      images: this.http.get<WearImage[]>('/api/wear-images'),
    }).subscribe({
      next: ({ attributes, categories, images }) => {
        this.attributes.set(attributes);
        this.categories.set(categories);
        this.images.set(images);
        if (!id) this.form.set(newWearTypeForm());
      },
      error: () => this.notFound.set(true),
    });

    if (!id) return;
    this.http.get<WearTypeDetail>(`/api/wear-types/${id}`).subscribe({
      next: wear => {
        const form = toForm(wear);
        this.form.set(form);
        this.addedImageIds.set(linkedImageIds(form.imageLinks));
      },
      error: () => this.notFound.set(true),
    });
  }

  public ngOnDestroy(): void {
    this.objectUrls.forEach(url => URL.revokeObjectURL(url));
  }

  // Every attribute by kind, for each variant's checkboxes
  public attributeGroups(): KindGroup[] {
    return byKind(this.attributes());
  }

  public isTicked(variant: number[], attribute: WearAttribute): boolean {
    return variant.includes(attribute.id);
  }

  public toggleAttribute(form: WearTypeForm, index: number, attribute: WearAttribute, ticked: boolean): void {
    const variant = form.variants[index].filter(id => id !== attribute.id);
    form.variants[index] = ticked ? [...variant, attribute.id] : variant;
    this.form.set({ ...form });
  }

  // "Tilføj variant"
  public addVariant(form: WearTypeForm): void {
    this.form.set({ ...form, variants: [...form.variants, []] });
  }

  // The categories the price select offers
  public freeCategories(form: WearTypeForm): ParticipantCategory[] {
    return freeCategories(this.categories(), form.prices);
  }

  // "Tilføj" under the prices
  public addPrice(form: WearTypeForm): void {
    const category = this.categories().find(c => c.id === this.newCategoryId);
    if (!category) return;
    const row: WearPriceRow = {
      participantCategoryId: category.id,
      categoryName: category.name,
      price: this.newPrice,
      orderCount: 0,
    };
    this.form.set({ ...form, prices: [...form.prices, row] });
    this.newCategoryId = null;
    this.newPrice = null;
  }

  // "Slet" on a price; an ordered price can't be removed
  public removePrice(form: WearTypeForm, row: WearPriceRow): void {
    if (row.orderCount > 0) return;
    this.form.set({ ...form, prices: form.prices.filter(p => p !== row) });
  }

  public imageName(id: number): string {
    return this.images().find(i => i.id === id)?.fileName ?? '';
  }

  // The uploaded images that aren't on the list yet
  public availableImages(): WearImage[] {
    return this.images().filter(i => !this.addedImageIds().includes(i.id));
  }

  public openImageDialog(): void {
    this.dialogImageId.set(null);
    this.dialogOpen.set(true);
  }

  public previewInDialog(id: number): void {
    this.dialogImageId.set(id);
    this.loadPreview(id);
  }

  // "Tilføj billede" / "Annulér" in the dialog
  public closeImageDialog(confirm: boolean): void {
    const id = this.dialogImageId();
    this.dialogOpen.set(false);
    this.dialogImageId.set(null);
    if (confirm && id !== null) this.addedImageIds.update(ids => [...ids, id]);
  }

  public selectImage(id: number): void {
    this.selectedImageId.set(id);
    this.loadPreview(id);
  }

  // The attributes an image can be shown for: those ticked in a variant, and those it is linked to already
  public imageAttributeGroups(form: WearTypeForm, imageId: number): KindGroup[] {
    const ids = new Set([
      ...form.variants.flat(),
      ...form.imageLinks.filter(l => l.wearImageId === imageId && l.attributeId !== null).map(l => l.attributeId),
    ]);
    return byKind(this.attributes().filter(a => ids.has(a.id)));
  }

  public isLinked(form: WearTypeForm, imageId: number, attributeId: number | null): boolean {
    return form.imageLinks.some(l => l.wearImageId === imageId && l.attributeId === attributeId);
  }

  public toggleLink(form: WearTypeForm, imageId: number, attributeId: number | null, ticked: boolean): void {
    const others = form.imageLinks.filter(l => !(l.wearImageId === imageId && l.attributeId === attributeId));
    this.form.set({
      ...form,
      imageLinks: ticked ? [...others, { wearImageId: imageId, attributeId }] : others,
    });
  }

  public canSubmit(form: WearTypeForm): boolean {
    return !this.busy() && canSubmitWearType(form);
  }

  public submit(): void {
    const form = this.form();
    if (!form || !this.canSubmit(form)) return;

    const id = this.wearId();
    this.busy.set(true);
    this.errorKey.set(null);

    if (id === null) {
      this.create(toDto(form));
    } else {
      this.update(id, toDto(form));
    }
  }

  // Infosys "Fortryd": "Wear typen blev ikke oprettet." on the Wear page, or "Wear-typen blev ikke opdateret." on
  // the type's page
  public cancel(): void {
    const id = this.wearId();
    if (id === null) {
      this.flash.set('error', 'notCreated');
      void this.router.navigate(['/resources/wear']);
    } else {
      this.flash.set('error', 'notUpdated');
      void this.router.navigate(['/resources/wear/types', id]);
    }
  }

  public askDelete(): void {
    this.confirmingDelete.set(true);
  }

  // Infosys goes to its Wear page after a delete, and back to the type when it is refused or fails
  public confirmDelete(): void {
    const id = this.wearId();
    if (id === null) return;

    this.busy.set(true);
    this.http.delete(`/api/wear-types/${id}`).subscribe({
      next: () => {
        this.flash.set('success', 'deleted');
        void this.router.navigate(['/resources/wear']);
      },
      error: (err: HttpErrorResponse) => {
        if (err.error?.reason === 'inUse') {
          this.flash.set('error', 'inUse', { count: String(err.error.orderCount) });
        } else {
          this.flash.set('error', 'deleteFailed');
        }
        void this.router.navigate(['/resources/wear/types', id]);
      },
    });
  }

  // Infosys "Fortryd" on the delete confirmation goes back to the type, without a message
  public cancelDelete(): void {
    void this.router.navigate(['/resources/wear/types', this.wearId()]);
  }

  private create(dto: WearTypeFieldsDto): void {
    this.http.post<WearTypeDetail>('/api/wear-types', dto).subscribe({
      next: created => {
        this.flash.set('success', 'created');
        void this.router.navigate(['/resources/wear/types', created.id]);
      },
      error: () => this.fail('createFailed'),
    });
  }

  private update(id: number, dto: WearTypeFieldsDto): void {
    this.http.put<WearTypeDetail>(`/api/wear-types/${id}`, dto).subscribe({
      next: () => {
        this.flash.set('success', 'updated');
        void this.router.navigate(['/resources/wear/types', id]);
      },
      error: (err: HttpErrorResponse) => {
        if (err.error?.reason === 'priceOrdered') {
          this.fail('priceOrdered', { category: String(err.error.categoryName) });
        } else {
          this.fail('updateFailed');
        }
      },
    });
  }

  // The form keeps what was typed, like the other forms (Infosys went to another page)
  private fail(key: string, params?: Record<string, string>): void {
    this.busy.set(false);
    this.errorKey.set(key);
    this.errorParams.set(params);
  }

  // The images are behind login, so they are fetched and shown as object URLs
  private loadPreview(id: number): void {
    if (this.previewUrls()[id]) return;
    this.http.get(`/api/wear-images/${id}/file`, { responseType: 'blob' }).subscribe({
      next: blob => {
        const url = URL.createObjectURL(blob);
        this.objectUrls.push(url);
        this.previewUrls.update(urls => ({ ...urls, [id]: url }));
      },
      error: () => undefined,
    });
  }
}
