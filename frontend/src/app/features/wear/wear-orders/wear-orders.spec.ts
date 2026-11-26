import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ActivatedRoute, provideRouter } from '@angular/router';
import { afterEach, describe, expect, it } from 'vitest';
import { WearOrderListParticipant } from '@shared/types';
import { AuthService } from '@shared/services';
import { WearOrdersComponent } from './wear-orders';

const participants: WearOrderListParticipant[] = [
  {
    participantId: 1,
    name: 'Kaptajnen',
    categoryName: 'Arrangør',
    orders: [
      { id: 5, name: 'T-shirt-Sort', priceCategoryName: 'Arrangør', amount: 2, size: 'M', isReceived: false },
      { id: 6, name: 'Kasket', priceCategoryName: 'Deltager', amount: 1, size: null, isReceived: true },
    ],
  },
];

describe('WearOrdersComponent (Infosys wear/detailed and wear/unfilled)', () => {
  let http: HttpTestingController;

  afterEach(() => http.verify());

  const render = async (privileges: string[], unfilled: boolean) => {
    await TestBed.configureTestingModule({
      imports: [WearOrdersComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        { provide: ActivatedRoute, useValue: { snapshot: { data: unfilled ? { unfilled: true } : {} } } },
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
    const fixture = TestBed.createComponent(WearOrdersComponent);
    fixture.detectChanges();
    http.match('/assets/i18n/en.json').forEach(request => request.flush({}));
    http.expectOne(`/api/wear-orders?unfilled=${unfilled}`).flush(participants);
    fixture.detectChanges();
    return { fixture, element: fixture.nativeElement as HTMLElement };
  };

  it('shows each participant with their orders', async () => {
    const { element } = await render(['wear_list_view'], false);

    expect(element.querySelector('h1')?.textContent).toBe('wear.orders.title');
    expect(element.querySelector('.participant')?.textContent?.replace(/\s+/g, ' ').trim()).toBe(
      'wear.orders.id: 1 — wear.orders.name: Kaptajnen — Arrangør',
    );
    expect(element.querySelector('.participant a')?.getAttribute('href')).toBe('/participants/1');
    const rows = [...element.querySelectorAll('.orders tr')].map(row =>
      [...row.querySelectorAll('td')].map(cell => cell.textContent?.replace(/\s+/g, ' ').trim()),
    );
    expect(rows).toEqual([
      [
        'wear.orders.kind: T-shirt-Sort',
        'Arrangør',
        'wear.orders.amount: 2',
        'wear.orders.size: M',
        'wear.orders.status: wear.orders.notReceived',
      ],
      ['wear.orders.kind: Kasket', 'Deltager', 'wear.orders.amount: 1', '', 'wear.orders.status: wear.orders.received'],
    ]);
    // Marking takes wear_distribute_edit
    expect(element.querySelector('.hand-out')).toBeNull();
  });

  it('marks an order handed out by its id (Infosys flipped by participant and price)', async () => {
    const { fixture, element } = await render(['wear_distribute_edit'], true);

    expect(element.querySelector('h1')?.textContent).toBe('wear.orders.unfilledTitle');
    element.querySelector<HTMLButtonElement>('.hand-out')?.click();
    const request = http.expectOne('/api/wear-orders/5/received');
    expect(request.request.body).toEqual({ isReceived: true });
    request.flush(null);
    fixture.detectChanges();

    expect(element.querySelector('.hand-out')?.textContent?.trim()).toBe('wear.orders.undoReceived');
  });

  it('says "Kunne ikke ændre markeringen" when marking fails', async () => {
    const { fixture, element } = await render(['wear_distribute_edit'], false);

    element.querySelector<HTMLButtonElement>('.hand-out')?.click();
    http.expectOne('/api/wear-orders/5/received').flush({}, { status: 500, statusText: 'Server error' });
    fixture.detectChanges();

    expect(element.querySelector('.mark-failed')).not.toBeNull();
    expect(element.querySelector('.hand-out')?.textContent?.trim()).toBe('wear.orders.markReceived');
  });
});
