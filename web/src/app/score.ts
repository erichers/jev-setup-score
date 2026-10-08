import { HttpErrorResponse } from '@angular/common/http';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Component, OnDestroy, effect, inject, signal } from '@angular/core';
import { Title } from '@angular/platform-browser';
import { ActivatedRoute, Router } from '@angular/router';
import { type ECharts, type EChartsCoreOption, init } from 'echarts/core';
import { combineLatest } from 'rxjs';
import { ApiService } from './api.service';
import { calibrationOption, equityOption, macdOption, palette, priceOption, rsiOption } from './charts';
import { Factor, HORIZONS, ScoreResponse } from './models';
import { CountDirective, RevealDirective, easeRise, reduceMotion } from './motion';
import { ThemeService } from './theme.service';

@Component({
  selector: 'app-score',
  imports: [RevealDirective, CountDirective],
  templateUrl: './score.html',
})
export class ScorePage implements OnDestroy {
  private readonly api = inject(ApiService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly title = inject(Title);
  private readonly themes = inject(ThemeService);
  private readonly charts = new Map<string, ECharts>();
  private request = 0;
  private resizeObserver?: ResizeObserver;
  private chartObservers: IntersectionObserver[] = [];
  private gaugeObserver?: IntersectionObserver;
  private barObserver?: IntersectionObserver;
  private timers: number[] = [];

  readonly horizons = HORIZONS;
  readonly data = signal<ScoreResponse | null>(null);
  readonly loading = signal(true);
  readonly error = signal('');
  readonly draft = signal('');
  readonly horizon = signal(10);
  readonly threshold = signal(60);
  readonly shown = signal(0);
  readonly barsReady = signal(false);
  readonly ticks = [0, 50, 100].map((score) => tick(score));
  private anim = 0;

  constructor() {
    combineLatest([this.route.paramMap, this.route.queryParamMap])
      .pipe(takeUntilDestroyed())
      .subscribe(([params, query]) => {
      const ticker = (params.get('ticker') ?? '').toUpperCase();
      const horizon = normalizeHorizon(query.get('horizon'));
      const threshold = normalizeThreshold(query.get('threshold'));
      this.draft.set(ticker);
      this.horizon.set(horizon);
      this.threshold.set(threshold);
      this.load(ticker, horizon, threshold);
    });

    effect(() => {
      this.themes.theme();
      const score = this.data();
      if (score) {
        queueMicrotask(() => this.paint(score));
      }
    });

    this.resizeObserver = new ResizeObserver(() => {
      for (const chart of this.charts.values()) {
        chart.resize();
      }
    });
  }

  ngOnDestroy(): void {
    cancelAnimationFrame(this.anim);
    this.gaugeObserver?.disconnect();
    this.barObserver?.disconnect();
    this.disconnectChartObservers();
    this.clearTimers();
    this.disposeCharts();
    this.resizeObserver?.disconnect();
  }

  onTicker(event: Event): void {
    this.draft.set((event.target as HTMLInputElement).value.toUpperCase());
  }

  setHorizon(horizon: number): void {
    this.horizon.set(horizon);
    this.submit();
  }

  setThreshold(event: Event): void {
    this.threshold.set(Number((event.target as HTMLInputElement).value));
    this.submit();
  }

  submit(event?: Event): void {
    event?.preventDefault();
    const ticker = this.draft().trim().toUpperCase();
    if (!/^[A-Z0-9.\-]{1,12}$/.test(ticker)) {
      this.error.set('Enter a ticker using letters, numbers, dots, or dashes.');
      this.data.set(null);
      return;
    }
    const current = (this.route.snapshot.paramMap.get('ticker') ?? '').toUpperCase();
    const horizon = normalizeHorizon(this.route.snapshot.queryParamMap.get('horizon'));
    const threshold = normalizeThreshold(this.route.snapshot.queryParamMap.get('threshold'));
    if (current === ticker && horizon === this.horizon() && threshold === this.threshold()) {
      this.load(ticker, this.horizon(), this.threshold());
      return;
    }
    void this.router.navigate(['/score', ticker], {
      queryParams: { horizon: this.horizon(), threshold: this.threshold() },
    });
  }

  pdfHref(): string {
    const fromServer = this.data()?.reportUrl;
    if (fromServer) return fromServer;
    const ticker = this.draft().trim().toUpperCase() || 'SPY';
    return `api/score/${encodeURIComponent(ticker)}/report.pdf?horizon=${this.horizon()}&threshold=${this.threshold()}`;
  }

  arc(score: number): { track: string; value: string } {
    const radius = 78;
    const circumference = 2 * Math.PI * radius;
    const sweep = circumference * 0.75;
    const filled = sweep * (Math.min(100, Math.max(0, score)) / 100);
    return {
      track: `${sweep} ${circumference - sweep}`,
      value: `${filled} ${circumference - filled}`,
    };
  }

  barStyle(factor: Factor, factors: Factor[]): Record<string, string> {
    const max = Math.max(...factors.map((item) => Math.abs(item.contribution)), 0.0001);
    const width = (Math.abs(factor.contribution) / max) * 50;
    const left = factor.contribution >= 0 ? 50 : 50 - width;
    const reduce = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
    if (!reduce && !this.barsReady()) {
      return { width: '0%', left: '50%' };
    }
    return { width: `${width}%`, left: `${left}%` };
  }

  barDelay(index: number): string {
    if (!this.barsReady() || reduceMotion()) return '0ms';
    return `${Math.min(index, 8) * 40}ms`;
  }

  pct(value: number, digits = 1): string {
    return (value * 100).toFixed(digits) + '%';
  }

  signed(value: number): string {
    const text = Math.abs(value).toFixed(2);
    if (value > 0) return '+' + text;
    if (value < 0) return '-' + text;
    return '0.00';
  }

  weight(value: number): string {
    const text = Math.abs(value).toFixed(3);
    if (value > 0) return '+' + text;
    if (value < 0) return '-' + text;
    return '0.000';
  }

  multiple(value: number): string {
    return value.toFixed(2) + 'x';
  }

  fixed(value: number, digits = 3): string {
    return value.toFixed(digits);
  }

  private load(ticker: string, horizon: number, threshold: number): void {
    if (!ticker) {
      this.loading.set(false);
      this.error.set('Enter a ticker such as SPY.');
      return;
    }
    const id = ++this.request;
    this.loading.set(true);
    this.error.set('');
    this.barsReady.set(false);
    this.shown.set(0);
    cancelAnimationFrame(this.anim);
    this.gaugeObserver?.disconnect();
    this.barObserver?.disconnect();
    this.disconnectChartObservers();
    this.clearTimers();
    this.disposeCharts();
    this.data.set(null);
    this.title.setTitle(`${ticker} setup score · Jev`);
    this.api.score(ticker, horizon, threshold).subscribe({
      next: (score) => {
        if (id !== this.request) return;
        this.data.set(score);
        this.loading.set(false);
        setTimeout(() => {
          if (id !== this.request) return;
          this.paint(score);
          this.armGauge(score.score);
          this.armBars();
        }, 0);
      },
      error: (err: HttpErrorResponse) => {
        if (id !== this.request) return;
        const message = typeof err.error?.message === 'string' ? err.error.message : 'The score request failed.';
        this.error.set(message);
        this.loading.set(false);
      },
    });
  }

  private armGauge(target: number): void {
    this.gaugeObserver?.disconnect();
    cancelAnimationFrame(this.anim);
    if (reduceMotion()) {
      this.shown.set(target);
      return;
    }
    const el = document.querySelector('.gauge');
    if (!el) {
      this.playScore(target);
      return;
    }
    this.gaugeObserver = new IntersectionObserver(
      (entries) => {
        if (entries.some((entry) => entry.isIntersecting)) {
          this.gaugeObserver?.disconnect();
          this.playScore(target);
        }
      },
      { threshold: 0.35 },
    );
    this.gaugeObserver.observe(el);
  }

  private armBars(): void {
    this.barObserver?.disconnect();
    if (reduceMotion()) {
      this.barsReady.set(true);
      return;
    }
    const el = document.querySelector('.factor-list');
    if (!el) {
      this.barsReady.set(true);
      return;
    }
    this.barObserver = new IntersectionObserver(
      (entries) => {
        if (entries.some((entry) => entry.isIntersecting)) {
          this.barsReady.set(true);
          this.barObserver?.disconnect();
        }
      },
      { threshold: 0.2 },
    );
    this.barObserver.observe(el);
  }

  private playScore(target: number): void {
    cancelAnimationFrame(this.anim);
    const start = performance.now();
    const duration = 360;
    const step = (now: number) => {
      const t = Math.min(1, (now - start) / duration);
      this.shown.set(Math.round(target * easeRise(t)));
      if (t < 1) this.anim = requestAnimationFrame(step);
    };
    this.shown.set(0);
    this.anim = requestAnimationFrame(step);
  }

  private paint(score: ScoreResponse): void {
    this.clearTimers();
    this.disconnectChartObservers();
    const colors = palette(this.themes.theme());
    const reduce = reduceMotion();
    this.mountWhenVisible('price-chart', priceOption(score.chart, colors));
    this.mountWhenVisible('rsi-chart', rsiOption(score.chart, colors), () => this.fadeArea('rsi-chart', 'rsi', colors.accent, reduce));
    this.mountWhenVisible('macd-chart', macdOption(score.chart, colors));
    this.mountWhenVisible('calibration-chart', calibrationOption(score, colors));
    this.mountWhenVisible('equity-chart', equityOption(score, colors, reduce), () => this.fadeArea('equity-chart', 'strategy', colors.accent, reduce));
    const root = document.getElementById('price-chart');
    if (root && this.resizeObserver) this.resizeObserver.observe(root);
  }

  private fadeArea(id: string, seriesId: string, color: string, reduce: boolean): void {
    if (reduce) return;
    this.later(() => {
      const chart = this.charts.get(id);
      if (!chart || chart.isDisposed()) return;
      chart.setOption({ series: [{ id: seriesId, areaStyle: { color, opacity: 0.14 } }] });
    }, 380);
  }

  private mountWhenVisible(id: string, option: EChartsCoreOption, after?: () => void): void {
    const element = document.getElementById(id);
    if (!element) return;
    const run = () => {
      this.mount(id, option);
      after?.();
    };
    if (reduceMotion()) {
      run();
      return;
    }
    const observer = new IntersectionObserver(
      (entries) => {
        if (entries.some((entry) => entry.isIntersecting)) {
          observer.disconnect();
          run();
        }
      },
      { threshold: 0.2 },
    );
    observer.observe(element);
    this.chartObservers.push(observer);
  }

  private later(fn: () => void, ms: number): void {
    this.timers.push(window.setTimeout(fn, ms));
  }

  private clearTimers(): void {
    for (const id of this.timers) window.clearTimeout(id);
    this.timers = [];
  }

  private disconnectChartObservers(): void {
    for (const observer of this.chartObservers) observer.disconnect();
    this.chartObservers = [];
  }

  private mount(id: string, option: EChartsCoreOption): void {
    const element = document.getElementById(id);
    if (!element) return;
    let chart = this.charts.get(id);
    if (!chart || chart.isDisposed()) {
      chart = init(element);
      this.charts.set(id, chart);
    }
    chart.setOption(option, true);
    chart.resize();
  }

  private disposeCharts(): void {
    for (const chart of this.charts.values()) {
      chart.dispose();
    }
    this.charts.clear();
  }
}

function normalizeHorizon(value: string | null): number {
  const horizon = Number(value ?? 10);
  return HORIZONS.includes(horizon as 5 | 10 | 20) ? horizon : 10;
}

function normalizeThreshold(value: string | null): number {
  const threshold = Number(value ?? 60);
  if (!Number.isFinite(threshold)) return 60;
  return Math.min(90, Math.max(50, Math.round(threshold / 5) * 5));
}

function tick(score: number) {
  const rad = ((135 + 270 * (score / 100)) * Math.PI) / 180;
  const cx = 120;
  const cy = 118;
  return {
    score,
    x1: cx + 86 * Math.cos(rad),
    y1: cy + 86 * Math.sin(rad),
    x2: cx + 94 * Math.cos(rad),
    y2: cy + 94 * Math.sin(rad),
    lx: cx + 108 * Math.cos(rad),
    ly: cy + 108 * Math.sin(rad) + 4,
  };
}
