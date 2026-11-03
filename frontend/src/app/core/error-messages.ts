import { HttpErrorResponse } from '@angular/common/http';

import { ApiProblem } from './models';

// User-facing texts for the API's error codes. The server's own "detail" is not shown:
// it is written for developers, and the UI should speak in one voice.
const MESSAGES: Record<string, string> = {
  FILE_TOO_LARGE: 'The file is too large.',
  UNSUPPORTED_FORMAT: 'This file type is not supported, or the file is not what its extension says.',
  INVALID_FILE_CONTENT: 'The file looks damaged and cannot be read.',
  CONVERSION_NOT_SUPPORTED: 'This conversion is not available for this file.',
  PROCESSING_TIMEOUT: 'The conversion took too long and was stopped.',
  PROCESSING_FAILED: 'The file could not be converted. It may be damaged.',
  JOB_EXPIRED: 'This file has expired and was deleted.',
  JOB_NOT_COMPLETED: 'The file is not ready yet.',
  JOB_IN_PROGRESS: 'The file is busy right now. Please try again in a moment.',
  ACCESS_DENIED: 'This file is no longer available.',
  STORAGE_LIMIT_EXCEEDED: 'The service is busy. Please try again later.',
  RATE_LIMIT_EXCEEDED: 'Too many uploads. Please wait a minute and try again.',
  VALIDATION_ERROR: 'The request was not valid. Please choose a file and a format.',
};

const UNKNOWN_ERROR = 'Something went wrong. Please try again.';
const NETWORK_ERROR = 'Cannot reach the server. Check your connection and try again.';

/** Message for an error code stored on a job (e.g. a failed conversion). */
export function messageForCode(code: string | null | undefined): string {
  return (code && MESSAGES[code]) || UNKNOWN_ERROR;
}

/** Message for a failed HTTP request. Never exposes technical details. */
export function messageForHttpError(error: unknown): string {
  if (!(error instanceof HttpErrorResponse)) {
    return UNKNOWN_ERROR;
  }

  // Status 0: the request never got an answer (offline, server down, request aborted).
  if (error.status === 0) {
    return NETWORK_ERROR;
  }

  return messageForCode(codeOf(error));
}

export function codeOf(error: unknown): string | undefined {
  if (error instanceof HttpErrorResponse && error.error && typeof error.error === 'object') {
    return (error.error as ApiProblem).code;
  }

  return undefined;
}
