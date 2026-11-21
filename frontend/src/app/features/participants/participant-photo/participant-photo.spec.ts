import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { ParticipantPhotoComponent } from './participant-photo';

// jsdom loads no images: this one "loads" at once, as a 4000 x 3000 photo
class LoadedImage {
  public width = 4000;
  public height = 3000;
  public onload: (() => void) | null = null;
  public onerror: (() => void) | null = null;
  public set src(_value: string) {
    setTimeout(() => this.onload?.());
  }
}

const cutOut = 'data:image/png;base64,Y3V0';

describe('ParticipantPhotoComponent (Infosys photo/form, "Foto upload")', () => {
  let httpMock: HttpTestingController;
  let fixture: ComponentFixture<ParticipantPhotoComponent>;
  let component: ParticipantPhotoComponent;
  let element: HTMLElement;

  beforeEach(async () => {
    vi.stubGlobal('Image', LoadedImage);
    // jsdom can't draw: no context, and the cut-out is a fixed PNG
    vi.spyOn(HTMLCanvasElement.prototype, 'getContext').mockReturnValue(null);
    vi.spyOn(HTMLCanvasElement.prototype, 'toDataURL').mockReturnValue(cutOut);

    await TestBed.configureTestingModule({
      imports: [ParticipantPhotoComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: convertToParamMap({ id: '7' }) } } },
      ],
    }).compileComponents();
    httpMock = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(ParticipantPhotoComponent);
    component = fixture.componentInstance;
    element = fixture.nativeElement as HTMLElement;
    fixture.detectChanges();
    httpMock.match('/assets/i18n/en.json').forEach(req => req.flush({}));
  });

  afterEach(() => {
    vi.unstubAllGlobals();
    vi.restoreAllMocks();
    httpMock.verify();
  });

  const original = () => httpMock.expectOne('/api/participants/7/photo/original');

  const loaded = async () => {
    await vi.waitFor(() => expect(component.image()).not.toBeNull());
    fixture.detectChanges();
  };

  const uploadButton = () => element.querySelector<HTMLButtonElement>('button.upload');

  it('loads the earlier photo, and uploads the cut-out with the original', async () => {
    const request = original();
    expect(request.request.responseType).toBe('blob');
    request.flush(new Blob(['x'], { type: 'image/png' }));
    await loaded();
    expect(uploadButton()?.disabled).toBe(false);

    const done = component.upload();
    const upload = httpMock.expectOne('/api/participants/7/photo');
    expect(upload.request.method).toBe('PUT');
    expect(upload.request.body).toEqual({ original: 'data:image/png;base64,eA==', cropped: cutOut });
    upload.flush(null);
    await done;
    fixture.detectChanges();

    // Infosys hides the cropper and shows the success message
    expect(element.querySelector('.upload-success')).not.toBeNull();
    expect(element.querySelector('.cropper')).toBeNull();
  });

  it('starts empty when the participant has no photo yet', async () => {
    original().flush(null, { status: 404, statusText: 'Not Found' });
    await new Promise(resolve => setTimeout(resolve));
    fixture.detectChanges();

    expect(component.image()).toBeNull();
    expect(uploadButton()?.disabled).toBe(true);
    await component.upload();
    httpMock.expectNone('/api/participants/7/photo');
  });

  it('cuts out a chosen image, turned and zoomed', async () => {
    original().flush(null, { status: 404, statusText: 'Not Found' });
    const file = new File(['y'], 'foto.png', { type: 'image/png' });
    await component.imageChosen({ target: { files: [file], value: 'foto.png' } } as unknown as Event);
    await loaded();

    // Filling the frame, centred
    expect(component.crop()?.scale).toBeCloseTo(295 / 3000);
    component.rotate(true);
    expect(component.crop()?.rotation).toBe(90);
    component.zoom('0.5');
    expect(component.crop()?.scale).toBe(0.5);

    const done = component.upload();
    const upload = httpMock.expectOne('/api/participants/7/photo');
    expect(upload.request.body.original).toBe('data:image/png;base64,eQ==');
    upload.flush(null);
    await done;
  });

  it('says so when the upload fails, and keeps the cropper', async () => {
    original().flush(new Blob(['x'], { type: 'image/png' }));
    await loaded();

    const done = component.upload();
    httpMock.expectOne('/api/participants/7/photo').flush(null, { status: 400, statusText: 'Bad Request' });
    await done;
    fixture.detectChanges();

    expect(element.querySelector('.upload-failure')).not.toBeNull();
    expect(element.querySelector('.cropper')).not.toBeNull();
    expect(uploadButton()?.disabled).toBe(false);
  });
});
