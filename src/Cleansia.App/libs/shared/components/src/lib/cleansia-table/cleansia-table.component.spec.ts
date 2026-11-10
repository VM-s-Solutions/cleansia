import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { TranslateModule } from '@ngx-translate/core';
import { readFileSync } from 'fs';
import { join } from 'path';
import { CleansiaTableComponent } from './cleansia-table.component';
import { TableColumn } from './cleansia-table.models';

/**
 * The row actions and the pager are 2rem glyph boxes; a thumb needs 44px. The hit area is the
 * shared ring grown past each box, so the boxes keep their size and the rows their rhythm. jsdom
 * lays nothing out, so the pins read the stylesheet, which is declared an input of this project's
 * test target.
 */
describe('CleansiaTableComponent — control touch floor', () => {
  const scss = readFileSync(
    join(__dirname, '../../../../assets/src/styles/components/cleansia-table.component.scss'),
    'utf-8'
  );

  it('gives each row action the shared 44px hit area around its 2rem box', () => {
    expect(scss).toMatch(
      /\.action-btn\s*\{\s*width:\s*2rem;\s*height:\s*2rem;\s*padding:\s*0;\s*position:\s*relative;\s*@include touch-target;/
    );
  });

  it('gives each pager button the shared 44px hit area around its 2rem box', () => {
    expect(scss).toMatch(
      /&__btn\s*\{\s*min-width:\s*2rem;\s*height:\s*2rem;\s*padding:\s*0 0\.5rem;\s*position:\s*relative;\s*@include touch-target;/
    );
  });
});

describe('CleansiaTableComponent — rendering', () => {
  let fixture: ComponentFixture<CleansiaTableComponent<Row>>;

  interface Row {
    id: number;
    name: string;
    total: number;
  }

  const rows = (count: number): Row[] =>
    Array.from({ length: count }, (_, i) => ({ id: i + 1, name: `Row ${i + 1}`, total: i * 10 }));

  const columns: TableColumn<Row>[] = [
    { id: 'name', field: 'name', header: 'name' },
    { id: 'total', field: 'total', header: 'total', numeric: true },
  ];

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [CleansiaTableComponent, TranslateModule.forRoot()],
      providers: [provideNoopAnimations()],
    }).compileComponents();
    fixture = TestBed.createComponent(CleansiaTableComponent<Row>);
    fixture.componentRef.setInput('columns', columns);
  });

  const query = (selector: string): HTMLElement | null => fixture.nativeElement.querySelector(selector);
  const nextButton = (): HTMLButtonElement | null =>
    fixture.nativeElement.querySelector('.pagination__controls .pagination__btn:last-child');

  it('shows twenty rows a page by default, the back-office list size', () => {
    fixture.componentRef.setInput('data', rows(25));
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelectorAll('tbody .table__row').length).toBe(20);
    expect(fixture.componentInstance.mergedConfig().rowsPerPageOptions).toEqual([5, 10, 20, 50]);
  });

  it('hides the paginator on an empty table and draws the empty state instead of a bare sentence', () => {
    fixture.componentRef.setInput('data', []);
    fixture.detectChanges();

    expect(query('.pagination')).toBeNull();
    expect(query('.table__empty .not-found-state')).not.toBeNull();
    expect(query('.table__empty .not-found-state__message')?.textContent?.trim()).toBe('global.no_data');
  });

  it('disables next on the last page rather than letting it point past the data', () => {
    fixture.componentRef.setInput('data', rows(3));
    fixture.detectChanges();

    expect(nextButton()?.disabled).toBe(true);
  });

  it('right-aligns a numeric column in the header and in every cell', () => {
    fixture.componentRef.setInput('data', rows(1));
    fixture.detectChanges();

    const header = fixture.nativeElement.querySelectorAll('thead th')[1] as HTMLElement;
    const cell = fixture.nativeElement.querySelectorAll('tbody .table__row td')[1] as HTMLElement;
    expect(header.classList.contains('text-right')).toBe(true);
    expect(cell.classList.contains('text-right')).toBe(true);
    expect(cell.classList.contains('numeric')).toBe(true);
  });

  it('gives each table its own rows-per-page id so two tables on a page keep their labels apart', () => {
    fixture.componentRef.setInput('data', rows(1));
    fixture.detectChanges();
    const second = TestBed.createComponent(CleansiaTableComponent<Row>);
    second.componentRef.setInput('columns', columns);
    second.componentRef.setInput('data', rows(1));
    second.detectChanges();

    const idOf = (host: HTMLElement) => host.querySelector('.pagination__rows-selector label')?.getAttribute('for');
    const first = idOf(fixture.nativeElement);
    expect(first).toMatch(/^rows-per-page-\d+$/);
    expect(idOf(second.nativeElement)).not.toBe(first);
    expect(fixture.nativeElement.querySelector(`.pagination__rows-selector p-select [id="${first}"]`)).not.toBeNull();
  });
});

describe('CleansiaTableComponent — row click', () => {
  let fixture: ComponentFixture<CleansiaTableComponent<Row>>;
  let opened: Row[];
  let actionClicks: Row[];

  interface Row {
    id: number;
    name: string;
  }

  const data: Row[] = [
    { id: 1, name: 'One' },
    { id: 2, name: 'Two' },
  ];

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [CleansiaTableComponent, TranslateModule.forRoot()],
      providers: [provideNoopAnimations()],
    }).compileComponents();
    fixture = TestBed.createComponent(CleansiaTableComponent<Row>);
    fixture.componentRef.setInput('columns', [{ id: 'name', field: 'name', header: 'name' }]);
    fixture.componentRef.setInput('data', data);
    fixture.componentRef.setInput('actions', [
      { icon: 'pi pi-pencil', onClick: (row: Row) => actionClicks.push(row) },
    ]);
    opened = [];
    actionClicks = [];
    fixture.componentInstance.rowClick.subscribe((row) => opened.push(row));
  });

  const render = (clickable: boolean | ((row: Row) => boolean)) => {
    fixture.componentRef.setInput('clickableRows', clickable);
    fixture.detectChanges();
  };
  const rowEls = (): HTMLTableRowElement[] =>
    Array.from(fixture.nativeElement.querySelectorAll('tbody .table__row'));
  const key = (target: Element, name: 'Enter' | ' ') =>
    target.dispatchEvent(new KeyboardEvent('keydown', { key: name, bubbles: true, cancelable: true }));

  it('gives no row a tab stop, the class or a role when rows are not clickable, and a click opens nothing', () => {
    render(false);

    for (const row of rowEls()) {
      expect(row.getAttribute('tabindex')).toBeNull();
      expect(row.classList.contains('table__row--clickable')).toBe(false);
      expect(row.getAttribute('role')).toBeNull();
    }
    rowEls()[0].querySelector('td')?.click();
    expect(opened).toEqual([]);
  });

  it('gives every clickable row a tab stop and the class, and no role', () => {
    render(true);

    for (const row of rowEls()) {
      expect(row.getAttribute('tabindex')).toBe('0');
      expect(row.classList.contains('table__row--clickable')).toBe(true);
      expect(row.getAttribute('role')).toBeNull();
    }
  });

  it('opens the row once on a click, on Enter and on Space', () => {
    render(true);
    const [first] = rowEls();

    first.querySelector('td')?.click();
    key(first, 'Enter');
    key(first, ' ');

    expect(opened).toEqual([data[0], data[0], data[0]]);
  });

  it('leaves a row inert when the per-row function answers false', () => {
    render((row) => row.id === 2);
    const [first, second] = rowEls();

    expect(first.getAttribute('tabindex')).toBeNull();
    expect(first.classList.contains('table__row--clickable')).toBe(false);
    first.querySelector('td')?.click();
    key(first, 'Enter');
    expect(opened).toEqual([]);

    expect(second.getAttribute('tabindex')).toBe('0');
    second.querySelector('td')?.click();
    expect(opened).toEqual([data[1]]);
  });

  it('opens nothing on a click on a link inside the row', () => {
    render(true);
    const cell = rowEls()[0].querySelector('td') as HTMLElement;
    const link = document.createElement('a');
    link.href = '#';
    cell.appendChild(link);

    link.click();

    expect(opened).toEqual([]);
  });

  it('opens nothing on Enter on a row action, and an action click still does not open the row', () => {
    render(true);
    const button = rowEls()[0].querySelector('.action-btn') as HTMLButtonElement;

    key(button, 'Enter');
    button.click();

    expect(opened).toEqual([]);
    expect(actionClicks).toEqual([data[0]]);
  });

  it('opens nothing on a click that ends a text selection', () => {
    render(true);
    const cell = rowEls()[0].querySelector('td') as HTMLElement;
    const range = document.createRange();
    range.selectNodeContents(cell);
    const selection = document.getSelection();
    selection?.removeAllRanges();
    selection?.addRange(range);

    cell.click();
    selection?.removeAllRanges();

    expect(opened).toEqual([]);
  });
});
