import { isPlatformBrowser } from '@angular/common';
import { Injectable, PLATFORM_ID, inject } from '@angular/core';

/** Hands a served or built Blob to the browser as a named download; a no-op on the server. */
@Injectable({ providedIn: 'root' })
export class FileDownloadService {
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));

  downloadBlob(blob: Blob, fileName: string): void {
    if (!this.isBrowser) return;
    const url = URL.createObjectURL(blob);
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = fileName;
    anchor.click();
    URL.revokeObjectURL(url);
  }
}
