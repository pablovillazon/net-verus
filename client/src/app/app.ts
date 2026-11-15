import { Component, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { CompanyForm } from './company-form/company-form';
import { CompanyList } from './company-list/company-list';
import { CompanyApi } from './company.api';
import { Company } from './company.models';
import { toErrorMessages } from './error-messages';

@Component({
  selector: 'app-root',
  imports: [CompanyForm, CompanyList],
  templateUrl: './app.html',
  styleUrl: './app.css',
})
export class App {
  private readonly api = inject(CompanyApi);

  protected readonly companies = signal<Company[]>([]);
  protected readonly query = signal('');
  protected readonly loading = signal(false);
  protected readonly loadError = signal<string | null>(null);

  constructor() {
    void this.load();
  }

  protected search(term: string): void {
    this.query.set(term.trim());
    void this.load();
  }

  protected clearSearch(): void {
    this.search('');
  }

  protected onCreated(): void {
    this.clearSearch(); // show the full list so the new company is visible
  }

  private async load(): Promise<void> {
    this.loading.set(true);
    this.loadError.set(null);
    try {
      this.companies.set(await firstValueFrom(this.api.list({ q: this.query() })));
    } catch (error) {
      this.companies.set([]);
      this.loadError.set(toErrorMessages(error)[0]);
    } finally {
      this.loading.set(false);
    }
  }
}
