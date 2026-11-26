import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { describe, expect, it } from 'vitest';
import { AuthService } from '@shared/services';
import { WearOverviewComponent } from './wear-overview';

describe('WearOverviewComponent (Infosys wear/)', () => {
  const render = async (privileges: string[]) => {
    await TestBed.configureTestingModule({
      imports: [WearOverviewComponent],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    }).compileComponents();
    TestBed.inject(AuthService).currentUser.set({
      id: 1,
      username: 'wear',
      email: '',
      firstName: '',
      lastName: '',
      roles: [],
      privileges,
    });
    const fixture = TestBed.createComponent(WearOverviewComponent);
    fixture.detectChanges();
    TestBed.inject(HttpTestingController)
      .match('/assets/i18n/en.json')
      .forEach(request => request.flush({}));
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  };

  const links = (element: HTMLElement) =>
    [...element.querySelectorAll('.wear-links a')].map(a => a.getAttribute('href'));

  it('lists every wear page for a user who may use them all', async () => {
    const element = await render(['wear_edit', 'wear_list_view', 'wear_distribute_edit']);

    expect(links(element)).toEqual([
      '/wear/breakdown',
      '/wear/orders',
      '/wear/orders/unfilled',
      '/wear/types',
      '/wear/types/new',
      '/wear/attributes',
      '/wear/images/upload',
      '/wear/handout',
    ]);
  });

  it('lists only the pages the user may open', async () => {
    const element = await render(['wear_view']);

    expect(links(element)).toEqual(['/wear/breakdown', '/wear/types']);
  });
});
