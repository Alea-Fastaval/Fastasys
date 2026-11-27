import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { TranslationService } from './translation.service';
import { describe, beforeEach, afterEach, it, expect } from 'vitest';

describe('TranslationService', () => {
  let service: TranslationService;
  let httpMock: HttpTestingController;

  const mockEnDict = {
    nav: {
      brand: 'Fastasys',
      participants: 'Participants',
      management: 'Management',
    },
    common: {
      actions: 'Actions',
    },
    roles: {
      title: 'Role & Access Control',
      subtitle: 'Configure system roles and fine-grained domain permissions for FastaSys.',
      addRole: 'Add New Role',
      roleName: 'Role Name',
      systemRoles: 'System Roles',
      permissionsMatrix: 'Permissions Matrix:',
    },
  };

  const mockDaDict = {
    nav: {
      brand: 'Fastasys',
      participants: 'Deltagere',
      management: 'Administration',
    },
    common: {
      actions: 'Handlinger',
    },
    roles: {
      title: 'Roller & Rettigheder',
      subtitle: 'Konfigurer systemroller og domænerettigheder i FastaSys.',
      addRole: 'Tilføj ny rolle',
      roleName: 'Rollenavn',
      systemRoles: 'Systemroller',
      permissionsMatrix: 'Rettighedsmatrix:',
    },
  };

  beforeEach(() => {
    globalThis.localStorage?.removeItem('fastasys_lang');

    TestBed.configureTestingModule({
      providers: [TranslationService, provideHttpClient(), provideHttpClientTesting()],
    });

    service = TestBed.inject(TranslationService);
    httpMock = TestBed.inject(HttpTestingController);

    // Default load triggered on constructor
    const req = httpMock.expectOne('/assets/i18n/en.json');
    req.flush(mockEnDict);
  });

  afterEach(() => {
    httpMock?.verify();
    globalThis.localStorage?.clear();
  });

  it('should initialize and load default English dictionary', () => {
    expect(service.currentLang()).toBe('en');
    expect(service.translate('nav.participants')).toBe('Participants');
    expect(service.translate('nav.management')).toBe('Management');
    expect(service.translate('roles.title')).toBe('Role & Access Control');
    expect(service.translate('roles.subtitle')).toBe(
      'Configure system roles and fine-grained domain permissions for FastaSys.',
    );
    expect(service.translate('roles.addRole')).toBe('Add New Role');
    expect(service.translate('roles.roleName')).toBe('Role Name');
    expect(service.translate('roles.systemRoles')).toBe('System Roles');
    expect(service.translate('roles.permissionsMatrix')).toBe('Permissions Matrix:');
  });

  it('should switch language to Danish and update role/nav translations', () => {
    service.setLanguage('da');

    const req = httpMock.expectOne('/assets/i18n/da.json');
    expect(req.request.method).toBe('GET');
    req.flush(mockDaDict);

    expect(service.currentLang()).toBe('da');
    expect(service.translate('nav.management')).toBe('Administration');
    expect(service.translate('roles.title')).toBe('Roller & Rettigheder');
    expect(service.translate('roles.subtitle')).toBe('Konfigurer systemroller og domænerettigheder i FastaSys.');
    expect(service.translate('roles.addRole')).toBe('Tilføj ny rolle');
    expect(service.translate('roles.roleName')).toBe('Rollenavn');
    expect(service.translate('roles.systemRoles')).toBe('Systemroller');
    expect(service.translate('roles.permissionsMatrix')).toBe('Rettighedsmatrix:');
  });

  it('should fallback to key if key is missing in dictionary', () => {
    expect(service.translate('nonexistent.key')).toBe('nonexistent.key');
  });
});
