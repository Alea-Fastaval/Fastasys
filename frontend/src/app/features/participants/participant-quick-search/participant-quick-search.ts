import { Component, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { ParticipantSearchResult, ParticipantSearchStep } from '@shared/types';
import { TranslatePipe } from '@shared/pipes';
import { serializeSearch, toApiSteps } from '../participant-list';

// Infosys's search field in the page header (IndexController::main). An id or a scanned badge opens the
// participant; so does a search that finds only one. Otherwise the participants found are listed.
@Component({
  selector: 'app-participant-quick-search',
  imports: [FormsModule, TranslatePipe],
  templateUrl: './participant-quick-search.html',
  styleUrl: './participant-quick-search.scss',
})
export class ParticipantQuickSearchComponent {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);

  public readonly term = signal('');

  public submit(): void {
    const term = this.term();
    // PHP empty(): nothing is searched for an empty field or "0"
    if (term === '' || term === '0') return;

    const steps: ParticipantSearchStep[] = [{ quick: term }];
    const showList = (): void => {
      this.router.navigate(['/participants'], { queryParams: { search: serializeSearch(steps) } });
    };
    this.http
      .post<ParticipantSearchResult>('/api/participants/search', { steps: toApiSteps(steps), columns: [] })
      .subscribe({
        next: result => {
          this.term.set('');
          if (result.directId !== null) {
            this.router.navigate(['/participants', result.directId]);
          } else {
            showList();
          }
        },
        error: () => showList(),
      });
  }
}
