import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { API_BASE_URL } from './api-config';
import { Company, CompanyFilters, CreateCompanyRequest } from './company.models';

/** The only place that knows the HTTP shape of the backend. Components depend on this, never on HttpClient. */
@Injectable({ providedIn: 'root' })
export class CompanyApi {
  private readonly http = inject(HttpClient);
  private readonly url = `${API_BASE_URL}/companies`;

  create(request: CreateCompanyRequest): Observable<Company> {
    return this.http.post<Company>(this.url, request);
  }

  list(filters: CompanyFilters = {}): Observable<Company[]> {
    let params = new HttpParams();
    for (const [key, value] of Object.entries(filters)) {
      if (value) {
        params = params.set(key, value);
      }
    }
    return this.http.get<Company[]>(this.url, { params });
  }
}
