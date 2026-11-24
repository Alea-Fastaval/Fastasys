import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ActivatedRoute, Router, convertToParamMap, provideRouter } from '@angular/router';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { TicketType } from '@shared/types';
import { TicketFlashService } from '../ticket-flash.service';
import { TicketTypeFormComponent, canSubmitTicketType, newTicketTypeForm, toDto } from './ticket-type-form';

const complete = {
  ...newTicketTypeForm(),
  name: 'Indgang - Enkelt',
  price: 75,
  day: '2027-03-25',
  kind: 'Entrance' as const,
};

const existing: TicketType = {
  id: 4,
  name: 'Overnatning - Enkelt',
  price: 75,
  day: '2027-03-24',
  kind: 'Sleeping',
  isAllConvention: false,
  isDigital: false,
  soldCount: 3,
};

describe('canSubmitTicketType (the rules the API checks)', () => {
  it('needs a type, a whole price of 0 or more, a day and a kind', () => {
    expect(canSubmitTicketType(newTicketTypeForm())).toBe(false);
    expect(canSubmitTicketType(complete)).toBe(true);
    expect(canSubmitTicketType({ ...complete, price: 0 })).toBe(true);
    expect(canSubmitTicketType({ ...complete, price: -1 })).toBe(false);
    expect(canSubmitTicketType({ ...complete, price: 7.5 })).toBe(false);
    expect(canSubmitTicketType({ ...complete, price: null })).toBe(false);
    expect(canSubmitTicketType({ ...complete, day: '' })).toBe(false);
    expect(canSubmitTicketType({ ...complete, kind: null })).toBe(false);
  });

  it('refuses the types PHP empty() refuses, and more than 64 characters', () => {
    expect(canSubmitTicketType({ ...complete, name: '' })).toBe(false);
    expect(canSubmitTicketType({ ...complete, name: '0' })).toBe(false);
    expect(canSubmitTicketType({ ...complete, name: 'x'.repeat(64) })).toBe(true);
    expect(canSubmitTicketType({ ...complete, name: 'x'.repeat(65) })).toBe(false);
  });

  it('sends no day when none is chosen', () => {
    expect(toDto({ ...complete, day: '' }).day).toBeNull();
    expect(toDto(complete)).toEqual({
      name: 'Indgang - Enkelt',
      price: 75,
      day: '2027-03-25',
      kind: 'Entrance',
      isAllConvention: false,
      isDigital: false,
    });
  });
});

describe('TicketTypeFormComponent', () => {
  let http: HttpTestingController;

  afterEach(() => http.verify());

  const render = async (id: string | null) => {
    await TestBed.configureTestingModule({
      imports: [TicketTypeFormComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: convertToParamMap(id ? { id } : {}) } } },
      ],
    }).compileComponents();
    http = TestBed.inject(HttpTestingController);
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
    const fixture = TestBed.createComponent(TicketTypeFormComponent);
    fixture.detectChanges();
    http.match('/assets/i18n/en.json').forEach(request => request.flush({}));
    http.expectOne('/api/signup/status').flush({ conventionStart: '2027-03-24', conventionEnd: '2027-03-29' });
    if (id) http.expectOne(`/api/ticket-types/${id}`).flush(existing);
    fixture.detectChanges();
    return { fixture, component: fixture.componentInstance, navigate, flash: TestBed.inject(TicketFlashService) };
  };

  it('creates a type and opens it with "Indgangs-typen blev oprettet."', async () => {
    const { fixture, component, navigate, flash } = await render(null);
    const element = fixture.nativeElement as HTMLElement;
    // Like Infosys's date picker: the convention's days
    const day = element.querySelector<HTMLInputElement>('input[name="day"]');
    expect(day?.getAttribute('min')).toBe('2027-03-24');
    expect(day?.getAttribute('max')).toBe('2027-03-29');
    // The create form has no Fortryd or Slet
    expect(element.querySelectorAll('.form-actions button').length).toBe(1);

    component.form.set({ ...complete, isAllConvention: true });
    component.submit();
    const request = http.expectOne('/api/ticket-types');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ ...toDto(complete), isAllConvention: true });
    request.flush({ ...existing, id: 9 });

    expect(flash.take()).toEqual({ type: 'success', key: 'created' });
    expect(navigate).toHaveBeenCalledWith(['/resources/tickets', 9]);
  });

  it('keeps what was typed when creating fails', async () => {
    const { component } = await render(null);
    component.form.set(complete);
    component.submit();
    http.expectOne('/api/ticket-types').flush({}, { status: 400, statusText: 'Bad request' });

    expect(component.errorKey()).toBe('createFailed');
    expect(component.form()).toEqual(complete);
    expect(component.busy()).toBe(false);
  });

  it('updates a type and goes back to it with "Indgangs-typen blev opdateret."', async () => {
    const { component, navigate, flash } = await render('4');
    expect(component.form()?.name).toBe('Overnatning - Enkelt');

    component.form.update(form => form && { ...form, price: 80 });
    component.submit();
    const request = http.expectOne('/api/ticket-types/4');
    expect(request.request.method).toBe('PUT');
    expect(request.request.body.price).toBe(80);
    request.flush({ ...existing, price: 80 });

    expect(flash.take()).toEqual({ type: 'success', key: 'updated' });
    expect(navigate).toHaveBeenCalledWith(['/resources/tickets', 4]);
  });

  it('"Fortryd" goes back to the type with "indgangs-typen blev ikke opdateret.", like Infosys', async () => {
    const { component, navigate, flash } = await render('4');

    component.cancel();

    expect(flash.take()).toEqual({ type: 'error', key: 'notUpdated' });
    expect(navigate).toHaveBeenCalledWith(['/resources/tickets', 4]);
  });

  it('asks before deleting, and goes to all types afterwards', async () => {
    const { fixture, component, navigate, flash } = await render('4');
    component.askDelete();
    fixture.detectChanges();
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('tickets.deleteConfirmTitle');

    component.confirmDelete();
    const request = http.expectOne('/api/ticket-types/4');
    expect(request.request.method).toBe('DELETE');
    request.flush({});

    expect(flash.take()).toEqual({ type: 'success', key: 'deleted' });
    expect(navigate).toHaveBeenCalledWith(['/resources/tickets']);
  });

  it('goes back to the type with "Kunne ikke slette indgangs-typen" when participants have it', async () => {
    const { component, navigate, flash } = await render('4');
    component.confirmDelete();
    http.expectOne('/api/ticket-types/4').flush({ reason: 'inUse' }, { status: 409, statusText: 'Conflict' });

    expect(flash.take()).toEqual({ type: 'error', key: 'deleteFailed' });
    expect(navigate).toHaveBeenCalledWith(['/resources/tickets', 4]);
  });
});
