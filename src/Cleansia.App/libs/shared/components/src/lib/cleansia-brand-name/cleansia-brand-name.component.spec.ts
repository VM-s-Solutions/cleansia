import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { existsSync, readFileSync } from 'fs';
import { dirname, extname, join } from 'path';
import { inflateSync } from 'zlib';
import { CleansiaBrandNameComponent } from './cleansia-brand-name.component';

function findSolutionDir(): string {
  let dir = process.cwd();
  for (let i = 0; i < 12; i++) {
    if (existsSync(join(dir, 'Cleansia.Api.sln'))) return dir;
    const parent = dirname(dir);
    if (parent === dir) break;
    dir = parent;
  }
  throw new Error('Could not locate the solution dir (Cleansia.Api.sln)');
}

const APPS_DIR = join(findSolutionDir(), 'Cleansia.App/apps');
const APPS = ['cleansia.app', 'cleansia-partner.app', 'cleansia-admin.app'];

const VARIABLES_SCSS = join(
  findSolutionDir(),
  'Cleansia.App/libs/shared/assets/src/styles/common/variables.scss'
);

const BRAND_ASSETS = [
  ...APPS.flatMap((app) =>
    ['Logo.png', 'Logo.webp', 'Logo.ico'].map((file) =>
      join(APPS_DIR, app, 'src/assets/logos', file)
    )
  ),
  join(APPS_DIR, 'cleansia.app/src/assets/images/logo.png'),
];

const PNG_SIGNATURE = Buffer.from([
  0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a,
]);

const MAGIC: Record<string, (bytes: Buffer) => boolean> = {
  '.png': (b) => b.subarray(0, 8).equals(PNG_SIGNATURE),
  '.webp': (b) =>
    b.subarray(0, 4).toString('latin1') === 'RIFF' &&
    b.subarray(8, 12).toString('latin1') === 'WEBP',
  '.ico': (b) => b.readUInt16LE(0) === 0 && b.readUInt16LE(2) === 1,
};

function paeth(a: number, b: number, c: number): number {
  const p = a + b - c;
  const pa = Math.abs(p - a);
  const pb = Math.abs(p - b);
  const pc = Math.abs(p - c);
  if (pa <= pb && pa <= pc) return a;
  return pb <= pc ? b : c;
}

/**
 * Minimal 8-bit RGBA PNG reader — enough to read the mark's ink back out.
 * No image library ships in this workspace and the alternative is trusting
 * the bytes we just wrote, which is what let a PNG masquerade as a WebP.
 */
function decodeRgbaPng(buf: Buffer): {
  width: number;
  height: number;
  data: Buffer;
} {
  let width = 0;
  let height = 0;
  let bitDepth = 0;
  let colorType = 0;
  let interlace = 0;
  const idat: Buffer[] = [];

  for (let off = 8; off + 8 <= buf.length; ) {
    const length = buf.readUInt32BE(off);
    const type = buf.subarray(off + 4, off + 8).toString('latin1');
    const data = buf.subarray(off + 8, off + 8 + length);
    if (type === 'IHDR') {
      width = data.readUInt32BE(0);
      height = data.readUInt32BE(4);
      bitDepth = data[8];
      colorType = data[9];
      interlace = data[12];
    } else if (type === 'IDAT') {
      idat.push(Buffer.from(data));
    } else if (type === 'IEND') {
      break;
    }
    off += 12 + length;
  }

  if (bitDepth !== 8 || colorType !== 6 || interlace !== 0) {
    throw new Error(
      `unsupported PNG: bitDepth=${bitDepth} colorType=${colorType} interlace=${interlace}`
    );
  }

  const raw = inflateSync(Buffer.concat(idat));
  const bpp = 4;
  const stride = width * bpp;
  const out = Buffer.alloc(height * stride);

  for (let y = 0; y < height; y++) {
    const filter = raw[y * (stride + 1)];
    const rowStart = y * (stride + 1) + 1;
    for (let x = 0; x < stride; x++) {
      const left = x >= bpp ? out[y * stride + x - bpp] : 0;
      const up = y > 0 ? out[(y - 1) * stride + x] : 0;
      const upLeft = x >= bpp && y > 0 ? out[(y - 1) * stride + x - bpp] : 0;
      let value = raw[rowStart + x];
      if (filter === 1) value += left;
      else if (filter === 2) value += up;
      else if (filter === 3) value += (left + up) >> 1;
      else if (filter === 4) value += paeth(left, up, upLeft);
      out[y * stride + x] = value & 0xff;
    }
  }

  return { width, height, data: out };
}

function visibleInkColours(png: { data: Buffer }): string[] {
  const seen = new Set<string>();
  for (let i = 0; i < png.data.length; i += 4) {
    if (png.data[i + 3] === 0) continue;
    const hex = (n: number) => n.toString(16).padStart(2, '0');
    seen.add(
      `#${hex(png.data[i])}${hex(png.data[i + 1])}${hex(png.data[i + 2])}`
    );
  }
  return [...seen];
}

function brandPrimaryFromScss(): string {
  const match = /--cleansia-primary:\s*(#[0-9a-fA-F]{6})\s*;/.exec(
    readFileSync(VARIABLES_SCSS, 'utf8')
  );
  if (!match) throw new Error('--cleansia-primary not found in variables.scss');
  return match[1].toLowerCase();
}

describe('brand mark assets', () => {
  it.each(BRAND_ASSETS)('%s has magic bytes matching its extension', (path) => {
    const bytes = readFileSync(path);
    const check = MAGIC[extname(path).toLowerCase()];
    expect(check).toBeDefined();
    expect(check(bytes)).toBe(true);
  });

  it.each(APPS)(
    '%s Logo.png is drawn in exactly one colour, and it is --cleansia-primary',
    (app) => {
      const png = decodeRgbaPng(
        readFileSync(join(APPS_DIR, app, 'src/assets/logos/Logo.png'))
      );
      expect(visibleInkColours(png)).toEqual([brandPrimaryFromScss()]);
    }
  );

  it('ships the same mark in all three apps', () => {
    const digests = APPS.map((app) =>
      readFileSync(join(APPS_DIR, app, 'src/assets/logos/Logo.png')).toString(
        'base64'
      )
    );
    expect(new Set(digests).size).toBe(1);
  });
});

describe('CleansiaBrandNameComponent', () => {
  let fixture: ComponentFixture<CleansiaBrandNameComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [CleansiaBrandNameComponent],
      providers: [provideRouter([])],
    }).compileComponents();

    fixture = TestBed.createComponent(CleansiaBrandNameComponent);
    fixture.detectChanges();
  });

  function root(): HTMLElement {
    return fixture.nativeElement.querySelector('.cleansia-brand-name');
  }

  it('marks itself compact only when asked', () => {
    expect(root().classList).not.toContain('cleansia-brand-name--compact');

    fixture.componentRef.setInput('compact', true);
    fixture.detectChanges();

    expect(root().classList).toContain('cleansia-brand-name--compact');
  });

  it('renders the wordmark alone — the mark already says "Cleansia"', () => {
    expect(fixture.nativeElement.textContent.trim()).toBe('');
    expect(fixture.nativeElement.querySelector('img').getAttribute('alt')).toBe(
      'Cleansia'
    );
  });
});
