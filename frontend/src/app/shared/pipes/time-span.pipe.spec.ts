import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { describe, beforeEach, afterEach, it, expect } from 'vitest';
import { TimeSpanPipe } from './time-span.pipe';

describe('TimeSpanPipe', () => {
  let pipe: TimeSpanPipe;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    globalThis.localStorage?.removeItem('fastasys_lang');
    TestBed.configureTestingModule({
      providers: [TimeSpanPipe, provideHttpClient(), provideHttpClientTesting()],
    });

    pipe = TestBed.inject(TimeSpanPipe);
    httpMock = TestBed.inject(HttpTestingController);
    httpMock.expectOne('/assets/i18n/en.json').flush({
      common: { weekdays: { Wednesday: 'Onsdag', Thursday: 'Torsdag' } },
    });
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('shows a run like Infosys: day and start, then end time', () => {
    expect(pipe.transform('2027-03-24T09:00:00Z', '2027-03-24T13:00:00Z')).toBe('Onsdag 10:00-14:00');
  });

  it('shows a schedule like the Infosys activity page: day on both ends', () => {
    expect(pipe.transform('2027-03-24T21:00:00Z', '2027-03-25T01:00:00Z', true)).toBe('Onsdag 22:00-Torsdag 02:00');
  });
});
