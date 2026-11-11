import { Component, computed, effect, inject, signal, untracked } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { toObservable } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatSortModule, Sort } from '@angular/material/sort';
import { catchError, of, switchMap } from 'rxjs';
import {
  CriteriaSearchStep,
  OrganizerArea,
  ParticipantCategory,
  ParticipantOptions,
  ParticipantRow,
  ParticipantSearchResult,
  ParticipantSearchStep,
} from '@shared/types';
import { TranslatePipe } from '@shared/pipes';
import { AuthService, QueryParamsService, TranslationService } from '@shared/services';
import { ParticipantFlashService } from './participant-flash.service';
import { sortByDanish } from './participant-format';
import {
  EDITOR_COLUMNS,
  LIST_COLUMNS,
  ListColumn,
  SORT_DIRECTIONS,
  SORT_KEYS,
  cellText,
  columnsUsedBy,
  filterRows,
  parseSearch,
  serializeSearch,
  sortRows,
  toApiSteps,
} from './participant-list';
import { ParticipantSearchFormComponent } from './participant-search-form/participant-search-form';

// Infosys DataTables offers 10, 25, 50, 100 and All rows per page, 25 to start with; 0 is All here
export const PAGE_SIZES = [10, 25, 50, 100, 0] as const;

interface SearchRequest {
  steps: ParticipantSearchStep[];
  columns: ListColumn[];
}

// Infosys deltager/visalle ("Alle deltagere") and deltager/search_result ("Søgning"): the participants found by the
// searches, with id, name and the added columns. The searches, the columns, the sorting and the search field are
// kept in the URL (Infosys kept the searches in the session).
@Component({
  selector: 'app-participants',
  imports: [
    FormsModule,
    RouterLink,
    MatButtonModule,
    MatIconModule,
    MatSortModule,
    TranslatePipe,
    ParticipantSearchFormComponent,
  ],
  providers: [QueryParamsService],
  templateUrl: './participants.html',
  styleUrl: './participants.scss',
})
export class ParticipantsComponent {
  private readonly http = inject(HttpClient);
  private readonly query = inject(QueryParamsService);
  private readonly translation = inject(TranslationService);
  private readonly canEdit = inject(AuthService).hasPrivilege('participants_edit');

  public readonly steps = this.query.parsed('search', parseSearch);
  private readonly chosenColumns = this.query.listOf('columns', LIST_COLUMNS);
  public readonly sortKey = this.query.oneOf('sort', SORT_KEYS);
  public readonly sortDirection = this.query.oneOf('dir', SORT_DIRECTIONS);
  public readonly filter = this.query.text('filter');

  public readonly flash = inject(ParticipantFlashService).take();
  public readonly pageSizes = PAGE_SIZES;
  public readonly rows = signal<ParticipantRow[] | null>(null);
  public readonly failed = signal(false);
  public readonly pageSize = signal<number>(25);
  public readonly page = signal(0);
  public readonly showColumns = signal(false);
  public readonly showSearch = signal(false);
  public readonly categories = signal<ParticipantCategory[]>([]);
  public readonly organizerAreas = signal<OrganizerArea[]>([]);

  // The columns the user may add, by their name as Infosys lists them (asort)
  public readonly availableColumns = computed(() => {
    this.translation.translations();
    return LIST_COLUMNS.filter(c => this.canEdit || !EDITOR_COLUMNS.includes(c))
      .map(c => ({ column: c, label: this.translation.translate(`participants.listColumns.${c}`) }))
      .sort((a, b) => (a.label < b.label ? -1 : a.label > b.label ? 1 : 0));
  });

  public readonly columns = computed(() =>
    (this.chosenColumns() ?? []).filter(c => this.canEdit || !EDITOR_COLUMNS.includes(c)),
  );

  // Only a change of the searches or the columns fetches the list again
  private readonly request = computed<SearchRequest>(() => ({ steps: this.steps() ?? [], columns: this.columns() }), {
    equal: (a, b) => JSON.stringify(a) === JSON.stringify(b),
  });

  // ja and nej in the user's language
  private readonly yesNo = computed(() => {
    this.translation.translations();
    return { yes: this.translation.translate('participants.yes'), no: this.translation.translate('participants.no') };
  });

  private readonly texts = computed(() => {
    const { yes, no } = this.yesNo();
    const columns = this.columns();
    return (row: ParticipantRow) => columns.map(c => cellText(c, row.values[c], yes, no));
  });

  public readonly filtered = computed(() => filterRows(this.rows() ?? [], this.filter() ?? '', this.texts()));
  private readonly sorted = computed(() => sortRows(this.filtered(), this.sortKey(), this.sortDirection() ?? 'asc'));
  public readonly pageCount = computed(() =>
    this.pageSize() === 0 ? 1 : Math.max(1, Math.ceil(this.sorted().length / this.pageSize())),
  );
  public readonly visibleRows = computed(() => {
    const size = this.pageSize();
    if (size === 0) return this.sorted();
    const start = Math.min(this.page(), this.pageCount() - 1) * size;
    return this.sorted().slice(start, start + size);
  });
  public readonly firstShown = computed(() =>
    this.visibleRows().length === 0 ? 0 : Math.min(this.page(), this.pageCount() - 1) * (this.pageSize() || 0) + 1,
  );
  public readonly lastShown = computed(() =>
    this.firstShown() === 0 ? 0 : this.firstShown() + this.visibleRows().length - 1,
  );

  // "Søgning: <term>" for a quick search, "Søgning" for the search box, null without a search
  public readonly searchLabel = computed(() => {
    const steps = this.steps();
    if (!steps || steps.length === 0) return null;
    const first = steps[0];
    return steps.length === 1 && 'quick' in first ? first.quick : '';
  });

  public readonly unknownQueryValues = computed(() =>
    this.query
      .unknownValues()
      .map(error => `${error.param}=${error.value}`)
      .join(', '),
  );

  constructor() {
    toObservable(this.request)
      .pipe(
        switchMap(request => {
          this.rows.set(null);
          this.failed.set(false);
          return this.http
            .post<ParticipantSearchResult>('/api/participants/search', {
              steps: toApiSteps(request.steps),
              columns: request.columns,
            })
            .pipe(
              catchError(err => {
                console.error('Failed to load participants:', err);
                this.failed.set(true);
                return of(null);
              }),
            );
        }),
      )
      .subscribe(result => {
        if (result) this.rows.set(result.rows);
      });

    // Like DataTables, the list goes back to the first page when its rows, filter or sorting change
    effect(() => {
      this.filtered();
      this.sortKey();
      this.sortDirection();
      untracked(() => this.page.set(0));
    });
  }

  public text(row: ParticipantRow, column: ListColumn): string {
    const { yes, no } = this.yesNo();
    return cellText(column, row.values[column], yes, no);
  }

  public columnLabel(column: ListColumn): string {
    return `participants.listColumns.${column}`;
  }

  public isShown(column: ListColumn): boolean {
    return this.columns().includes(column);
  }

  public toggleColumn(column: ListColumn): void {
    const columns = this.isShown(column) ? this.columns().filter(c => c !== column) : [...this.columns(), column];
    this.query.update({ columns: columns.length ? columns.join(',') : null });
  }

  // Infosys "Reset kolonner": back to id and name, and the column list closes
  public resetColumns(): void {
    this.showColumns.set(false);
    this.query.update({ columns: null });
  }

  public resetSearch(): void {
    this.query.update({ search: null });
  }

  public toggleSearch(): void {
    this.showSearch.update(open => !open);
    if (this.categories().length > 0) return;
    this.http.get<ParticipantOptions>('/api/participants/options').subscribe({
      next: options => {
        this.categories.set(options.categories);
        this.organizerAreas.set(sortByDanish(options.organizerAreas, a => a.nameDanish));
      },
      error: err => console.error('Failed to load participant options:', err),
    });
  }

  // Infosys search box: a new search replaces the searches so far; the others are combined with their result. The
  // columns of the fields searched on are added to the list.
  public search(step: CriteriaSearchStep): void {
    const current = this.steps() ?? [];
    const steps = step.combination === 'New' || current.length === 0 ? [step] : [...current, step];
    const columns = [...new Set([...this.columns(), ...columnsUsedBy(step.criteria)])];
    this.showSearch.set(false);
    this.query.update({ search: serializeSearch(steps), columns: columns.length ? columns.join(',') : null });
  }

  public setFilter(filter: string): void {
    this.query.update({ filter: filter || null });
  }

  public sortBy(sort: Sort): void {
    this.query.update({ sort: sort.active, dir: sort.direction || null });
  }

  public setPageSize(size: number): void {
    this.pageSize.set(size);
    this.page.set(0);
  }

  public goTo(page: number): void {
    this.page.set(Math.max(0, Math.min(page, this.pageCount() - 1)));
  }
}
