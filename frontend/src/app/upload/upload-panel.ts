import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';

import { ConversionApi } from '../core/conversion-api';
import { formatSize } from '../core/format-size';
import { JobsStore } from '../core/jobs-store';
import { SupportedFormat } from '../core/models';

/**
 * Picking a file (drop or file dialog), picking a target format, starting the conversion.
 * The list of allowed targets comes from GET /formats: the server decides what is possible,
 * the UI only stops offering what the server would reject anyway.
 */
@Component({
  selector: 'app-upload-panel',
  templateUrl: './upload-panel.html',
  styleUrl: './upload-panel.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class UploadPanel {
  private readonly api = inject(ConversionApi);
  private readonly store = inject(JobsStore);

  protected readonly formats = signal<SupportedFormat[] | null>(null);
  protected readonly formatsFailed = signal(false);

  protected readonly file = signal<File | null>(null);
  protected readonly targetFormat = signal<string | null>(null);
  protected readonly dragging = signal(false);

  /** Guessed from the extension, only to offer the right targets. The server checks the content. */
  protected readonly sourceFormat = computed(() => {
    const name = this.file()?.name ?? '';
    const dot = name.lastIndexOf('.');
    const extension = dot >= 0 ? name.slice(dot + 1).toLowerCase() : '';
    return extension === 'jpeg' ? 'jpg' : extension;
  });

  protected readonly targets = computed(
    () => this.formats()?.find((format) => format.source === this.sourceFormat())?.targets ?? [],
  );

  protected readonly supportedList = computed(() =>
    (this.formats() ?? []).map((format) => format.source.toUpperCase()).join(', '),
  );

  protected readonly convertLabel = computed(() => {
    const target = this.targetFormat();
    return target ? `Convert to ${target.toUpperCase()}` : 'Choose a format';
  });

  protected readonly formatSize = formatSize;

  constructor() {
    this.loadFormats();
  }

  protected loadFormats(): void {
    this.formatsFailed.set(false);
    this.api.getFormats().subscribe({
      next: (response) => this.formats.set(response.formats),
      error: () => this.formatsFailed.set(true),
    });
  }

  protected onDragOver(event: DragEvent): void {
    event.preventDefault(); // without this the browser would open the file instead of dropping it here
    this.dragging.set(true);
  }

  protected onDragLeave(event: DragEvent): void {
    // Moving over a child element (the button, the text) also fires dragleave: ignore that.
    const zone = event.currentTarget as HTMLElement;
    if (!zone.contains(event.relatedTarget as Node | null)) {
      this.dragging.set(false);
    }
  }

  protected onDrop(event: DragEvent): void {
    event.preventDefault();
    this.dragging.set(false);

    const dropped = event.dataTransfer?.files.item(0);
    if (dropped) {
      this.select(dropped);
    }
  }

  protected onFileChosen(input: HTMLInputElement): void {
    const chosen = input.files?.item(0);
    if (chosen) {
      this.select(chosen);
    }

    // Lets the same file be chosen again later.
    input.value = '';
  }

  protected convert(): void {
    const file = this.file();
    const target = this.targetFormat();
    if (!file || !target) {
      return;
    }

    this.store.start(file, target);
    this.file.set(null);
    this.targetFormat.set(null);
  }

  private select(file: File): void {
    this.file.set(file);
    this.targetFormat.set(null);
  }
}
