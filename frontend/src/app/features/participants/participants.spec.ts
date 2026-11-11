import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { describe, afterEach, it, expect } from 'vitest';
import { ParticipantSummary } from '@shared/types';
import { ParticipantFlashService } from './participant-flash.service';
import { ParticipantsComponent } from './participants';

describe('ParticipantsComponent (Infosys deltager/visalle)', () => {
  let httpMock: HttpTestingController;

  afterEach(() => {
    httpMock.verify();
  });

  const setup = async () => {
    await TestBed.configureTestingModule({
      imports: [ParticipantsComponent],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    }).compileComponents();

    httpMock = TestBed.inject(HttpTestingController);
    const flash = TestBed.inject(ParticipantFlashService);
    return { flash };
  };

  const render = () => {
    const fixture = TestBed.createComponent(ParticipantsComponent);
    fixture.detectChanges();
    httpMock.match('/assets/i18n/en.json').forEach(req => req.flush({}));
    return fixture;
  };

  const list: ParticipantSummary[] = [
    { id: 1, name: 'Testa Testesen' },
    { id: 4, name: 'Prøve Person' },
  ];

  it('lists every participant with links to their page', async () => {
    await setup();
    const fixture = render();

    httpMock.expectOne('/api/participants').flush(list);
    fixture.detectChanges();

    const rows = (fixture.nativeElement as HTMLElement).querySelectorAll('tbody tr');
    expect(rows.length).toBe(2);
    const links = rows[1].querySelectorAll('a');
    expect(links[0].textContent?.trim()).toBe('4');
    expect(links[1].textContent?.trim()).toBe('Prøve Person');
    expect(links[1].getAttribute('href')).toBe('/participants/4');
  });

  it('shows the message from the page it came from', async () => {
    const { flash } = await setup();
    flash.set('error', 'createFailed');
    const fixture = render();
    httpMock.expectOne('/api/participants').flush([]);
    fixture.detectChanges();

    expect(fixture.componentInstance.flash).toEqual({ type: 'error', key: 'createFailed' });
    expect((fixture.nativeElement as HTMLElement).querySelector('.flash.error')).not.toBeNull();
  });
});
