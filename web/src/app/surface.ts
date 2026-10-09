import { AfterViewInit, Component, ElementRef, OnDestroy, ViewChild, computed, effect, inject, signal } from '@angular/core';
import { RevealDirective } from './motion';
import type { SurfaceHandle, SurfaceState } from './surface-scene';
import { ThemeService } from './theme.service';

@Component({
  selector: 'app-surface',
  imports: [RevealDirective],
  templateUrl: './surface.html',
})
export class SurfaceView implements AfterViewInit, OnDestroy {
  private readonly themes = inject(ThemeService);
  /** Reduced motion: the same scene at its resting camera with no auto-rotate. Rotate still works (user-started). */
  readonly reduced = signal(window.matchMedia('(prefers-reduced-motion: reduce)').matches);
  /** loading, live (WebGL up), lost (context lost, still shown), fallback (no WebGL, still shown). */
  readonly state = signal<SurfaceState | 'loading'>('loading');
  readonly ready = computed(() => this.state() === 'live');
  /** The still of the same scene is showing (no WebGL, or the context was lost). */
  readonly fallback = computed(() => this.state() === 'lost' || this.state() === 'fallback');
  /** Drag-to-rotate is off until Rotate is pressed, so a swipe over the surface scrolls the page. */
  readonly orbit = signal(false);
  private setOrbit?: (on: boolean) => void;

  toggleOrbit(): void {
    const next = !this.orbit();
    this.orbit.set(next);
    this.setOrbit?.(next);
  }

  @ViewChild('host') host?: ElementRef<HTMLElement>;
  @ViewChild('still') still?: ElementRef<SVGSVGElement>;

  private view?: SurfaceHandle;
  private destroyed = false;
  private media?: MediaQueryList;
  private onMedia?: () => void;

  constructor() {
    effect(() => {
      this.themes.theme();
      this.view?.refresh();
    });
  }

  ngAfterViewInit(): void {
    this.media = window.matchMedia('(prefers-reduced-motion: reduce)');
    this.onMedia = () => {
      this.reduced.set(Boolean(this.media?.matches));
      this.mount();
    };
    this.media.addEventListener('change', this.onMedia);
    this.mount();
  }

  private mount(): void {
    this.view?.dispose();
    this.view = undefined;
    this.orbit.set(false);
    const el = this.host?.nativeElement;
    const svg = this.still?.nativeElement;
    if (!el || !svg) return;
    const spin = !this.reduced();
    void import('./surface-scene')
      .then(({ mountSurface }) => {
        if (this.destroyed) return;
        this.view = mountSurface(el, () => this.themes.theme(), (fn) => (this.setOrbit = fn), spin, svg, (state) => {
          if (state !== 'live') this.orbit.set(false);
          this.state.set(state);
        });
        if (this.state() === 'loading') this.state.set('live');
      })
      .catch(() => this.state.set('fallback'));
  }

  ngOnDestroy(): void {
    this.destroyed = true;
    this.view?.dispose();
    if (this.media && this.onMedia) this.media.removeEventListener('change', this.onMedia);
  }
}
