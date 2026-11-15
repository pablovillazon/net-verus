import { HttpErrorResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { vi } from 'vitest';
import { CompanyApi } from '../company.api';
import { CompanyForm } from './company-form';

const stored = { id: '1', name: 'Example', websiteUrl: 'https://www.example.com/', domain: 'example.com', createdAtUtc: '2026-01-01T00:00:00Z', searchScore: null };

async function setup(create: ReturnType<typeof vi.fn>) {
  await TestBed.configureTestingModule({ imports: [CompanyForm], providers: [{ provide: CompanyApi, useValue: { create } }] }).compileComponents();
  const fixture = TestBed.createComponent(CompanyForm);
  await fixture.whenStable();
  const root = fixture.nativeElement as HTMLElement;
  const fill = async (name: string, url: string) => {
    const inputs = root.querySelectorAll('input');
    inputs[0].value = name;
    inputs[0].dispatchEvent(new Event('input'));
    inputs[1].value = url;
    inputs[1].dispatchEvent(new Event('input'));
    await fixture.whenStable();
  };
  const submit = async () => {
    root.querySelector('form')!.dispatchEvent(new Event('submit'));
    await fixture.whenStable();
  };
  return { fixture, root, fill, submit };
}

describe('CompanyForm', () => {
  it('does not call the API when required fields are empty', async () => {
    const create = vi.fn();
    const { root, submit } = await setup(create);

    await submit();

    expect(create).not.toHaveBeenCalled();
    expect(root.textContent).toContain('Company name is required.');
  });

  it('shows a success message and emits the created company', async () => {
    const create = vi.fn().mockReturnValue(of(stored));
    const { fixture, root, fill, submit } = await setup(create);
    const emitted: unknown[] = [];
    fixture.componentInstance.created.subscribe((c) => emitted.push(c));

    await fill('Example', 'https://www.example.com');
    await submit();

    expect(create).toHaveBeenCalledWith({ name: 'Example', websiteUrl: 'https://www.example.com' });
    expect(root.querySelector('[role=status]')?.textContent).toContain('Example');
    expect(emitted).toEqual([stored]);
  });

  it('shows the server validation message on a 400', async () => {
    const problem = { status: 400, error: { errors: { Relevance: ["Company name 'Acme' does not appear to be related to 'example.com'."] } } };
    const create = vi.fn().mockReturnValue(throwError(() => new HttpErrorResponse(problem)));
    const { root, fill, submit } = await setup(create);

    await fill('Acme', 'https://www.example.com');
    await submit();

    expect(root.querySelector('[role=alert]')?.textContent).toContain('does not appear to be related');
    expect(root.querySelector('[role=status]')).toBeNull();
  });
});
