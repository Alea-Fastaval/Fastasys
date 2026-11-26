import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Router, provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { afterEach, describe, expect, it } from 'vitest';
import { WearMiniList } from '@shared/types';
import { UnknownQueryParamError } from '@shared/services';
import { WearMiniListComponent, miniListParams, parseAttributeIds, parseSize } from './wear-mini-list';

const list: WearMiniList = {
  wearTypeId: 1,
  wearTypeName: 'T-shirt',
  attributes: [
    { id: 111, kind: 'Color', nameDanish: 'Sort', nameEnglish: 'Black', position: 1 },
    { id: 13, kind: 'Size', nameDanish: 'M', nameEnglish: 'M', position: 13 },
  ],
  orders: [
    {
      participantId: 4,
      participantName: 'Kaptajnen',
      order: { id: 9, name: 'T-shirt-Sort', priceCategoryName: 'Deltager', amount: 2, size: 'M', isReceived: false },
    },
  ],
};

describe('the mini list query', () => {
  it('reads ?attributes= and ?size=, and refuses anything else', () => {
    expect(parseAttributeIds('101,111,101')).toEqual([101, 111]);
    expect(() => parseAttributeIds('red')).toThrow(UnknownQueryParamError);
    expect(parseSize('none')).toBe('none');
    expect(parseSize('13')).toBe(13);
    expect(() => parseSize('big')).toThrow(UnknownQueryParamError);
  });

  it('asks the API for the attributes with the size among them, or the orders without a size', () => {
    expect(miniListParams([111], 13).toString()).toBe('attributeIds=111&attributeIds=13');
    expect(miniListParams(null, 'none').toString()).toBe('noSize=true');
    expect(miniListParams(null, null).toString()).toBe('');
  });
});

describe('WearMiniListComponent (Infosys wear/detailed/:id:)', () => {
  let http: HttpTestingController;

  afterEach(() => http.verify());

  const open = async (url: string) => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([{ path: 'resources/wear/types/:id/orders', component: WearMiniListComponent }]),
      ],
    });
    http = TestBed.inject(HttpTestingController);
    const harness = await RouterTestingHarness.create();
    const component = await harness.navigateByUrl(url, WearMiniListComponent);
    http.match('/assets/i18n/en.json').forEach(request => request.flush({}));
    return { harness, component };
  };

  it('shows the orders behind a number', async () => {
    const { harness, component } = await open('/resources/wear/types/1/orders?attributes=111&size=13');

    http.expectOne('/api/wear-orders/by-type/1?attributeIds=111&attributeIds=13').flush(list);
    harness.detectChanges();
    const element = harness.routeNativeElement as HTMLElement;

    expect(component.heading(list)).toBe('T-shirt - Sort - M');
    expect(element.querySelector('.participant a')?.getAttribute('href')).toBe('/participants/4');
    expect(element.querySelector('.line')?.textContent?.replace(/\s+/g, ' ').trim()).toBe(
      'wear.orders.kind: T-shirt-Sort, Deltager wear.orders.amount: 2 wear.orders.size: M',
    );
  });

  it('removes an unknown query value and says so', async () => {
    const { harness } = await open('/resources/wear/types/1/orders?size=big');

    // The unknown size is left out of the first request and removed from the URL
    http.expectOne('/api/wear-orders/by-type/1').flush(list);
    harness.detectChanges();
    await harness.fixture.whenStable();
    http.match('/api/wear-orders/by-type/1').forEach(request => request.flush(list));
    harness.detectChanges();

    expect(TestBed.inject(Router).url).toBe('/resources/wear/types/1/orders');
    expect((harness.routeNativeElement as HTMLElement).querySelector('.unknown-query')).not.toBeNull();
  });
});
