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
    const day = (id: number, date: string, count: number) => ({
      id,
      day: date,
      kind: 'Entrance' as const,
      isAllConvention: false,
      count,
    });
    const groups: TicketStatsGroup[] = [
      {
        name: 'Indgang - Enkelt',
        kind: 'Entrance',
        isAllConvention: false,
        types: [day(2, '2027-03-25', 12), day(4, '2027-03-24', 3)],
      },
      {
        name: 'Campingvogn',
        kind: 'Other',
        isAllConvention: false,
        types: [{ ...day(5, '2027-03-24', 1), kind: 'Other' }],
      },
      {
        name: 'Hele Fastaval',
        kind: 'Entrance',
        isAllConvention: true,
        types: [{ ...day(1, '2027-03-24', 40), isAllConvention: true }],
      },
      {
        name: 'Alea medlemskab',
        kind: 'AleaMembership',
        isAllConvention: true,
        types: [{ ...day(6, '2027-03-24', 5), kind: 'AleaMembership', isAllConvention: true }],
      },
    ];
    http.expectOne('/api/ticket-types/stats').flush(groups);
    fixture.detectChanges();
    const element = fixture.nativeElement as HTMLElement;

    // The heading is the name, with the kind under it (2026-11-25; Infosys showed only the kind, so the Alea
    // membership looked like "Indgang, partout")
    const titles = [...element.querySelectorAll('.atom-card-title')].map(title => title.textContent?.trim());
    expect(titles).toEqual(['Indgang - Enkelt', 'Campingvogn', 'Hele Fastaval', 'Alea medlemskab']);
    const kinds = [...element.querySelectorAll('.atom-card-subtitle')].map(subtitle => subtitle.textContent?.trim());
    expect(kinds).toEqual([
      'tickets.stats.entranceDay',
      'tickets.kinds.Other',
      'tickets.stats.entrancePartout',
      'tickets.kinds.AleaMembership',
    ]);
    const rows = [...element.querySelectorAll('.days li')].map(li => li.textContent?.replace(/\s+/g, ' ').trim());
    expect(rows).toEqual([
      'common.weekdaysShort.Thursday, 25 — 12',
      'common.weekdaysShort.Wednesday, 24 — 3',
      'common.weekdaysShort.Wednesday, 24 — 1',
      'tickets.partout — 40',
      'tickets.fields.soldCount — 5',
    ]);
    http.verify();
  });
});
