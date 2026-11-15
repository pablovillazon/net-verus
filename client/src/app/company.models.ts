/** Mirrors the backend contracts (CompanyResponse / CreateCompanyRequest). */
export interface Company {
  id: string;
  name: string;
  websiteUrl: string;
  domain: string;
  createdAtUtc: string;
  /** Only present for relevance searches (?q=). */
  searchScore: number | null;
}

export interface CreateCompanyRequest {
  name: string;
  websiteUrl: string;
}

export interface CompanyFilters {
  /** Free-text relevance search; results come back best-first. */
  q?: string;
  name?: string;
  domain?: string;
}

/** RFC 7807 problem details as returned by the API for 4xx/5xx responses. */
export interface ProblemDetails {
  title?: string;
  detail?: string;
  status?: number;
  errors?: Record<string, string[]>;
}
