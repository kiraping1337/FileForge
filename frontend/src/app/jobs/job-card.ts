import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { JobView } from '../core/jobs-store';

/** Temporary minimal version: you build the real card in step 4. */
@Component({
  selector: 'app-job-card',
  templateUrl: './job-card.html',
  styleUrl: './job-card.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class JobCard {
  readonly job = input.required<JobView>();
}
