// Shapes of the backend's JSON. Kept in one place so a contract change is easy to spot.

export type JobStatus = 'Pending' | 'Processing' | 'Completed' | 'Failed' | 'Expired' | 'Cancelled';

export const FINAL_STATUSES: readonly JobStatus[] = ['Completed', 'Failed', 'Expired', 'Cancelled'];

export interface SupportedFormat {
  source: string;
  targets: string[];
}

export interface FormatsResponse {
  formats: SupportedFormat[];
}

export interface CreateConversionResponse {
  jobId: string;
  accessToken: string;
  status: JobStatus;
  statusUrl: string;
}

export interface ConversionStatusResponse {
  jobId: string;
  originalFileName: string;
  sourceFormat: string;
  targetFormat: string;
  status: JobStatus;
  outputSizeBytes: number | null;
  errorCode: string | null;
  createdAtUtc: string;
  expiresAtUtc: string;
  downloadAvailable: boolean;
}

/** RFC 9457 error body the API returns, with our extra "code" field. */
export interface ApiProblem {
  status?: number;
  detail?: string;
  code?: string;
}
