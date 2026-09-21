import { fileExtensionOf, formatFileSize } from './profile-documents.helpers';

describe('formatFileSize', () => {
  it('pluralises bytes in the session language', () => {
    expect(formatFileSize(1, 'en')).toBe('1 byte');
    expect(formatFileSize(512, 'en')).toBe('512 bytes');
    expect(formatFileSize(1, 'cs')).toBe('1 bajt');
    expect(formatFileSize(2, 'cs')).toBe('2 bajty');
    expect(formatFileSize(512, 'cs')).toBe('512 bajtů');
    expect(formatFileSize(512, 'uk')).toBe('512 байтів');
  });

  it('steps to the next unit at 1024 and keeps one decimal', () => {
    expect(formatFileSize(1024, 'en')).toBe('1 kB');
    expect(formatFileSize(1536, 'en')).toBe('1.5 kB');
    expect(formatFileSize(1536, 'cs')).toBe('1,5 kB');
    expect(formatFileSize(10.25 * 1024 * 1024, 'en')).toBe('10.3 MB');
    expect(formatFileSize(2 * 1024 ** 3, 'en')).toBe('2 GB');
  });

  it('reads zero, a missing size and a negative size as nothing', () => {
    expect(formatFileSize(0, 'en')).toBe('0 bytes');
    expect(formatFileSize(undefined, 'en')).toBe('');
    expect(formatFileSize(-5, 'en')).toBe('');
  });

  it('falls back to English for an unknown language', () => {
    expect(formatFileSize(1536, undefined)).toBe('1.5 kB');
  });
});

describe('fileExtensionOf', () => {
  it('returns the upper-cased extension after the last dot', () => {
    expect(fileExtensionOf('scan.pdf')).toBe('PDF');
    expect(fileExtensionOf('archive.tar.gz')).toBe('GZ');
    expect(fileExtensionOf('Photo.JPEG')).toBe('JPEG');
  });

  // A name without a dot used to render itself as the extension, which the tile wrapped one
  // letter per line.
  it('returns nothing for a name with no extension, a dotfile or a trailing dot', () => {
    expect(fileExtensionOf('Ukázkový dlouhý název')).toBe('');
    expect(fileExtensionOf('.env')).toBe('');
    expect(fileExtensionOf('name.')).toBe('');
    expect(fileExtensionOf(undefined)).toBe('');
  });

  it('refuses an extension longer than five characters as not one', () => {
    expect(fileExtensionOf('report.final version')).toBe('');
  });
});
