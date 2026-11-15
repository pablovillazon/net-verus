import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { API_BASE_URL } from './api-config';
import { CompanyApi } from './company.api';

describe('CompanyApi', () => {
  let api: CompanyApi;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    api = TestBed.inject(CompanyApi);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('posts a new company as JSON', () => {
    api.create({ name: 'Example', websiteUrl: 'https://www.example.com' }).subscribe();

    const req = http.expectOne(`${API_BASE_URL}/companies`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ name: 'Example', websiteUrl: 'https://www.example.com' });
    req.flush({});
  });

  it('sends only the filters that have a value', () => {
    api.list({ q: 'acme', name: '', domain: undefined }).subscribe();

    const req = http.expectOne((r) => r.url === `${API_BASE_URL}/companies`);
    expect(req.request.params.get('q')).toBe('acme');
    expect(req.request.params.has('name')).toBe(false);
    expect(req.request.params.has('domain')).toBe(false);
    req.flush([]);
  });
});
