import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { afterEach, describe, expect, it } from 'vitest';
import { WearTypeDetail } from '@shared/types';
import { AuthService } from '@shared/services';
import { WearTypeDetailComponent } from './wear-type-detail';

const shirt: WearTypeDetail = {
  id: 1,
  nameDanish: 'T-shirt',
  nameEnglish: 'T-shirt',
  descriptionDanish: 'Bomuld',
  descriptionEnglish: 'Cotton',
  maxOrder: 2,
  isRequired: true,
  variants: [
    [
      { id: 101, kind: 'Model', nameDanish: 'Herre', nameEnglish: 'Men', position: 1 },
      { id: 111, kind: 'Color', nameDanish: 'Sort', nameEnglish: 'Black', position: 1 },
      { id: 12, kind: 'Size', nameDanish: 'S', nameEnglish: 'S', position: 12 },
      { id: 13, kind: 'Size', nameDanish: 'M', nameEnglish: 'M', position: 13 },
    ],
  ],
  prices: [
    { id: 4, participantCategoryId: 1, categoryName: 'Deltager', price: 150, orderCount: 2 },
    { id: 5, participantCategoryId: 2, categoryName: 'Arrangør', price: 100, orderCount: 0 },
  ],
  imageLinks: [],
  orderCount: 2,
};

describe('WearTypeDetailComponent (Infosys wear/viswear/:id:)', () => {
  let http: HttpTestingController;

  afterEach(() => http.verify());

  const render = async (privileges: string[], wear: WearTypeDetail | null) => {
    await TestBed.configureTestingModule({
      imports: [WearTypeDetailComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: convertToParamMap({ id: '1' }) } } },
      ],
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
    const fixture = TestBed.createComponent(WearTypeDetailComponent);
    fixture.detectChanges();
    http.match('/assets/i18n/en.json').forEach(request => request.flush({}));
    const request = http.expectOne('/api/wear-types/1');
    if (wear) request.flush(wear);
    else request.flush({}, { status: 404, statusText: 'Not found' });
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  };

  it('shows the fields, the variants by kind and the prices', async () => {
    const element = await render(['wear_view'], shirt);

    expect(element.querySelector('.name-english')?.textContent).toBe('T-shirt');
    expect(element.querySelector('.description-danish')?.textContent).toBe('Bomuld');
    expect(element.querySelector('.max-order')?.textContent).toBe('2');
    expect(element.querySelector('.is-required')?.textContent).toBe('common.yes');
    const kinds = [...element.querySelectorAll('.variant .kind')].map(kind => [
      kind.querySelector('h3')?.textContent,
      [...kind.querySelectorAll('.attribute')].map(a => a.textContent),
    ]);
    expect(kinds).toEqual([
      ['wear.kinds.Model', ['Herre']],
      ['wear.kinds.Color', ['Sort']],
      ['wear.kinds.Size', ['S', 'M']],
    ]);
    const prices = [...element.querySelectorAll('.prices tbody tr')].map(row =>
      [...row.querySelectorAll('td')].map(cell => cell.textContent),
    );
    expect(prices).toEqual([
      ['4', 'Deltager', '150 DKK'],
      ['5', 'Arrangør', '100 DKK'],
    ]);
    // Only wear_edit may change it
    expect(element.querySelector('a[href="/resources/wear/types/1/edit"]')).toBeNull();
  });

  it('links "ret/slet" for wear_edit', async () => {
    const element = await render(['wear_edit'], shirt);

    expect(element.querySelector('a[href="/resources/wear/types/1/edit"]')).not.toBeNull();
  });

  it("shows Infosys's noResults text for an unknown type", async () => {
    const element = await render(['wear_view'], null);

    expect(element.textContent).toContain('wear.messages.notFound');
  });
});
