import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { afterEach, describe, expect, it } from 'vitest';
import { WearBreakdown } from '@shared/types';
import { AuthService } from '@shared/services';
import { WearBreakdownComponent, breakdownCells } from './wear-breakdown';

const breakdown: WearBreakdown = {
  sizes: [
    { id: 12, kind: 'Size', nameDanish: 'S', nameEnglish: 'S', position: 12 },
    { id: 13, kind: 'Size', nameDanish: 'M', nameEnglish: 'M', position: 13 },
  ],
  rows: [
    {
      wearTypeId: 2,
      wearTypeName: 'Kasket',
      attributes: [],
      startsVariant: true,
      total: 4,
      noSize: 4,
      sizeCounts: [],
    },
    {
      wearTypeId: 1,
      wearTypeName: 'T-shirt',
      attributes: [{ id: 111, kind: 'Color', nameDanish: 'Sort', nameEnglish: 'Black', position: 1 }],
      startsVariant: true,
      total: 3,
      noSize: 0,
      sizeCounts: [{ sizeId: 13, count: 3 }],
    },
  ],
};

describe('breakdownCells', () => {
  it('gives each row "Ingen størrelse" and a cell per size', () => {
    expect(breakdownCells(breakdown.rows[1], breakdown)).toEqual([
      { sizeId: null, count: 0 },
      { sizeId: 12, count: 0 },
      { sizeId: 13, count: 3 },
    ]);
  });
});

describe('WearBreakdownComponent (Infosys wear/breakdown)', () => {
  let http: HttpTestingController;

  afterEach(() => http.verify());

  const render = async (privileges: string[]) => {
    await TestBed.configureTestingModule({
      imports: [WearBreakdownComponent],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    }).compileComponents();
    http = TestBed.inject(HttpTestingController);
    TestBed.inject(AuthService).currentUser.set({
      id: 1,
      username: 'wear',
      email: '',
      firstName: '',
      lastName: '',
      roles: [],
      privileges,
    });
    const fixture = TestBed.createComponent(WearBreakdownComponent);
    fixture.detectChanges();
    http.match('/assets/i18n/en.json').forEach(request => request.flush({}));
    http.expectOne('/api/wear-orders/breakdown').flush(breakdown);
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  };

  it('shows the rows with I alt and the sizes, linking the amounts to the orders behind them', async () => {
    const element = await render(['wear_list_view', 'wear_view']);

    const rows = [...element.querySelectorAll('tbody tr')].map(row =>
      [...row.querySelectorAll('td')].map(cell => cell.textContent?.trim()),
    );
    expect(rows).toEqual([
      ['(2) Kasket', '4', '4', '0', '0'],
      ['(1) T-shirt - Sort', '3', '0', '0', '3'],
    ]);
    const links = [...element.querySelectorAll('tbody a')].map(a => a.getAttribute('href'));
    expect(links).toEqual([
      '/resources/wear/types/2/orders?size=none',
      '/resources/wear/types/1/orders?size=13&attributes=111',
    ]);
  });

  it("doesn't link the amounts for users who can't see the lists", async () => {
    const element = await render(['wear_view']);

    expect(element.querySelectorAll('tbody a').length).toBe(0);
    expect(element.querySelector('tbody tr td.count')?.textContent?.trim()).toBe('4');
  });
});
