import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { vi } from 'vitest';
import { App } from './app';
import { CompanyApi } from './company.api';

const company = (name: string, domain: string, score: number | null = null) => ({
  id: name, name, websiteUrl: `https://${domain}/`, domain, createdAtUtc: '2026-01-01T00:00:00Z', searchScore: score,
});

describe('App', () => {
  it('loads and renders the stored companies on start', async () => {
    const list = vi.fn().mockReturnValue(of([company('Acme Tools', 'acme-tools.com'), company('IBM', 'ibm.com')]));
    await TestBed.configureTestingModule({ imports: [App], providers: [{ provide: CompanyApi, useValue: { list, create: vi.fn() } }] }).compileComponents();

    const fixture = TestBed.createComponent(App);
    await fixture.whenStable();

    const rows = (fixture.nativeElement as HTMLElement).querySelectorAll('tbody tr');
    expect(rows.length).toBe(2);
    expect(list).toHaveBeenCalledWith({ q: '' });
  });

  it('shows an empty state and the relevance column for searches', async () => {
    const list = vi.fn().mockReturnValueOnce(of([])).mockReturnValueOnce(of([company('Acme Tools', 'acme-tools.com', 1)]));
    await TestBed.configureTestingModule({ imports: [App], providers: [{ provide: CompanyApi, useValue: { list, create: vi.fn() } }] }).compileComponents();

    const fixture = TestBed.createComponent(App);
    await fixture.whenStable();
    const root = fixture.nativeElement as HTMLElement;
    expect(root.textContent).toContain('No companies found.');

    const input = root.querySelector<HTMLInputElement>('input[type=search]')!;
    input.value = 'acme';
    root.querySelector('form.search')!.dispatchEvent(new Event('submit'));
    await fixture.whenStable();

    expect(list).toHaveBeenLastCalledWith({ q: 'acme' });
    expect(root.textContent).toContain('Relevance');
    expect(root.textContent).toContain('100%');
  });
});
