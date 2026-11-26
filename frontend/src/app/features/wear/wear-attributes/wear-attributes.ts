import { Component, OnInit, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import {
  CreateWearAttributeDto,
  UpdateWearAttributeDto,
  WEAR_ATTRIBUTE_KINDS,
  WearAttribute,
  WearAttributeKind,
} from '@shared/types';
import { Card } from '@shared/components';
import { TranslatePipe } from '@shared/pipes';
import { TranslationService } from '@shared/services';
import { kindKey, sortAttributes } from '../wear-texts';

// A row of the page: the attribute as saved, and the names as typed. A new row has no id until it is saved.
export interface AttributeRow {
  id: number | null;
  kind: WearAttributeKind;
  nameDanish: string;
  nameEnglish: string;
  savedDanish: string;
  savedEnglish: string;
}

export function toRow(attribute: WearAttribute): AttributeRow {
  return {
    id: attribute.id,
    kind: attribute.kind,
    nameDanish: attribute.nameDanish,
    nameEnglish: attribute.nameEnglish,
    savedDanish: attribute.nameDanish,
    savedEnglish: attribute.nameEnglish,
  };
}

// wear_attributes.js: "Gem" shows on a new row and on a row whose names were changed
export function isChanged(row: AttributeRow): boolean {
  return row.id === null || row.nameDanish !== row.savedDanish || row.nameEnglish !== row.savedEnglish;
}

// Infosys wear/attributes ("Rediger wear egenskaber", wear_attributes.js): a table per kind with Dansk and Engelsk,
// edited in place and saved row by row with "Gem"; "Tilføj egenskab" adds a row, "navn"/"name" to begin with.
// Infosys didn't note the id of a new row after saving it, so saving it again created a second attribute; a saved row
// keeps its id here. Its "Tilføj Kategori" (a new kind) isn't ported: the kinds are fixed (agreed 2026-11-26).
@Component({
  selector: 'app-wear-attributes',
  imports: [FormsModule, RouterLink, MatButtonModule, MatIconModule, Card, TranslatePipe],
  templateUrl: './wear-attributes.html',
  styleUrl: './wear-attributes.scss',
})
export class WearAttributesComponent implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly translation = inject(TranslationService);

  public readonly kinds = WEAR_ATTRIBUTE_KINDS;
  public readonly kindKey = kindKey;
  public readonly isChanged = isChanged;
  public readonly rows = signal<AttributeRow[] | null>(null);
  public readonly failed = signal(false);
  public readonly saveFailed = signal(false);
  public readonly busy = signal(false);

  public ngOnInit(): void {
    this.http.get<WearAttribute[]>('/api/wear-attributes').subscribe({
      next: attributes => this.rows.set(sortAttributes(attributes).map(toRow)),
      error: () => this.failed.set(true),
    });
  }

  public rowsOf(kind: WearAttributeKind): AttributeRow[] {
    return (this.rows() ?? []).filter(r => r.kind === kind);
  }

  // "Tilføj egenskab"
  public addRow(kind: WearAttributeKind): void {
    const row: AttributeRow = {
      id: null,
      kind,
      nameDanish: this.translation.translate('wear.newAttributeDanish'),
      nameEnglish: this.translation.translate('wear.newAttributeEnglish'),
      savedDanish: '',
      savedEnglish: '',
    };
    this.rows.update(rows => [...(rows ?? []), row]);
  }

  // "Gem"
  public save(row: AttributeRow): void {
    if (this.busy()) return;
    this.busy.set(true);
    this.saveFailed.set(false);

    const fields: UpdateWearAttributeDto = { nameDanish: row.nameDanish, nameEnglish: row.nameEnglish };
    const created: CreateWearAttributeDto = { ...fields, kind: row.kind };
    const request =
      row.id === null
        ? this.http.post<WearAttribute>('/api/wear-attributes', created)
        : this.http.put<WearAttribute>(`/api/wear-attributes/${row.id}`, fields);
    request.subscribe({
      next: saved => {
        this.rows.update(rows => (rows ?? []).map(r => (r === row ? toRow(saved) : r)));
        this.busy.set(false);
      },
      error: () => {
        this.saveFailed.set(true);
        this.busy.set(false);
      },
    });
  }
}
