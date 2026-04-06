import {
  BlobFileDto,
  GetOrderPhotosResponse,
  PhotoType,
  SaveOrderPhotosPhotoToSave,
} from '@cleansia/partner-services';
import { GalleryPhoto } from './photo-gallery.component';

export interface StagedPhoto {
  file: BlobFileDto;
  photoType: PhotoType;
  notes?: string;
  preview: string;
}

export const PHOTO_MAX_SIZE = 10 * 1024 * 1024; // 10 MB
export const PHOTO_ALLOWED_TYPES = [
  'image/jpeg',
  'image/jpg',
  'image/png',
  'image/webp',
];

export interface FileValidationResult {
  valid: boolean;
  errorKey?: string;
}

export function validatePhotoFile(file: File): FileValidationResult {
  if (file.size > PHOTO_MAX_SIZE) {
    return {
      valid: false,
      errorKey: 'global.messages.orders.photo_size_exceeded',
    };
  }

  if (!PHOTO_ALLOWED_TYPES.includes(file.type)) {
    return {
      valid: false,
      errorKey: 'global.messages.orders.photo_invalid_type',
    };
  }

  return { valid: true };
}

export function formatPhotoDate(date: any): string {
  if (!date) return '';
  const dateObj = typeof date === 'string' ? new Date(date) : date;
  return dateObj.toLocaleString('en-GB');
}

export function filterPhotosByType(
  photosData: GetOrderPhotosResponse | null,
  photoType: PhotoType
) {
  return (
    photosData?.photos?.filter((p) => p.photoType === photoType) || []
  );
}

export function filterStagedByType(
  staged: StagedPhoto[],
  photoType: PhotoType
): StagedPhoto[] {
  return staged.filter((p) => p.photoType === photoType);
}

export function buildGalleryPhotos(
  photosData: GetOrderPhotosResponse | null,
  stagedPhotos: StagedPhoto[]
): GalleryPhoto[] {
  const uploaded =
    photosData?.photos?.map((p) => ({
      id: p.id,
      url: p.blobUrl!,
      fileName: p.originalFileName || p.fileName,
      capturedAt: p.capturedAt,
      capturedByEmployeeName: p.capturedByEmployeeName,
      isStaged: false,
    })) || [];

  const staged = stagedPhotos.map((s) => ({
    url: s.preview,
    fileName: s.file.fileName,
    isStaged: true,
  }));

  return [...uploaded, ...staged];
}

export function createStagedPhoto(
  base64Content: string,
  file: File,
  photoType: PhotoType
): StagedPhoto {
  return {
    file: new BlobFileDto({
      fileName: file.name,
      base64Content,
      contentType: file.type,
    }),
    photoType,
    preview: base64Content,
  };
}

export function buildPhotosToSave(
  staged: StagedPhoto[]
): SaveOrderPhotosPhotoToSave[] {
  return staged.map(
    (sp) =>
      new SaveOrderPhotosPhotoToSave({
        photoType: sp.photoType,
        file: sp.file,
        notes: sp.notes,
      })
  );
}

export function calculateStagedIndex(
  globalIndex: number,
  uploadedCount: number
): number {
  return globalIndex - uploadedCount;
}
