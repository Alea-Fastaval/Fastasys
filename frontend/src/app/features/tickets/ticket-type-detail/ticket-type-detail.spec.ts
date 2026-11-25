import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { describe, expect, it } from 'vitest';
import { TicketType } from '@shared/types';
import { AuthService } from '@shared/services';
import { TicketTypeDetailComponent } from './ticket-type-detail';

const type: TicketType = {
  id: 4,
  name: 'Indgang - Partout',
  price: 300,
  day: '2027-03-24',
  kind: 'Entrance',
  isAllConvention: true,
  soldCount: 7,
};

describe('TicketTypeDetailComponent', () => {
  const render = async (privileges: string[], found = true) => {
    await TestBed.configureTestingModule({
      imports: [TicketTypeDetailComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: convertToParamMap({ id: '4' }) } } },
      ],
    }).compileComponents();
    TestBed.inject(AuthService).currentUser.set({
      id: 1,
      username: 'info',
      email: '',
      firstName: '',
      lastName: '',
      roles: [],
      privileges,
    });
    const http = TestBed.inject(HttpTestingController);
    const fixture = TestBed.createComponent(TicketTypeDetailComponent);
    fixture.detectChanges();
    http.match('/assets/i18n/en.json').forEach(request => request.flush({}));
    const request = http.expectOne('/api/ticket-types/4');
    if (found) request.flush(type);
    else request.flush({}, { status: 404, statusText: 'Not found' });
    fixture.detectChanges();
    http.verify();
    return fixture.nativeElement as HTMLElement;
  };

  const text = (element: HTMLElement, selector: string) => element.querySelector(selector)?.textContent?.trim();

  it('shows the fields of the type', async () => {
    const element = await render(['entrance_view']);

    expect(text(element, '.id')).toBe('4');
    expect(text(element, '.name')).toBe('Indgang - Partout');
    expect(text(element, '.price')).toBe('300 DKK');
    expect(text(element, '.day')).toBe('common.weekdays.Wednesday 24/03');
    expect(text(element, '.kind')).toBe('tickets.kinds.Entrance');
    expect(text(element, '.all-convention')).toBe('common.yes');
    expect(element.querySelector('.digital')).toBeNull();
    expect(text(element, '.sold')).toBe('7');
    // "ret/slet" is for entrance_edit only
    expect(element.querySelector('a[href="/resources/tickets/4/edit"]')).toBeNull();
  });

  it('links "ret/slet" for entrance_edit', async () => {
    const element = await render(['entrance_edit']);

    expect(element.querySelector('a[href="/resources/tickets/4/edit"]')).not.toBeNull();
  });

  it('says so for an unknown type, like Infosys', async () => {
    const element = await render(['entrance_view'], false);

    expect(element.textContent).toContain('tickets.messages.notFound');
  });
});
