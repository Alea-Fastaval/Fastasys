import { TestBed } from '@angular/core/testing';
import { HttpErrorResponse, provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { MAX_IMAGE_BYTES, WearImageUploadComponent, readAsDataUrl, uploadErrorKey } from './wear-image-upload';

const file = new File(['png'], 'shirt.png', { type: 'image/png' });

describe('readAsDataUrl', () => {
  it('reads a file as a data URL', async () => {
    expect(await readAsDataUrl(file)).toBe('data:image/png;base64,cG5n');
  });
});

describe('uploadErrorKey', () => {
  const error = (status: number, error: unknown) => new HttpErrorResponse({ status, error });

  it("names the API's reasons, a request refused as too large, and anything else", () => {
    expect(uploadErrorKey(error(400, { reason: 'notImage' }))).toBe('notImage');
    expect(uploadErrorKey(error(409, { reason: 'nameTaken' }))).toBe('nameTaken');
    expect(uploadErrorKey(error(400, { reason: 'tooLarge' }))).toBe('tooLarge');
    // nginx or the API refuse a too large request with 413 before reading it
    expect(uploadErrorKey(error(413, '<html>413 Request Entity Too Large</html>'))).toBe('tooLarge');
    expect(uploadErrorKey(error(500, null))).toBe('uploadFailed');
  });
});

describe('WearImageUploadComponent (Infosys wear/upload-image)', () => {
  let http: HttpTestingController;

  afterEach(() => http.verify());

  const render = async () => {
    URL.createObjectURL = vi.fn(() => 'blob:uploaded');
    URL.revokeObjectURL = vi.fn();
    await TestBed.configureTestingModule({
      imports: [WearImageUploadComponent],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    }).compileComponents();
    http = TestBed.inject(HttpTestingController);
    const fixture = TestBed.createComponent(WearImageUploadComponent);
    fixture.detectChanges();
    http.match('/assets/i18n/en.json').forEach(request => request.flush({}));
    return { fixture, component: fixture.componentInstance, element: fixture.nativeElement as HTMLElement };
  };

  it('uploads the chosen file with its name and shows it', async () => {
    const { fixture, component, element } = await render();

    component.file.set(file);
    await component.upload();
    const request = http.expectOne('/api/wear-images');
    expect(request.request.body).toEqual({ fileName: 'shirt.png', data: 'data:image/png;base64,cG5n' });
    request.flush({ id: 7, fileName: 'shirt.png' });
    fixture.detectChanges();

    expect(element.querySelector('.uploaded')?.textContent).toContain('wear.messages.uploaded');
    expect(element.querySelector('img.uploaded-image')?.getAttribute('src')).toBe('blob:uploaded');
  });

  it('refuses a file over 10 MB without sending it', async () => {
    const { component } = await render();

    component.file.set(new File([new Uint8Array(MAX_IMAGE_BYTES + 1)], 'huge.png', { type: 'image/png' }));
    await component.upload();

    expect(component.errorKey()).toBe('tooLarge');
    expect(component.busy()).toBe(false);
    http.expectNone('/api/wear-images');
  });

  it("shows Infosys's messages when the file isn't an image or the name is taken", async () => {
    const { component } = await render();

    component.file.set(file);
    await component.upload();
    http.expectOne('/api/wear-images').flush({ reason: 'notImage' }, { status: 400, statusText: 'Bad request' });
    expect(component.errorKey()).toBe('notImage');

    await component.upload();
    http.expectOne('/api/wear-images').flush({ reason: 'nameTaken' }, { status: 409, statusText: 'Conflict' });
    expect(component.errorKey()).toBe('nameTaken');

    await component.upload();
    http.expectOne('/api/wear-images').flush({}, { status: 500, statusText: 'Server error' });
    expect(component.errorKey()).toBe('uploadFailed');
  });
});
