import { HttpClient, HttpEvent, HttpHeaders, HttpResponse } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { ConversionStatusResponse, CreateConversionResponse, FormatsResponse } from './models';

/**
 * Thin wrapper over the REST API. No state and no error handling here: that is the store's job.
 * The base URL is relative: in development `ng serve` proxies /api to the backend,
 * in Docker a reverse proxy will do the same.
 */
@Injectable({ providedIn: 'root' })
export class ConversionApi {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/v1';

  getFormats(): Observable<FormatsResponse> {
    return this.http.get<FormatsResponse>(`${this.baseUrl}/formats`);
  }

  /** Emits upload progress events, then the response. */
  createConversion(file: File, targetFormat: string): Observable<HttpEvent<CreateConversionResponse>> {
    const form = new FormData();
    form.append('file', file);
    form.append('targetFormat', targetFormat);

    return this.http.post<CreateConversionResponse>(`${this.baseUrl}/conversions`, form, {
      reportProgress: true,
      observe: 'events',
    });
  }

  getStatus(jobId: string, token: string): Observable<ConversionStatusResponse> {
    return this.http.get<ConversionStatusResponse>(`${this.baseUrl}/conversions/${jobId}`, {
      headers: this.tokenHeader(token),
    });
  }

  /** The whole response: the file name is in the Content-Disposition header. */
  download(jobId: string, token: string): Observable<HttpResponse<Blob>> {
    return this.http.get(`${this.baseUrl}/conversions/${jobId}/download`, {
      headers: this.tokenHeader(token),
      responseType: 'blob',
      observe: 'response',
    });
  }

  /** The whole response: 202 means "cancellation requested", 204 means "deleted". */
  delete(jobId: string, token: string): Observable<HttpResponse<unknown>> {
    return this.http.delete(`${this.baseUrl}/conversions/${jobId}`, {
      headers: this.tokenHeader(token),
      observe: 'response',
    });
  }

  // The token travels only in a header: never in the URL, where it would end up in logs and history.
  private tokenHeader(token: string): HttpHeaders {
    return new HttpHeaders({ 'X-Job-Token': token });
  }
}
