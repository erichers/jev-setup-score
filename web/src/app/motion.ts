import { AfterViewInit, Directive, ElementRef, Input, OnChanges, OnDestroy, inject } from '@angular/core';

export function reduceMotion(): boolean {
  return window.matchMedia('(prefers-reduced-motion: reduce)').matches;
}

/** cubic-bezier(.2, .7, .2, 1) evaluated at t. */
export function easeRise(t: number): number {
  const x1 = 0.2;
  const y1 = 0.7;
  const x2 = 0.2;
  const y2 = 1;
  let u = t;
  for (let i = 0; i < 6; i++) {
    const omu = 1 - u;
    const x = 3 * omu * omu * u * x1 + 3 * omu * u * u * x2 + u * u * u;
    const dx = 3 * omu * omu * x1 + 6 * omu * u * (x2 - x1) + 3 * u * u * (1 - x2);
    u = Math.min(1, Math.max(0, u - (x - t) / (dx || 1)));
  }
  const omu = 1 - u;
  return 3 * omu * omu * u * y1 + 3 * omu * u * u * y2 + u * u * u;
}

@Directive({ selector: '[appReveal]' })
export class RevealDirective implements AfterViewInit, OnDestroy {
  private readonly el = inject(ElementRef<HTMLElement>);
  private observer?: IntersectionObserver;

  @Input() appReveal: number | string = 0;
  @Input() revealSelf = true;

  ngAfterViewInit(): void {
    const node = this.el.nativeElement;
    const index = Number(this.appReveal);
    const safe = Number.isFinite(index) ? index : 0;
    node.style.setProperty('--delay', `${Math.min(Math.max(safe, 0), 8) * 50}ms`);
    if (this.revealSelf) {
      node.classList.add('reveal');
    } else {
      node.classList.remove('reveal');
      node.classList.add('reveal-group');
    }
    if (reduceMotion()) {
      node.classList.add('in');
      return;
    }
    this.observer = new IntersectionObserver(
      (entries) => {
        if (entries.some((entry) => entry.isIntersecting)) {
          node.classList.add('in');
          this.observer?.disconnect();
        }
      },
      { threshold: 0.05 },
    );
    this.observer.observe(node);
  }

  ngOnDestroy(): void {
    this.observer?.disconnect();
  }
}

@Directive({ selector: '[appCount]' })
export class CountDirective implements AfterViewInit, OnChanges, OnDestroy {
  private readonly el = inject(ElementRef<HTMLElement>);
  private observer?: IntersectionObserver;
  private raf = 0;
  private seen = false;
  private viewReady = false;

  @Input() appCount = 0;
  @Input() countDigits = 0;
  @Input() countPrefix = '';
  @Input() countSuffix = '';

  ngOnChanges(): void {
    if (this.viewReady) this.kick();
  }

  ngAfterViewInit(): void {
    this.viewReady = true;
    this.kick();
  }

  ngOnDestroy(): void {
    cancelAnimationFrame(this.raf);
    this.observer?.disconnect();
  }

  private kick(): void {
    const target = Number(this.appCount);
    const value = Number.isFinite(target) ? target : 0;
    if (reduceMotion()) {
      this.el.nativeElement.textContent = this.format(value);
      return;
    }
    if (this.seen) {
      this.run(value);
      return;
    }
    this.el.nativeElement.textContent = this.format(0);
    this.observer?.disconnect();
    this.observer = new IntersectionObserver(
      (entries) => {
        if (entries.some((entry) => entry.isIntersecting)) {
          this.seen = true;
          this.observer?.disconnect();
          this.run(value);
        }
      },
      { threshold: 0.4 },
    );
    this.observer.observe(this.el.nativeElement);
  }

  private run(target: number): void {
    cancelAnimationFrame(this.raf);
    const start = performance.now();
    const duration = 360;
    const step = (now: number) => {
      const t = Math.min(1, (now - start) / duration);
      const current = t === 1 ? target : target * easeRise(t);
      this.el.nativeElement.textContent = this.format(current);
      if (t < 1) this.raf = requestAnimationFrame(step);
    };
    this.raf = requestAnimationFrame(step);
  }

  private format(value: number): string {
    return `${this.countPrefix}${value.toFixed(this.countDigits)}${this.countSuffix}`;
  }
}
