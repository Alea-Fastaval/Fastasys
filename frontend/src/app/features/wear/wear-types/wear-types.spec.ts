import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { WearTypeSummary } from '@shared/types';
import { AuthService } from '@shared/services';
import { WearFlashService } from '../wear-flash.service';
import { WearTypesComponent } from './wear-types';

const types: WearTypeSummary[] = [
  { id: 2, nameDanish: 'Kasket', descriptionDanish: 'Sort kasket', position: 1 },
  { id: 1, nameDanish: 'T-shirt', descriptionDanish: '', position: 2 },
];

describe('WearTypesComponent (Infosys wear/showwear)', () => {
  let http: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [WearTypesComponent],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    }).compileComponents();
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  const render = (privileges: string[], list: WearTypeSummary[] | null) => {
    TestBed.inject(AuthService).currentUser.set({
      id: 1,
      username: 'wear',
      email: '',
      firstName: '',
      lastName: '',
      roles: [],
      privileges,
    });
    const fixture = TestBed.createComponent(WearTypesComponent);
    fixture.detectChanges();
    http.match('/assets/i18n/en.json').forEach(request => request.flush({}));
    const request = http.expectOne('/api/wear-types');
    if (list) request.flush(list);
    else request.flush({}, { status: 500, statusText: 'Server error' });
    fixture.detectChanges();
    return { fixture, element: fixture.nativeElement as HTMLElement };
  };

  it('lists Id, Navn and Beskrivelse by position, without the order buttons for wear_view', () => {
    const { element } = render(['wear_view'], types);

    const rows = [...element.querySelectorAll('tbody tr')].map(row =>
      [...row.querySelectorAll('td')].map(cell => cell.textContent?.trim()),
    );
    expect(rows).toEqual([
      ['2', 'Kasket', 'Sort kasket'],
      ['1', 'T-shirt', ''],
    ]);
    expect(element.querySelector('tbody a')?.getAttribute('href')).toBe('/resources/wear/types/2');
    expect(element.querySelector('.move-up')).toBeNull();
    expect(element.querySelector('a[href="/resources/wear/types/new"]')).toBeNull();
  });

  it('moves a type with the arrows and reloads the list (wearlist.js switch_row)', () => {
    const { fixture, element } = render(['wear_edit'], types);

    const up = element.querySelectorAll<HTMLButtonElement>('.move-up');
    const down = element.querySelectorAll<HTMLButtonElement>('.move-down');
    // The first type can't go up and the last can't go down
    expect(up[0].disabled).toBe(true);
    expect(down[1].disabled).toBe(true);
    down[0].click();

    const move = http.expectOne('/api/wear-types/2/position');
    expect(move.request.method).toBe('PUT');
    expect(move.request.body).toEqual({ direction: 'Down' });
    move.flush(null);
    http.expectOne('/api/wear-types').flush([types[1], types[0]]);
    fixture.detectChanges();

    expect(element.querySelector('tbody td')?.textContent?.trim()).toBe('1');
  });

  it('says when a move fails', () => {
    const { fixture } = render(['wear_edit'], types);

    fixture.componentInstance.move(types[1], 'Up');
    http.expectOne('/api/wear-types/1/position').flush({}, { status: 500, statusText: 'Server error' });
    fixture.detectChanges();

    expect((fixture.nativeElement as HTMLElement).querySelector('.move-failed')).not.toBeNull();
  });

  it('shows the message from the page before, and says when loading fails', () => {
    TestBed.inject(WearFlashService).set('error', 'inUse', { count: '3' });
    const { element } = render(['wear_view'], null);

    expect(element.textContent).toContain('wear.messages.inUse');
    expect(element.textContent).toContain('wear.messages.loadFailed');
  });

  it("shows Infosys's noResults page when there are no types", () => {
    const { element } = render(['wear_edit'], []);

    expect(element.textContent).toContain('wear.messages.notFound');
    expect(element.querySelector('a[href="/resources/wear/types/new"]')).not.toBeNull();
  });
});
