import { HttpErrorResponse } from '@angular/common/http';
import { describeHttpError } from './problem-details';

describe('describeHttpError', () => {
  it('joins validation messages', () => {
    const error = new HttpErrorResponse({
      status: 400,
      error: { errors: { Name: ['Name is required.'], Description: ['Too long.'] } },
    });

    expect(describeHttpError(error)).toBe('Name is required. Too long.');
  });

  it('prefers detail over title', () => {
    const error = new HttpErrorResponse({
      status: 409,
      error: { title: 'Conflict', detail: 'Changed by someone else.' },
    });

    expect(describeHttpError(error)).toBe('Changed by someone else.');
  });

  it('falls back to the status code', () => {
    expect(describeHttpError(new HttpErrorResponse({ status: 500 }))).toBe('Request failed (500).');
  });

  it('handles non-HTTP errors', () => {
    expect(describeHttpError(new Error('boom'))).toBe('Something went wrong.');
  });
});
