import { ChangeDetectionStrategy, Component, inject } from '@angular/core';

import { JobsStore } from './core/jobs-store';
import { JobCard } from './jobs/job-card';
import { UploadPanel } from './upload/upload-panel';

@Component({
  selector: 'app-root',
  imports: [UploadPanel, JobCard],
  templateUrl: './app.html',
  styleUrl: './app.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { '(window:beforeunload)': 'warnBeforeLeaving($event)' },
})
export class App {
  protected readonly store = inject(JobsStore);

  protected warnBeforeLeaving(event: BeforeUnloadEvent): void {
    // Access tokens live only in memory: leaving the page loses access to these files.
    if (this.store.jobs().length > 0) {
      event.preventDefault();
    }
  }
}
