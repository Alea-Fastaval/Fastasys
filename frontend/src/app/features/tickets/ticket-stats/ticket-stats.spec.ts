import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { describe, expect, it } from 'vitest';
import { TicketStatsGroup } from '@shared/types';
import { TicketStatsComponent } from './ticket-stats';

describe('TicketStatsComponent', () => {
  it('shows a heading per ticket type name with each day and how many have it', async () => {
    await TestBed.configureTestingModule({
      imports: [TicketStatsComponent],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    }).compileComponents();
    const http = TestBed.inject(HttpTestingController);
    const fixture = TestBed.createComponent(TicketStatsComponent);
    fixture.detectChanges();
    http.match('/assets/i18n/en.json').forEach(request => request.flush({}));
    const groups: TicketStatsGroup[] = [
      {
        name: 'Indgang - Enkelt',
        kind: 'Entrance',
        isAllConvention: false,
        days: [
          { day: '2027-03-25', count: 12, isAllConvention: false },
          { day: '2027-03-24', count: 3, isAllConvention: false },
        ],
      },
      {
        name: 'Campingvogn',
        kind: 'Other',
        isAllConvention: false,
        days: [{ day: '2027-03-24', count: 1, isAllConvention: false }],
      },
      {
        name: 'Hele Fastaval',
        kind: 'Entrance',
        isAllConvention: true,
        days: [{ day: '2027-03-24', count: 40, isAllConvention: true }],
      },
    ];
    http.expectOne('/api/ticket-types/stats').flush(groups);
    fixture.detectChanges();
    const element = fixture.nativeElement as HTMLElement;

    const titles = [...element.querySelectorAll('.atom-card-title')].map(title => title.textContent?.trim());
    expect(titles).toEqual(['tickets.stats.entranceDay', 'Campingvogn', 'tickets.stats.entrancePartout']);
    const days = [...element.querySelectorAll('.days li')].map(li => li.textContent?.replace(/\s+/g, ' ').trim());
    expect(days).toEqual([
      'common.weekdaysShort.Thursday, 25 — 12',
      'common.weekdaysShort.Wednesday, 24 — 3',
      'common.weekdaysShort.Wednesday, 24 — 1',
      // A partout type has no day of its own (2026-11-25)
      'tickets.partout — 40',
    ]);
    http.verify();
  });
});
