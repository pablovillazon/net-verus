import { HttpErrorResponse } from '@angular/common/http';
import { ProblemDetails } from './company.models';

/** Turns any failure into short, user-friendly messages (server validation text is shown as-is). */
export function toErrorMessages(error: unknown): string[] {
  if (error instanceof HttpErrorResponse) {
    if (error.status === 0) {
      return ['Cannot reach the API. Is the backend running on http://localhost:5000?'];
    }

    const problem = error.error as ProblemDetails | null;
    const fieldMessages = Object.values(problem?.errors ?? {}).flat();
    if (fieldMessages.length > 0) {
      return fieldMessages;
    }
    if (problem?.detail) {
      return [problem.detail];
    }
    if (problem?.title) {
      return [problem.title];
    }
  }
  return ['Something went wrong. Please try again.'];
}
