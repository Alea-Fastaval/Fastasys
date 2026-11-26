import '@angular/compiler';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { Router, provideRouter } from '@angular/router';
import { AuthService } from '@shared/services';
import { Toolbar } from './toolbar';
import { describe, beforeEach, afterEach, it, expect, vi } from 'vitest';

describe('Toolbar Component', () => {
  let component: Toolbar;
  let fixture: ComponentFixture<Toolbar>;
  let httpMock: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [Toolbar],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    }).compileComponents();

    fixture = TestBed.createComponent(Toolbar);
    component = fixture.componentInstance;
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  const handleInit = () => {
    fixture.detectChanges();
    httpMock.expectOne('/assets/i18n/en.json').flush({});
  };

  it('should create toolbar component', () => {
    handleInit();
    expect(component).toBeTruthy();
  });

  it('should check active route states', () => {
    handleInit();
    expect(component.isResourcesActive()).toBe(false);
    expect(component.isManagementActive()).toBe(false);
    expect(component.isWearActive()).toBe(false);
  });

  const signIn = (privileges: string[]) =>
    TestBed.inject(AuthService).currentUser.set({
      id: 1,
      username: 'wear',
      email: '',
      firstName: '',
      lastName: '',
      roles: [],
      privileges,
    });

  it('has a Wear menu, like Infosys, with the wear pages the user may open', () => {
    handleInit();

    signIn([]);
    expect(component.mayUseWear()).toBe(false);
    signIn(['wear_view']);
    expect(component.mayUseWear()).toBe(true);
    expect(component.wearLinks().map(link => link.path)).toEqual(['/wear/breakdown', '/wear/types']);
    signIn(['wear_distribute_edit']);
    expect(component.wearLinks().map(link => link.key)).toEqual(['orders', 'unfilled', 'handout']);
  });

  it('marks the Wear menu active on the wear pages', () => {
    handleInit();
    const url = vi.spyOn(TestBed.inject(Router), 'url', 'get');

    url.mockReturnValue('/wear/types/3');
    expect(component.isWearActive()).toBe(true);
    expect(component.isResourcesActive()).toBe(false);
    url.mockReturnValue('/wear');
    expect(component.isWearActive()).toBe(true);
    url.mockReturnValue('/wearhouse');
    expect(component.isWearActive()).toBe(false);
  });
});
