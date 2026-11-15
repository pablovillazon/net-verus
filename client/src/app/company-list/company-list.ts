import { DatePipe, DecimalPipe } from '@angular/common';
import { Component, computed, input } from '@angular/core';
import { Company } from '../company.models';

@Component({
  selector: 'app-company-list',
  imports: [DatePipe, DecimalPipe],
  templateUrl: './company-list.html',
  styleUrl: './company-list.css',
})
export class CompanyList {
  readonly companies = input.required<Company[]>();
  readonly loading = input(false);
  readonly error = input<string | null>(null);

  /** The relevance column only makes sense when the results came from a ?q= search. */
  protected readonly showScore = computed(() => this.companies().some((c) => c.searchScore !== null));
}
