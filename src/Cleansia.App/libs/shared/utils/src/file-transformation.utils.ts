/**
 * File transformation utilities for handling file operations
 */

export interface FileTransformationOptions {
  maxSizeInMB?: number;
  allowedTypes?: string[];
  includeMetadata?: boolean;
}

export interface FileMetadata {
  name: string;
  size: number;
  type: string;
  lastModified: number;
}

export interface BlobFileDto {
  fileName: string;
  base64Content: string;
  contentType: string;
  metadata?: FileMetadata;
}

export interface FileTransformationResult {
  success: boolean;
  data?: BlobFileDto[];
  errors?: string[];
}

/**
 * Utility class for file transformations and operations
 */
export class FileTransformationUtils {
  /**
   * Converts a File to base64 string
   */
  static async fileToBase64(file: File): Promise<string> {
    return new Promise((resolve, reject) => {
      const reader = new FileReader();

      reader.onload = () => {
        const result = reader.result as string;
        // Remove the data URL prefix if present (data:image/png;base64,)
        const base64 = result.includes(',') ? result.split(',')[1] : result;
        resolve(base64);
      };

      reader.onerror = (error) => {
        reject(new Error(`Failed to read file: ${error}`));
      };

      reader.readAsDataURL(file);
    });
  }

  /**
   * Converts multiple files to BlobFileDto array
   */
  static async convertFilesToBlobFileDtos(
    files: File[] | FileList,
    options: FileTransformationOptions = {}
  ): Promise<FileTransformationResult> {
    const fileArray = Array.isArray(files) ? files : Array.from(files);

    if (!fileArray.length) {
      return { success: true, data: [] };
    }

    try {
      const transformationPromises = fileArray.map(file =>
        this.transformSingleFile(file, options)
      );

      const results = await Promise.allSettled(transformationPromises);
      const successfulTransformations: BlobFileDto[] = [];
      const errors: string[] = [];

      results.forEach((result, index) => {
        if (result.status === 'fulfilled') {
          successfulTransformations.push(result.value);
        } else {
          errors.push(`File ${fileArray[index].name}: ${result.reason.message}`);
        }
      });

      return {
        success: errors.length === 0,
        data: successfulTransformations,
        errors: errors.length > 0 ? errors : undefined
      };
    } catch (error) {
      return {
        success: false,
        errors: [`Transformation failed: ${error instanceof Error ? error.message : 'Unknown error'}`]
      };
    }
  }

  /**
   * Transforms a single file to BlobFileDto
   */
  private static async transformSingleFile(
    file: File,
    options: FileTransformationOptions
  ): Promise<BlobFileDto> {
    // Validate file if options are provided
    if (options.maxSizeInMB && file.size > options.maxSizeInMB * 1024 * 1024) {
      throw new Error(`File size exceeds ${options.maxSizeInMB}MB limit`);
    }

    if (options.allowedTypes?.length) {
      const fileExtension = file.name.split('.').pop()?.toLowerCase();
      const isAllowed = options.allowedTypes.some(type =>
        type.toLowerCase().includes(fileExtension || '') ||
        file.type.toLowerCase().includes(type.toLowerCase())
      );

      if (!isAllowed) {
        throw new Error(`File type not allowed. Allowed types: ${options.allowedTypes.join(', ')}`);
      }
    }

    const base64Content = await this.fileToBase64(file);

    const blobFileDto: BlobFileDto = {
      fileName: file.name,
      base64Content,
      contentType: file.type || 'application/octet-stream'
    };

    if (options.includeMetadata) {
      blobFileDto.metadata = {
        name: file.name,
        size: file.size,
        type: file.type,
        lastModified: file.lastModified
      };
    }

    return blobFileDto;
  }

  /**
   * Validates files against constraints
   */
  static validateFiles(
    files: File[] | FileList,
    options: FileTransformationOptions
  ): { isValid: boolean; errors: string[] } {
    const fileArray = Array.isArray(files) ? files : Array.from(files);
    const errors: string[] = [];

    fileArray.forEach((file, index) => {
      if (options.maxSizeInMB && file.size > options.maxSizeInMB * 1024 * 1024) {
        errors.push(`File ${index + 1} (${file.name}) exceeds ${options.maxSizeInMB}MB size limit`);
      }

      if (options.allowedTypes?.length) {
        const fileExtension = file.name.split('.').pop()?.toLowerCase();
        const isAllowed = options.allowedTypes.some(type =>
          type.toLowerCase().includes(fileExtension || '') ||
          file.type.toLowerCase().includes(type.toLowerCase())
        );

        if (!isAllowed) {
          errors.push(`File ${index + 1} (${file.name}) type not allowed`);
        }
      }
    });

    return {
      isValid: errors.length === 0,
      errors
    };
  }

  /**
   * Gets file array from FileList or File array
   */
  static normalizeFiles(files: File[] | FileList | null): File[] {
    if (!files) return [];
    return Array.isArray(files) ? files : Array.from(files);
  }

  /**
   * Removes a file from a file array by index
   */
  static removeFileByIndex(files: File[], index: number): File[] {
    if (index < 0 || index >= files.length) {
      return files;
    }
    return files.filter((_, i) => i !== index);
  }

  /**
   * Gets total size of files in bytes
   */
  static getTotalFileSize(files: File[] | FileList): number {
    const fileArray = Array.isArray(files) ? files : Array.from(files);
    return fileArray.reduce((total, file) => total + file.size, 0);
  }

  /**
   * Formats file size in human readable format
   */
  static formatFileSize(bytes: number): string {
    if (bytes === 0) return '0 Bytes';

    const k = 1024;
    const sizes = ['Bytes', 'KB', 'MB', 'GB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));

    return `${parseFloat((bytes / Math.pow(k, i)).toFixed(2))} ${sizes[i]}`;
  }
}