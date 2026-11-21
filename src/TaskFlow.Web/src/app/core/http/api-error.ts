export class ApiError extends Error {
  readonly status: number;
  readonly title: string;
  readonly detail: string;
  readonly fieldErrors: Readonly<Record<string, readonly string[]>>;
  readonly retryAfterSeconds: number | null;
  readonly traceId: string | null;

  constructor(params: {
    status: number;
    title: string;
    detail: string;
    fieldErrors?: Record<string, string[]>;
    retryAfterSeconds?: number | null;
    traceId?: string | null;
  }) {
    super(params.detail || params.title);
    this.name = 'ApiError';
    this.status = params.status;
    this.title = params.title;
    this.detail = params.detail;
    this.fieldErrors = params.fieldErrors ?? {};
    this.retryAfterSeconds = params.retryAfterSeconds ?? null;
    this.traceId = params.traceId ?? null;
  }

  /** Case-insensitive lookup for ASP.NET PascalCase ProblemDetails field error keys. */
  fieldError(fieldName: string): string | null {
    const target = fieldName.toLowerCase();
    for (const [key, messages] of Object.entries(this.fieldErrors)) {
      if (key.toLowerCase() === target && messages.length > 0) {
        return messages[0];
      }
    }
    return null;
  }
}
