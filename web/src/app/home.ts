import { Component, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { ApiService } from './api.service';
import { EXAMPLE_TICKERS, ExampleOutcome, HORIZONS, HistoryItem, TickerSummary } from './models';

@Component({
  selector: 'app-home',
  imports: [RouterLink],
  templateUrl: './home.html',
})
export class HomePage {
  private readonly api = inject(ApiService);
  private readonly router = inject(Router);

  readonly horizons = HORIZONS;
  readonly exampleTickers = EXAMPLE_TICKERS;
  readonly ticker = signal('SPY');
  readonly horizon = signal<number>(10);
  readonly tickers = signal<TickerSummary[]>([]);
  readonly examples = signal<ExampleOutcome[]>([]);
  readonly history = signal<HistoryItem[]>([]);
  readonly loading = signal(true);
  readonly examplesReady = signal(false);
  readonly error = signal('');
  readonly exampleError = signal('');

  constructor() {
    this.api.tickers().subscribe({
      next: (rows) => {
        this.tickers.set(rows);
        this.loading.set(false);
      },
      error: () => {
        this.error.set('The ticker list did not load. Start the API and refresh.');
        this.loading.set(false);
      },
    });
    this.api.examples().subscribe({
      next: (rows) => {
        this.examples.set(rows);
        this.examplesReady.set(true);
      },
      error: () => {
        this.exampleError.set('Past scores are still training. Refresh in a moment.');
        this.examplesReady.set(true);
      },
    });
    this.api.history(6).subscribe({
      next: (rows) => this.history.set(rows),
      error: () => this.history.set([]),
    });
  }

  onTicker(event: Event): void {
    const value = (event.target as HTMLInputElement).value.toUpperCase();
    this.ticker.set(value);
  }

  setHorizon(horizon: number): void {
    this.horizon.set(horizon);
  }

  go(event: Event): void {
    event.preventDefault();
    const ticker = this.ticker().trim().toUpperCase();
    if (!/^[A-Z0-9.\-]{1,12}$/.test(ticker)) {
      this.error.set('Enter a ticker using letters, numbers, dots, or dashes.');
      return;
    }
    this.error.set('');
    void this.router.navigate(['/score', ticker], { queryParams: { horizon: this.horizon(), threshold: 60 } });
  }

  money(value: number): string {
    return value.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
  }

  change(value: number): string {
    const text = Math.abs(value).toFixed(2) + '%';
    return value >= 0 ? 'Up ' + text : 'Down ' + text;
  }

  move(value: number): string {
    return Math.abs(value * 100).toFixed(1) + '%';
  }
}
