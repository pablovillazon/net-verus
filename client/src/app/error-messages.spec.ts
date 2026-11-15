import { HttpErrorResponse } from '@angular/common/http';
import { toErrorMessages } from './error-messages';

describe('toErrorMessages', () => {
  it('flattens field errors from a validation problem', () => {
    const error = new HttpErrorResponse({
      status: 400,
      error: { title: 'x', errors: { Name: ['Name is required.'], WebsiteUrl: ['URL is invalid.', 'Too short.'] } },
    });
    expect(toErrorMessages(error)).toEqual(['Name is required.', 'URL is invalid.', 'Too short.']);
  });

  it('falls back to detail, then title', () => {
    expect(toErrorMessages(new HttpErrorResponse({ status: 404, error: { detail: 'Not here.', title: 'Nope' } }))).toEqual(['Not here.']);
    expect(toErrorMessages(new HttpErrorResponse({ status: 500, error: { title: 'Boom' } }))).toEqual(['Boom']);
  });

  it('explains when the API is unreachable', () => {
    const messages = toErrorMessages(new HttpErrorResponse({ status: 0 }));
    expect(messages[0]).toContain('Cannot reach the API');
  });

  it('is generic for unknown errors', () => {
    expect(toErrorMessages(new Error('x'))).toEqual(['Something went wrong. Please try again.']);
  });
});
