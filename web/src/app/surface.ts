import { AfterViewInit, Component, ElementRef, OnDestroy, ViewChild, inject, signal } from '@angular/core';
import { RevealDirective } from './motion';
import { stillMesh, stillWalks } from './surface-math';
import { ThemeService } from './theme.service';

@Component({
  selector: 'app-surface',
  imports: [RevealDirective],
  templateUrl: './surface.html',
})
export class SurfaceView implements AfterViewInit, OnDestroy {
  private readonly themes = inject(ThemeService);
  readonly reduced = signal(window.matchMedia('(prefers-reduced-motion: reduce)').matches);
  readonly ready = signal(false);
  readonly mesh = stillMesh();
  readonly walks = stillWalks();

  @ViewChild('host') host?: ElementRef<HTMLElement>;

  private stop?: () => void;
  private destroyed = false;
  private media?: MediaQueryList;
  private onMedia?: () => void;

  ngAfterViewInit(): void {
    this.media = window.matchMedia('(prefers-reduced-motion: reduce)');
    this.onMedia = () => {
      if (!this.media?.matches) return;
      this.stop?.();
      this.stop = undefined;
      this.ready.set(false);
      this.reduced.set(true);
    };
    this.media.addEventListener('change', this.onMedia);
    if (this.reduced()) return;
    const el = this.host?.nativeElement;
    if (!el) return;
    void import('./surface-scene')
      .then(({ mountSurface }) => {
        if (this.destroyed || this.reduced()) return;
        this.stop = mountSurface(el, () => this.themes.theme());
        this.ready.set(true);
      })
      .catch(() => {
        this.ready.set(false);
      });
  }

  ngOnDestroy(): void {
    this.destroyed = true;
    this.stop?.();
    if (this.media && this.onMedia) this.media.removeEventListener('change', this.onMedia);
  }
}
