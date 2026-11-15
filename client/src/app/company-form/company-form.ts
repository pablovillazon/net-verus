import { Component, inject, output, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { CompanyApi } from '../company.api';
import { Company } from '../company.models';
import { toErrorMessages } from '../error-messages';

/**
 * Submission form. Client-side checks are limited to "required": the backend is the single source of truth
 * for every other rule (length, URL format, relevance), so rules can never drift apart. Its messages are shown verbatim.
 */
@Component({
  selector: 'app-company-form',
  imports: [ReactiveFormsModule],
  templateUrl: './company-form.html',
  styleUrl: './company-form.css',
})
export class CompanyForm {
  private readonly api = inject(CompanyApi);
  private readonly fb = inject(NonNullableFormBuilder);

  readonly created = output<Company>();

  protected readonly form = this.fb.group({
    name: ['', Validators.required],
    websiteUrl: ['', Validators.required],
  });
  protected readonly submitting = signal(false);
  protected readonly errors = signal<string[]>([]);
  protected readonly success = signal<string | null>(null);

  protected async submit(): Promise<void> {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.submitting.set(true);
    this.errors.set([]);
    this.success.set(null);

    try {
      const company = await firstValueFrom(this.api.create(this.form.getRawValue()));
      this.success.set(`"${company.name}" was validated and stored.`);
      this.form.reset();
      this.created.emit(company);
    } catch (error) {
      this.errors.set(toErrorMessages(error));
    } finally {
      this.submitting.set(false);
    }
  }
}
