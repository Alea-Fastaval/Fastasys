import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { afterEach, describe, expect, it } from 'vitest';
import { WearHandoutResult } from '@shared/types';
import { WearHandoutComponent } from './wear-handout';

const result: WearHandoutResult = {
  participantId: 4,
  name: 'Kaptajnen',
  categoryName: 'Deltager',
  items: [
    { name: 'T-shirt-Sort', amount: 2, size: 'M' },
    { name: 'Kasket', amount: 1, size: null },
  ],
};

describe('WearHandoutComponent (Infosys wear/handout)', () => {
  let http: HttpTestingController;

  afterEach(() => http.verify());

  const render = async () => {
    await TestBed.configureTestingModule({
      imports: [WearHandoutComponent],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    }).compileComponents();
    http = TestBed.inject(HttpTestingController);
    const fixture = TestBed.createComponent(WearHandoutComponent);
    fixture.detectChanges();
    http.match('/assets/i18n/en.json').forEach(request => request.flush({}));
    return { fixture, component: fixture.componentInstance, element: fixture.nativeElement as HTMLElement };
  };

  it('hands out the scanned participant\'s wear and offers "Undo"', async () => {
    const { fixture, component, element } = await render();

    component.userId = '4';
    component.markHandedOut();
    expect(component.userId).toBe('');
    const request = http.expectOne('/api/wear-handout');
    expect(request.request.body).toEqual({ input: '4' });
    request.flush(result);
    fixture.detectChanges();

    expect(element.querySelector('.registration-result p')?.textContent).toBe('wear.handout.marked');
    const lines = [...element.querySelectorAll('.registration-result li')].map(li =>
      li.textContent?.replace(/\s+/g, ' ').trim(),
    );
    expect(lines).toEqual([
      'T-shirt-Sort: 2 wear.handout.pieces wear.handout.inSize M (Deltager)',
      'Kasket: 1 wear.handout.pieces (Deltager)',
    ]);

    element.querySelector<HTMLButtonElement>('.undo')?.click();
    const undo = http.expectOne('/api/wear-handout/undo');
    expect(undo.request.body).toEqual({ input: '4' });
    undo.flush(result);
    fixture.detectChanges();

    expect(element.querySelector('.registration-result p')?.textContent).toBe('wear.handout.undone');
    // Undo shows the wear without the category
    expect(element.querySelector('.registration-result li')?.textContent?.trim()).toBe(
      'T-shirt-Sort: 2 wear.handout.pieces wear.handout.inSize M',
    );
    expect(element.querySelector('.undo')).toBeNull();
  });

  it('says "Intet id indtastet" without asking the API', async () => {
    const { fixture, component, element } = await render();

    component.markHandedOut();
    fixture.detectChanges();

    expect(element.querySelector('.registration-result.error')?.textContent).toContain(
      'wear.handout.error wear.handout.errors.noInput',
    );
  });

  it('shows the reason a handout was refused, and no "Undo"', async () => {
    const { fixture, component, element } = await render();

    component.userId = '4';
    component.markHandedOut();
    http
      .expectOne('/api/wear-handout')
      .flush({ reason: 'notCheckedIn', participantId: 4 }, { status: 409, statusText: 'Conflict' });
    fixture.detectChanges();

    expect(component.result()?.text).toBe('wear.handout.error wear.handout.errors.notCheckedIn');
    expect(element.querySelector('.undo')).toBeNull();
  });

  it('shows the same message whatever made "Undo" fail, like Infosys', async () => {
    const { component } = await render();

    component.userId = '4';
    component.markHandedOut();
    http.expectOne('/api/wear-handout').flush(result);
    component.undo();
    http
      .expectOne('/api/wear-handout/undo')
      .flush({ reason: 'notReceived', participantId: 4 }, { status: 409, statusText: 'Conflict' });

    expect(component.result()?.text).toBe('wear.handout.error wear.handout.errors.undoFailed');
  });
});
