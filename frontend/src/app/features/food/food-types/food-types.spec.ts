import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { FoodTypeSummary } from '@shared/types';
import { AuthService } from '@shared/services';
import { FoodFlashService } from '../food-flash.service';
import { FoodTypesComponent } from './food-types';

describe('FoodTypesComponent', () => {
  let http: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [FoodTypesComponent],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    }).compileComponents();
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  const render = (privileges: string[], types: FoodTypeSummary[] | null) => {
    TestBed.inject(AuthService).currentUser.set({
      id: 1,
      username: 'info',
      email: '',
      firstName: '',
      lastName: '',
      roles: [],
      privileges,
    });
    const fixture = TestBed.createComponent(FoodTypesComponent);
    fixture.detectChanges();
    http.match('/assets/i18n/en.json').forEach(request => request.flush({}));
    const request = http.expectOne('/api/food-types');
    if (types) request.flush(types);
    else request.flush({}, { status: 500, statusText: 'Server error' });
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  };

  it('lists Id, Skjult, Kategori and Pris (Infosys showtypes.phtml)', () => {
    const element = render(
      ['food_view'],
      [
        { id: 1, nameDanish: 'Aftensmad', nameEnglish: 'Dinner', price: 68, isHidden: false },
        { id: 4, nameDanish: 'Arrangørmad', nameEnglish: '', price: 0, isHidden: true },
      ],
    );

    const rows = [...element.querySelectorAll('tbody tr')].map(row =>
      [...row.querySelectorAll('td')].map(cell => cell.textContent?.trim()),
    );
    expect(rows).toEqual([
      ['1', 'common.no', 'Aftensmad', '68 DKK'],
      ['4', 'common.yes', 'Arrangørmad', '0 DKK'],
    ]);
    expect(element.querySelector('tbody a')?.getAttribute('href')).toBe('/resources/food/1');
    // Only food_edit may create; food_view may see the statistics
    expect(element.querySelector('a[href="/resources/food/new"]')).toBeNull();
    expect(element.querySelector('a[href="/resources/food/stats"]')).not.toBeNull();
  });

  it('offers "Opret mad-type" to food_edit', () => {
    const element = render(['food_edit'], []);

    expect(element.querySelector('a[href="/resources/food/new"]')).not.toBeNull();
    // Infosys's noResults page
    expect(element.textContent).toContain('food.messages.notFound');
  });

  it('shows the message from the page before, and says when loading fails', () => {
    TestBed.inject(FoodFlashService).set('success', 'deleted');
    const element = render(['food_view'], null);

    expect(element.textContent).toContain('food.messages.deleted');
    expect(element.textContent).toContain('food.messages.loadFailed');
  });
});
