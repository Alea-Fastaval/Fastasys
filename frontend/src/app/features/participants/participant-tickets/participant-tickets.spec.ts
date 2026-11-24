import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ActivatedRoute, Router, convertToParamMap, provideRouter } from '@angular/router';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { ParticipantTickets } from '@shared/types';
import { ParticipantFlashService } from '../participant-flash.service';
import { ParticipantTicketsComponent, addTicket } from './participant-tickets';

const partout = { id: 1, name: 'Indgang - Partout', day: '2027-03-24', isAllConvention: true };
const thursday = { id: 2, name: 'Indgang - Enkelt', day: '2027-03-25', isAllConvention: false };
const party = { id: 3, name: 'Ottofest', day: '2027-03-27', isAllConvention: false };

describe('addTicket (imwedit.js setupIndgang)', () => {
  it('adds the chosen type unless it is already listed', () => {
    expect(addTicket([partout], thursday)).toEqual([partout, thursday]);
    expect(addTicket([partout], partout)).toEqual([partout]);
    expect(addTicket([partout], undefined)).toEqual([partout]);
  });
});

describe('ParticipantTicketsComponent', () => {
  let http: HttpTestingController;

  afterEach(() => http.verify());

  const render = async (page: ParticipantTickets | null) => {
    await TestBed.configureTestingModule({
      imports: [ParticipantTicketsComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: convertToParamMap({ id: '7' }) } } },
      ],
    }).compileComponents();
    http = TestBed.inject(HttpTestingController);
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
    const fixture = TestBed.createComponent(ParticipantTicketsComponent);
    fixture.detectChanges();
    http.match('/assets/i18n/en.json').forEach(request => request.flush({}));
    const request = http.expectOne('/api/participants/7/tickets');
    if (page) request.flush(page);
    else request.flush({}, { status: 404, statusText: 'Not found' });
    fixture.detectChanges();
    return { fixture, component: fixture.componentInstance, navigate, flash: TestBed.inject(ParticipantFlashService) };
  };

  const page: ParticipantTickets = { participantId: 7, tickets: [partout], ticketTypes: [partout, thursday, party] };

  it('lists the tickets as "name (day)" and starts the select on the first type', async () => {
    const { fixture, component } = await render(page);

    const rows = [...(fixture.nativeElement as HTMLElement).querySelectorAll('.ticket-list .ticket')].map(cell =>
      cell.textContent?.trim(),
    );
    expect(rows).toEqual(['Indgang - Partout (common.weekdaysShort.Wednesday)']);
    expect(component.chosenId).toBe(1);
  });

  it('adds and removes tickets, and "Opdater" saves the list', async () => {
    const { component, navigate, flash } = await render(page);

    component.chosenId = 3;
    component.add();
    component.chosenId = 1;
    component.add();
    component.remove(partout);
    component.submit();
    const request = http.expectOne('/api/participants/7/tickets');
    expect(request.request.method).toBe('PUT');
    expect(request.request.body).toEqual({ ticketTypeIds: [3] });
    request.flush(null);

    expect(flash.take()).toEqual({ type: 'success', key: 'updated' });
    expect(navigate).toHaveBeenCalledWith(['/participants', 7]);
  });

  it('goes back to the participant with "Kunne ikke opdatere deltageren." when saving fails', async () => {
    const { component, navigate, flash } = await render(page);

    component.submit();
    http.expectOne('/api/participants/7/tickets').flush({}, { status: 500, statusText: 'Server error' });

    expect(flash.take()).toEqual({ type: 'error', key: 'ticketsUpdateFailed' });
    expect(navigate).toHaveBeenCalledWith(['/participants', 7]);
  });

  it('says so for an unknown participant', async () => {
    const { fixture } = await render(null);

    expect((fixture.nativeElement as HTMLElement).textContent).toContain('participants.messages.notFound');
  });
});
