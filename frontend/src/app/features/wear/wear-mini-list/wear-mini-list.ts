import { Component, computed, effect, inject, signal, untracked } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { WearMiniList } from '@shared/types';
import { Card } from '@shared/components';
import { TranslatePipe } from '@shared/pipes';
import { QueryParamsService, UnknownQueryParamError } from '@shared/services';
import { miniListName } from '../wear-texts';

// "attributes": comma-separated attribute ids, e.g. "101,111"
export function parseAttributeIds(value: string): number[] {
  if (!/^\d+(,\d+)*$/.test(value)) throw new UnknownQueryParamError('attributes', value);
  return [...new Set(value.split(',').map(Number))];
}

// "size": a size attribute id, or "none" for the orders without a size
export function parseSize(value: string): number | 'none' {
  if (value === 'none') return 'none';
  if (!/^\d+$/.test(value)) throw new UnknownQueryParamError('size', value);
  return Number(value);
}

// The API's query: the attributes, the size among them, and noSize for "none"
export function miniListParams(attributes: number[] | null, size: number | 'none' | null): HttpParams {
  let params = new HttpParams();
  for (const id of [...(attributes ?? []), ...(typeof size === 'number' ? [size] : [])]) {
    params = params.append('attributeIds', String(id));
  }
  if (size === 'none') params = params.set('noSize', 'true');
  return params;
}

// Infosys wear/detailed/:id: (detailedminilist.phtml), the list behind a number of "Bestillings-oversigt": "Liste over
// {wear} - {model} - ... bestillinger", then each order with "ID", "Navn", "Kategori" (the wear's name and the price's
// category), "Antal" and "Str.". Infosys read ?model=..&size=..; here ?attributes=..&size=.. (size "none" is "Ingen
// størrelse", which Infosys showed as every order of the type; fixed 2026-11-26). Annulled participants are left out.
@Component({
  selector: 'app-wear-mini-list',
  imports: [RouterLink, MatButtonModule, MatIconModule, Card, TranslatePipe],
  providers: [QueryParamsService],
  templateUrl: './wear-mini-list.html',
  styleUrl: './wear-mini-list.scss',
})
export class WearMiniListComponent {
  private readonly http = inject(HttpClient);
  private readonly route = inject(ActivatedRoute);
  private readonly query = inject(QueryParamsService);
  private readonly attributes = this.query.parsed('attributes', parseAttributeIds);
  private readonly size = this.query.parsed('size', parseSize);

  public readonly wearTypeId = Number(this.route.snapshot.paramMap.get('id'));
  public readonly list = signal<WearMiniList | null>(null);
  public readonly failed = signal(false);
  // The unknown values that were removed from the URL, e.g. "size=big", or '' when there were none
  public readonly unknownQueryValues = computed(() =>
    this.query
      .unknownValues()
      .map(error => `${error.param}=${error.value}`)
      .join(', '),
  );

  constructor() {
    effect(() => {
      const params = miniListParams(this.attributes(), this.size());
      untracked(() => this.load(params));
    });
  }

  public heading(list: WearMiniList): string {
    return miniListName(list.wearTypeName, list.attributes);
  }

  private load(params: HttpParams): void {
    this.failed.set(false);
    this.http.get<WearMiniList>(`/api/wear-orders/by-type/${this.wearTypeId}`, { params }).subscribe({
      next: list => this.list.set(list),
      error: () => this.failed.set(true),
    });
  }
}
