import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';

// Images behind login (ID card backgrounds and photos): an <img src> can't send the login token, so the image is
// fetched through HttpClient (which adds it) and shown from a blob URL. Revoke the URL when it is no longer shown.
export async function fetchImageUrl(http: HttpClient, url: string): Promise<string> {
  const blob = await firstValueFrom(http.get(url, { responseType: 'blob' }));
  return URL.createObjectURL(blob);
}

// The image, loaded and ready to draw on a canvas
export async function fetchImage(http: HttpClient, url: string): Promise<HTMLImageElement> {
  const objectUrl = await fetchImageUrl(http, url);
  try {
    return await loadImage(objectUrl);
  } finally {
    URL.revokeObjectURL(objectUrl);
  }
}

export function loadImage(src: string): Promise<HTMLImageElement> {
  return new Promise((resolve, reject) => {
    const image = new Image();
    image.onload = () => resolve(image);
    image.onerror = () => reject(new Error('The image could not be loaded'));
    image.src = src;
  });
}

// A blob (an image read from the API) as a data URL, so the photo page can upload the original again
export function blobToDataUrl(blob: Blob): Promise<string> {
  return new Promise((resolve, reject) => {
    const reader = new FileReader();
    reader.onload = () => resolve(reader.result as string);
    reader.onerror = () => reject(new Error('The image could not be read'));
    reader.readAsDataURL(blob);
  });
}
