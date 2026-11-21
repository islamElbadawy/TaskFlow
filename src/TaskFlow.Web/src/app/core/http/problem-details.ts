import { HttpErrorResponse } from '@angular/common/http';
import { ProblemDetails } from '../api/models';
import { ApiError } from './api-error';

function isProblemDetails(value: unknown): value is ProblemDetails {
  return (
    typeof value === 'object' &&
    value !== null &&
    ('title' in value || 'detail' in value || 'errors' in value)
  );
}

function parseRetryAfter(headerValue: string | null): number | null {
  if (!headerValue) return null;
  const seconds = Number(headerValue);
  if (!Number.isNaN(seconds) && seconds > 0) {
    return Math.ceil(seconds);
  }
  const dateMs = Date.parse(headerValue);
  if (!Number.isNaN(dateMs)) {
    return Math.max(1, Math.ceil((dateMs - Date.now()) / 1000));
  }
  return null;
}

export function toApiError(error: unknown): ApiError {
  if (error instanceof ApiError) {
    return error;
  }

  if (error instanceof HttpErrorResponse) {
    const body = error.error;
    const retryAfterSeconds = parseRetryAfter(error.headers?.get('Retry-After') ?? null);

    if (error.status === 0) {
      return new ApiError({
        status: 0,
        title: 'NetworkError',
        detail: 'Unable to reach the server.',
      });
    }

    if (isProblemDetails(body)) {
      return new ApiError({
        status: body.status ?? error.status,
        title: body.title || error.statusText || 'Error',
        detail: body.detail || error.message,
        fieldErrors: body.errors ?? {},
        retryAfterSeconds,
        traceId: body.traceId ?? null,
      });
    }

    return new ApiError({
      status: error.status,
      title: error.statusText || 'Error',
      detail: typeof body === 'string' && body.length > 0 ? body : error.message,
      retryAfterSeconds,
    });
  }

  return new ApiError({
    status: 500,
    title: 'Failure',
    detail: error instanceof Error ? error.message : 'Unexpected error',
  });
}
