import { HttpInterceptorFn, HttpResponse } from '@angular/common/http';
import { map } from 'rxjs';
import { ApiResult } from '../api/models';

function isApiResult(body: unknown): body is ApiResult<unknown> {
  return (
    typeof body === 'object' &&
    body !== null &&
    'isSuccess' in body &&
    (body as Record<string, unknown>)['isSuccess'] === true &&
    'data' in body
  );
}

export const envelopeInterceptor: HttpInterceptorFn = (req, next) =>
  next(req).pipe(
    map((event) => {
      if (event instanceof HttpResponse && isApiResult(event.body)) {
        return event.clone({ body: event.body.data });
      }
      return event;
    }),
  );
