import { Injectable, inject } from '@angular/core';
import { SwUpdate, VersionReadyEvent } from '@angular/service-worker';
import { BehaviorSubject, filter } from 'rxjs';

interface BeforeInstallPromptEvent extends Event {
  prompt(): Promise<void>;
  userChoice: Promise<{ outcome: 'accepted' | 'dismissed' }>;
}

@Injectable({ providedIn: 'root' })
export class PwaService {
  private swUpdate = inject(SwUpdate);

  private installPromptEvent: BeforeInstallPromptEvent | null = null;
  readonly canInstall$ = new BehaviorSubject<boolean>(false);
  readonly updateAvailable$ = new BehaviorSubject<boolean>(false);

  constructor() {
    this.listenInstallPrompt();
    this.listenForUpdates();
  }

  private listenInstallPrompt(): void {
    window.addEventListener('beforeinstallprompt', (event: Event) => {
      event.preventDefault();
      this.installPromptEvent = event as BeforeInstallPromptEvent;
      this.canInstall$.next(true);
    });

    window.addEventListener('appinstalled', () => {
      this.installPromptEvent = null;
      this.canInstall$.next(false);
    });
  }

  private listenForUpdates(): void {
    if (!this.swUpdate.isEnabled) return;

    this.swUpdate.versionUpdates
      .pipe(filter((evt): evt is VersionReadyEvent => evt.type === 'VERSION_READY'))
      .subscribe(() => this.updateAvailable$.next(true));
  }

  async promptInstall(): Promise<boolean> {
    if (!this.installPromptEvent) return false;

    await this.installPromptEvent.prompt();
    const { outcome } = await this.installPromptEvent.userChoice;

    if (outcome === 'accepted') {
      this.installPromptEvent = null;
      this.canInstall$.next(false);
    }

    return outcome === 'accepted';
  }

  async applyUpdate(): Promise<void> {
    if (!this.swUpdate.isEnabled) return;
    await this.swUpdate.activateUpdate();
    window.location.reload();
  }
}
