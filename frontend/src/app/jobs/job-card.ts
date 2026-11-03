import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';

import { formatSize } from '../core/format-size';
import { JobsStore, JobView } from '../core/jobs-store';

@Component({
  selector: 'app-job-card',
  imports: [DatePipe],
  templateUrl: './job-card.html',
  styleUrl: './job-card.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class JobCard {
  readonly job = input.required<JobView>();

  private readonly store = inject(JobsStore);

  protected readonly formatSize = formatSize;

  protected readonly formatsText = computed(() => {
    const job = this.job();
    const target = job.targetFormat.toUpperCase();
    return job.sourceFormat ? `${job.sourceFormat.toUpperCase()} to ${target}` : `to ${target}`;
  });

  protected readonly statusText = computed(() => {
    const job = this.job();
    switch (job.phase) {
      case 'Uploading':
        return 'Uploading';
      case 'Pending':
        return 'Waiting in queue';
      case 'Processing':
        return job.cancelling ? 'Cancelling' : 'Converting';
      case 'Completed':
        return 'Ready';
      case 'Failed':
        return 'Failed';
      case 'Cancelled':
        return 'Cancelled';
      case 'Expired':
        return 'Expired';
    }
  });

  protected readonly actionLabel = computed(() => {
    const job = this.job();
    switch (job.phase) {
      case 'Uploading':
        return 'Cancel upload';
      case 'Pending':
      case 'Processing':
        return 'Cancel';
      default:
        return 'Remove';
    }
  });

  protected download(): void {
    this.store.download(this.job().id);
  }

  protected cancelOrRemove(): void {
    this.store.cancelOrRemove(this.job().id);
  }

  protected checkAgain(): void {
    this.store.checkAgain(this.job().id);
  }
}
