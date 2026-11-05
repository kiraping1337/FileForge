import { provideHttpClient, withXhr } from '@angular/common/http';
import { ApplicationConfig, provideBrowserGlobalErrorListeners } from '@angular/core';

export const appConfig: ApplicationConfig = {
  // withXhr: fetch reports no upload progress.
  providers: [provideBrowserGlobalErrorListeners(), provideHttpClient(withXhr())],
};
