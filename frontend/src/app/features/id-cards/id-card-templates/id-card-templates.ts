import { Component, HostListener, OnDestroy, OnInit, computed, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import {
  CategoryIdCardTemplate,
  ID_CARD_DATA_SOURCES,
  IdCardDataSource,
  IdCardItemType,
  IdCardTemplate,
  IdCardTemplateItem,
} from '@shared/types';
import { Card } from '@shared/components';
import { TranslatePipe } from '@shared/pipes';
import { TranslationService } from '@shared/services';
import { blobToDataUrl, fetchImageUrl } from '../auth-images';
import { applyManualPosition, constrainItem, itemTransform, moveItem, newItem, renderSize } from './template-geometry';

// Infosys's editor saves 100 ms after the last change
const SAVE_DELAY_MS = 100;

// The items' colours in the preview (Infosys gives each a random one)
const ITEM_HUES = [210, 30, 140, 280, 0, 90, 330, 180];

// A message under idCardTemplates.messages
type Message = 'saveFailed' | 'createFailed' | 'copyFailed' | 'removeFailed' | 'categoryFailed' | 'backgroundFailed';

// Infosys id-templates ("ID skabeloner", IdTemplateController::showEdit and public/js/idtemplate.js): the ID card
// templates. Pick a template in the list (or add or copy one), give it a background, and place photo, barcode and text
// items on it by dragging them or typing their position, size and rotation. Every change is saved at once, as in
// Infosys. "Kategori-skabeloner" gives each participant category its template.
@Component({
  selector: 'app-id-card-templates',
  imports: [FormsModule, RouterLink, MatButtonModule, MatIconModule, Card, TranslatePipe],
  templateUrl: './id-card-templates.html',
  styleUrl: './id-card-templates.scss',
})
export class IdCardTemplatesComponent implements OnInit, OnDestroy {
  private readonly http = inject(HttpClient);
  private readonly translation = inject(TranslationService);

  public readonly dataSources = ID_CARD_DATA_SOURCES;
  public readonly itemTransform = itemTransform;

  public readonly templates = signal<IdCardTemplate[]>([]);
  public readonly categories = signal<CategoryIdCardTemplate[]>([]);
  public readonly activeId = signal<number | null>(null);
  public readonly selectedIndex = signal<number | null>(null);
  public readonly backgroundUrl = signal<string | null>(null);
  public readonly message = signal<Message | null>(null);
  public newName = '';

  public readonly active = computed(() => this.templates().find(t => t.id === this.activeId()) ?? null);
  public readonly selected = computed(() => {
    const index = this.selectedIndex();
    return index === null ? null : (this.active()?.items[index] ?? null);
  });
  public readonly size = computed(() => {
    const active = this.active();
    return active?.hasBackground ? renderSize(active) : null;
  });

  private saveTimer: ReturnType<typeof setTimeout> | null = null;
  private drag: { index: number; x: number; y: number } | null = null;

  public ngOnInit(): void {
    this.http.get<IdCardTemplate[]>('/api/id-card-templates').subscribe(list => this.templates.set(list));
    this.loadCategories();
  }

  public ngOnDestroy(): void {
    this.flushSave();
    this.setBackgroundUrl(null);
  }

  // --- The templates list ---

  public activate(template: IdCardTemplate): void {
    if (this.activeId() === template.id) return;
    this.flushSave();
    this.activeId.set(template.id);
    this.selectedIndex.set(null);
    void this.loadBackground(template);
  }

  // Infosys addTemplateHandler: the plus or Enter adds a template with the trimmed name
  public add(): void {
    const name = this.newName.trim();
    if (!name) return;
    this.http.post<IdCardTemplate>('/api/id-card-templates', { name }).subscribe({
      next: created => {
        this.newName = '';
        this.templates.update(list => [...list, created]);
        this.activate(created);
      },
      error: () => this.message.set('createFailed'),
    });
  }

  public copy(template: IdCardTemplate): void {
    this.flushSave();
    this.http.post<IdCardTemplate>(`/api/id-card-templates/${template.id}/copy`, {}).subscribe({
      next: created => {
        this.templates.update(list => [...list, created]);
        this.activate(created);
      },
      error: () => this.message.set('copyFailed'),
    });
  }

  public remove(template: IdCardTemplate): void {
    if (!confirm(this.translation.translate('idCardTemplates.confirmDelete'))) return;
    this.http.delete(`/api/id-card-templates/${template.id}`).subscribe({
      next: () => {
        if (this.activeId() === template.id) {
          this.cancelSave();
          this.activeId.set(null);
          this.selectedIndex.set(null);
          this.setBackgroundUrl(null);
        }
        this.templates.update(list => list.filter(t => t.id !== template.id));
        // A category that used it has no template now
        this.loadCategories();
      },
      error: () => this.message.set('removeFailed'),
    });
  }

  // --- The active template ---

  public rename(name: string): void {
    this.updateActive(template => ({ ...template, name }));
  }

  public async setBackground(event: Event): Promise<void> {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    input.value = '';
    const active = this.active();
    if (!file || !active) return;

    try {
      const image = await blobToDataUrl(file);
      const updated = await firstValueFrom(
        this.http.put<IdCardTemplate>(`/api/id-card-templates/${active.id}/background`, { image }),
      );
      // Infosys constrainTemplateElements: items that stick out of the new card are moved back onto it
      this.replaceTemplate({ ...updated, items: active.items.map(item => constrainItem(item, updated)) });
      this.scheduleSave();
      await this.loadBackground(updated);
    } catch {
      this.message.set('backgroundFailed');
    }
  }

  public addItem(type: IdCardItemType): void {
    const active = this.active();
    if (!active?.hasBackground) return;
    this.updateActive(template => ({ ...template, items: [...template.items, newItem(type)] }));
    this.selectedIndex.set(active.items.length);
  }

  public removeSelected(): void {
    const index = this.selectedIndex();
    if (index === null) return;
    this.updateActive(template => ({ ...template, items: template.items.filter((_, i) => i !== index) }));
    this.selectedIndex.set(null);
  }

  // The typed X, Y, W, H and rotation
  public setPosition(field: 'x' | 'y' | 'width' | 'height' | 'rotation', value: number | null): void {
    const active = this.active();
    const item = this.selected();
    const index = this.selectedIndex();
    if (!active || !item || index === null) return;

    const typed = { x: item.x, y: item.y, width: item.width, height: item.height, rotation: item.rotation };
    typed[field] = Number(value) || 0;
    this.setItem(index, applyManualPosition(item, typed, active));
  }

  public setDataSource(dataSource: IdCardDataSource): void {
    const item = this.selected();
    const index = this.selectedIndex();
    if (!item || index === null) return;
    this.setItem(index, { ...item, dataSource });
  }

  // --- Dragging an item in the preview (Infosys dragStartHandler / dragHandler / dragEndHandler) ---

  public startDrag(index: number, event: MouseEvent): void {
    event.preventDefault();
    this.selectedIndex.set(index);
    this.drag = { index, x: event.screenX, y: event.screenY };
  }

  @HostListener('document:mousemove', ['$event'])
  public onMouseMove(event: MouseEvent): void {
    const drag = this.drag;
    const active = this.active();
    if (!drag || !active) return;

    const item = active.items[drag.index];
    this.setItem(drag.index, moveItem(item, event.screenX - drag.x, event.screenY - drag.y, active));
    this.drag = { ...drag, x: event.screenX, y: event.screenY };
  }

  @HostListener('document:mouseup')
  public onMouseUp(): void {
    this.drag = null;
  }

  // --- Kategori-skabeloner ---

  public setCategoryTemplate(category: CategoryIdCardTemplate, templateId: number | null): void {
    this.http.put(`/api/id-card-templates/categories/${category.categoryId}`, { templateId }).subscribe({
      next: () =>
        this.categories.update(list =>
          list.map(c => (c.categoryId === category.categoryId ? { ...c, templateId } : c)),
        ),
      error: () => {
        this.message.set('categoryFailed');
        this.loadCategories();
      },
    });
  }

  public itemFill(index: number): string {
    return `hsla(${ITEM_HUES[index % ITEM_HUES.length]}, 75%, 50%, 0.45)`;
  }

  public itemTypeKey(type: IdCardItemType): string {
    return `idCardTemplates.itemTypes.${type}`;
  }

  private setItem(index: number, item: IdCardTemplateItem): void {
    this.updateActive(template => ({ ...template, items: template.items.map((old, i) => (i === index ? item : old)) }));
  }

  // Changes the active template and saves it shortly after (Infosys persistTemplateChanges)
  private updateActive(change: (template: IdCardTemplate) => IdCardTemplate): void {
    const active = this.active();
    if (!active) return;
    this.replaceTemplate(change(active));
    this.scheduleSave();
  }

  private replaceTemplate(template: IdCardTemplate): void {
    this.templates.update(list => list.map(t => (t.id === template.id ? template : t)));
  }

  private scheduleSave(): void {
    this.cancelSave();
    const id = this.activeId();
    this.saveTimer = setTimeout(() => this.save(id), SAVE_DELAY_MS);
  }

  private cancelSave(): void {
    if (this.saveTimer !== null) clearTimeout(this.saveTimer);
    this.saveTimer = null;
  }

  // Saves a pending change at once (before another template is opened or the page is left)
  private flushSave(): void {
    if (this.saveTimer === null) return;
    this.cancelSave();
    this.save(this.activeId());
  }

  private save(id: number | null): void {
    this.saveTimer = null;
    const template = this.templates().find(t => t.id === id);
    if (!template) return;
    this.http
      .put<IdCardTemplate>(`/api/id-card-templates/${template.id}`, { name: template.name, items: template.items })
      .subscribe({
        next: () => {
          if (this.message() === 'saveFailed') this.message.set(null);
        },
        error: () => this.message.set('saveFailed'),
      });
  }

  private loadCategories(): void {
    this.http
      .get<CategoryIdCardTemplate[]>('/api/id-card-templates/categories')
      .subscribe(list => this.categories.set(list));
  }

  private async loadBackground(template: IdCardTemplate): Promise<void> {
    this.setBackgroundUrl(null);
    if (!template.hasBackground) return;
    try {
      const url = await fetchImageUrl(this.http, `/api/id-card-templates/${template.id}/background`);
      // Another template may have been opened meanwhile
      if (this.activeId() === template.id) {
        this.setBackgroundUrl(url);
      } else {
        URL.revokeObjectURL(url);
      }
    } catch {
      this.message.set('backgroundFailed');
    }
  }

  private setBackgroundUrl(url: string | null): void {
    const old = this.backgroundUrl();
    if (old) URL.revokeObjectURL(old);
    this.backgroundUrl.set(url);
  }
}
