import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { describe, expect, it } from 'vitest';
import { FoodStats } from '@shared/types';
import { FoodStatsComponent } from './food-stats';

describe('FoodStatsComponent', () => {
  it('shows each food type with its total and each meal time with how many were handed out', async () => {
    await TestBed.configureTestingModule({
      imports: [FoodStatsComponent],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    }).compileComponents();
    const http = TestBed.inject(HttpTestingController);
    const fixture = TestBed.createComponent(FoodStatsComponent);
    fixture.detectChanges();
    http.match('/assets/i18n/en.json').forEach(request => request.flush({}));
    const stats: FoodStats[] = [
      {
        id: 1,
        nameDanish: 'Aftensmad',
        total: 3,
        mealTimes: [
          { id: 10, time: '2027-03-24T16:00:00Z', total: 3, received: 1 },
          { id: 11, time: '2027-03-25T16:00:00Z', total: 0, received: 0 },
        ],
      },
    ];
    http.expectOne('/api/food-types/stats').flush(stats);
    fixture.detectChanges();
    const element = fixture.nativeElement as HTMLElement;

    expect(element.querySelector('.atom-card-title')?.textContent?.trim()).toBe('Aftensmad');
    expect(element.querySelector('.total')?.textContent?.trim()).toBe('food.stats.total');
    const rows = [...element.querySelectorAll('.meal-times li')].map(li => li.textContent?.replace(/\s+/g, ' ').trim());
    // Without translations the key is shown; "heraf" is left out when nobody ordered it (foodstats.phtml)
    expect(rows).toEqual([
      'common.weekdaysShort.Wednesday, 24 — 3 - food.stats.received',
      'common.weekdaysShort.Thursday, 25 — 0',
    ]);
    http.verify();
  });
});
