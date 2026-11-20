import '@angular/compiler';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { Toolbar } from './toolbar';
import { describe, beforeEach, afterEach, it, expect } from 'vitest';

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
  });
});
