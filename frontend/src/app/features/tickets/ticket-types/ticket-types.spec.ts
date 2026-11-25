import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { TicketType } from '@shared/types';
import { AuthService } from '@shared/services';
import { TicketFlashService } from '../ticket-flash.service';
import { TicketTypesComponent } from './ticket-types';

const type = (overrides: Partial<TicketType>): TicketType => ({
  id: 1,
  name: 'Indgang - Partout',
  price: 300,
  day: '2027-03-24',
  kind: 'Entrance',
  isAllConvention: true,
  soldCount: 0,
  ...overrides,
});

describe('TicketTypesComponent', () => {
  let http: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [TicketTypesComponent],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    }).compileComponents();
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  const render = (privileges: string[], types: TicketType[] | null) => {
    TestBed.inject(AuthService).currentUser.set({
      id: 1,
      username: 'info',
      email: '',
      firstName: '',
      lastName: '',
      roles: [],
      privileges,
    });
    const fixture = TestBed.createComponent(TicketTypesComponent);
    fixture.detectChanges();
    http.match('/assets/i18n/en.json').forEach(request => request.flush({}));
    const request = http.expectOne('/api/ticket-types');
    if (types) request.flush(types);
    else request.flush({}, { status: 500, statusText: 'Server error' });
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  };

  it('lists the types in the order the API sorted them, with price and number sold', () => {
    const element = render(
      ['entrance_view'],
      [type({ id: 3, name: 'Indgang - Enkelt', day: '2027-03-25', price: 75, soldCount: 12 }), type({ soldCount: 4 })],
    );

    const rows = [...element.querySelectorAll('tbody tr')].map(row =>
      [...row.querySelectorAll('td')].map(cell => cell.textContent?.trim()),
    );
    expect(rows).toEqual([
      ['3', 'Indgang - Enkelt - common.weekdaysShort.Thursday 25/03', '75 DKK', '12'],
      ['1', 'Indgang - Partout - common.weekdaysShort.Wednesday 24/03', '300 DKK', '4'],
    ]);
    expect(element.querySelector('tbody a')?.getAttribute('href')).toBe('/resources/tickets/3');
    // Only entrance_edit may create
    expect(element.querySelector('a[href="/resources/tickets/new"]')).toBeNull();
    expect(element.querySelector('a[href="/resources/tickets/stats"]')).not.toBeNull();
  });

  it('offers "Opret indgangs-type" to entrance_edit', () => {
    const element = render(['entrance_edit'], [type({})]);

    expect(element.querySelector('a[href="/resources/tickets/new"]')).not.toBeNull();
  });

  it('says so when there are no types, like Infosys', () => {
    const element = render(['entrance_view'], []);

    expect(element.textContent).toContain('tickets.messages.notFound');
    expect(element.querySelector('table')).toBeNull();
  });

  it('shows the message from the page before', () => {
    TestBed.inject(TicketFlashService).set('success', 'deleted');
    const element = render(['entrance_edit'], [type({})]);

    expect(element.querySelector('.flash')?.textContent).toContain('tickets.messages.deleted');
  });

  it('says when the types could not be loaded', () => {
    const element = render(['entrance_view'], null);

    expect(element.textContent).toContain('tickets.messages.loadFailed');
  });
});
