import { HttpErrorResponse, HttpEventType, HttpResponse } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Subscription, repeat, takeWhile } from 'rxjs';

import { ConversionApi } from './conversion-api';
import { codeOf, messageForCode, messageForHttpError } from './error-messages';
import { ConversionStatusResponse, FINAL_STATUSES, JobStatus } from './models';

const POLL_INTERVAL_MS = 2000;

/** "Uploading" exists only in the browser: the server knows nothing about the job yet. */
export type JobPhase = 'Uploading' | JobStatus;

/** Everything a job card needs to show. The access token is deliberately not here. */
export interface JobView {
  /** Local id: exists before the server assigns a job id. */
  id: string;
  fileName: string;
  fileSizeBytes: number;
  targetFormat: string;
  phase: JobPhase;
  /** 0-100 while uploading, null otherwise. */
  uploadProgress: number | null;
  sourceFormat: string | null;
  outputSizeBytes: number | null;
  expiresAtUtc: string | null;
  downloadAvailable: boolean;
  /** Cancellation was requested and the server is stopping the conversion. */
  cancelling: boolean;
  /** A user-facing error: failed upload, failed conversion, failed download... */
  errorMessage: string | null;
  /** Polling stopped because of a network error; the card can offer "Check again". */
  statusCheckFailed: boolean;
}

/**
 * Holds the jobs of this browser tab and talks to the API about them. Access tokens live only
 * in memory (never localStorage): a reload forgets the jobs, which is fine for files that are
 * deleted within an hour anyway.
 */
@Injectable({ providedIn: 'root' })
export class JobsStore {
  private readonly api = inject(ConversionApi);

  private readonly jobsState = signal<JobView[]>([]);
  private readonly serverIds = new Map<string, string>();
  private readonly tokens = new Map<string, string>();
  private readonly subscriptions = new Map<string, Subscription>();
  private lastLocalId = 0;

  /** Newest first. */
  readonly jobs = this.jobsState.asReadonly();
  readonly hasActiveJobs = computed(() =>
    this.jobs().some(
      (job) => job.phase === 'Uploading' || job.phase === 'Pending' || job.phase === 'Processing',
    ),
  );

  start(file: File, targetFormat: string): void {
    // A counter, not crypto.randomUUID(): that one exists only on HTTPS or localhost,
    // and the app may be opened over plain HTTP in a local network.
    const id = `job-${++this.lastLocalId}`;

    this.jobsState.update((jobs) => [
      {
        id,
        fileName: file.name,
        fileSizeBytes: file.size,
        targetFormat,
        phase: 'Uploading',
        uploadProgress: 0,
        sourceFormat: null,
        outputSizeBytes: null,
        expiresAtUtc: null,
        downloadAvailable: false,
        cancelling: false,
        errorMessage: null,
        statusCheckFailed: false,
      },
      ...jobs,
    ]);

    const upload = this.api.createConversion(file, targetFormat).subscribe({
      next: (event) => {
        if (event.type === HttpEventType.UploadProgress) {
          const progress = event.total ? Math.round((event.loaded / event.total) * 100) : 0;
          this.patch(id, { uploadProgress: progress });
        } else if (event.type === HttpEventType.Response && event.body) {
          this.serverIds.set(id, event.body.jobId);
          this.tokens.set(id, event.body.accessToken);
          this.patch(id, { phase: event.body.status, uploadProgress: null });
          this.poll(id);
        }
      },
      error: (error: unknown) => {
        // The job was never created on the server: there is nothing to poll or delete.
        this.patch(id, {
          phase: 'Failed',
          uploadProgress: null,
          errorMessage: messageForHttpError(error),
        });
      },
    });

    this.subscriptions.set(id, upload);
  }

  /** Polling stopped after a network error: start it again. */
  checkAgain(id: string): void {
    this.patch(id, { statusCheckFailed: false });
    this.poll(id);
  }

  download(id: string): void {
    const serverId = this.serverIds.get(id);
    const token = this.tokens.get(id);
    if (!serverId || !token) {
      return;
    }

    this.patch(id, { errorMessage: null });

    this.api.download(serverId, token).subscribe({
      next: (response) => saveFile(response, this.find(id)),
      // The error body of a blob request is a Blob too, not parsed JSON.
      error: async (error: unknown) =>
        this.patch(id, { errorMessage: await messageForBlobError(error) }),
    });
  }

  /**
   * Uploading: aborts the upload. Pending/Processing: cancels the job.
   * Finished jobs: deletes the files from the server and removes the card.
   */
  cancelOrRemove(id: string): void {
    const job = this.find(id);
    const serverId = this.serverIds.get(id);
    const token = this.tokens.get(id);

    // Not on the server (upload in progress or failed), or the server has already forgotten it.
    if (!job || !serverId || !token || job.phase === 'Uploading' || job.phase === 'Expired') {
      this.forget(id);
      return;
    }

    this.patch(id, { errorMessage: null });

    this.api.delete(serverId, token).subscribe({
      next: (response: HttpResponse<unknown>) => {
        if (response.status === 202) {
          // The worker is stopping the conversion; polling will show "Cancelled" shortly.
          this.patch(id, { cancelling: true });
        } else {
          this.forget(id);
        }
      },
      error: (error: unknown) => {
        if (codeOf(error) === 'ACCESS_DENIED') {
          // Already deleted on the server (expired, or removed in another way).
          this.forget(id);
        } else {
          this.patch(id, { errorMessage: messageForHttpError(error) });
        }
      },
    });
  }

  private poll(id: string): void {
    const serverId = this.serverIds.get(id);
    const token = this.tokens.get(id);
    if (!serverId || !token) {
      return;
    }

    this.subscriptions.get(id)?.unsubscribe();

    // repeat({ delay }), not a timer + switchMap: the next request starts 2 s after the previous
    // answer. A timer would cancel a slow request, and that status would never arrive.
    const polling = this.api
      .getStatus(serverId, token)
      .pipe(
        repeat({ delay: POLL_INTERVAL_MS }),
        // "true": also emit the final status, then stop.
        takeWhile((status) => !FINAL_STATUSES.includes(status.status), true),
      )
      .subscribe({
        next: (status) => this.applyStatus(id, status),
        error: (error: unknown) => {
          if (error instanceof HttpErrorResponse && error.status === 403) {
            // The token is gone: the job expired and the cleanup deleted it.
            this.patch(id, { phase: 'Expired', downloadAvailable: false });
          } else {
            this.patch(id, { statusCheckFailed: true });
          }
        },
      });

    this.subscriptions.set(id, polling);
  }

  private applyStatus(id: string, status: ConversionStatusResponse): void {
    this.patch(id, {
      phase: status.status,
      sourceFormat: status.sourceFormat,
      outputSizeBytes: status.outputSizeBytes,
      expiresAtUtc: status.expiresAtUtc,
      downloadAvailable: status.downloadAvailable,
      cancelling: status.status === 'Processing' && this.find(id)?.cancelling === true,
      errorMessage: status.status === 'Failed' ? messageForCode(status.errorCode) : null,
    });
  }

  private forget(id: string): void {
    this.subscriptions.get(id)?.unsubscribe(); // also aborts an upload in progress
    this.subscriptions.delete(id);
    this.serverIds.delete(id);
    this.tokens.delete(id);
    this.jobsState.update((jobs) => jobs.filter((job) => job.id !== id));
  }

  private find(id: string): JobView | undefined {
    return this.jobs().find((job) => job.id === id);
  }

  private patch(id: string, changes: Partial<JobView>): void {
    this.jobsState.update((jobs) =>
      jobs.map((job) => (job.id === id ? { ...job, ...changes } : job)),
    );
  }
}

/** Hands the downloaded blob to the browser as a file. */
function saveFile(response: HttpResponse<Blob>, job: JobView | undefined): void {
  if (!response.body) {
    return;
  }

  const url = URL.createObjectURL(response.body);
  const link = document.createElement('a');
  link.href = url;
  link.download = fileNameFrom(response) ?? fallbackFileName(job);
  link.click();

  // Give the browser a moment to start the download before the blob URL is released.
  setTimeout(() => URL.revokeObjectURL(url), 1000);
}

/** Reads `filename*=UTF-8''...` (non-Latin names) or `filename=...` from Content-Disposition. */
function fileNameFrom(response: HttpResponse<Blob>): string | null {
  const header = response.headers.get('Content-Disposition');
  if (!header) {
    return null;
  }

  const encoded = /filename\*=UTF-8''([^;]+)/i.exec(header);
  if (encoded) {
    return decodeURIComponent(encoded[1]);
  }

  const plain = /filename="?([^";]+)"?/i.exec(header);
  return plain ? plain[1] : null;
}

function fallbackFileName(job: JobView | undefined): string {
  if (!job) {
    return 'converted';
  }

  const dot = job.fileName.lastIndexOf('.');
  const baseName = dot > 0 ? job.fileName.slice(0, dot) : job.fileName;
  return `${baseName}.${job.targetFormat}`;
}

async function messageForBlobError(error: unknown): Promise<string> {
  if (error instanceof HttpErrorResponse && error.error instanceof Blob) {
    try {
      const problem = JSON.parse(await error.error.text()) as { code?: string };
      return messageForCode(problem.code);
    } catch {
      // Not JSON: fall through to the generic message.
    }
  }

  return messageForHttpError(error);
}
