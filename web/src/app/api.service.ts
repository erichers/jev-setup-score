import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { ExampleOutcome, HistoryItem, ScoreResponse, TickerSummary } from './models';

@Injectable({ providedIn: 'root' })
export class ApiService {
  private readonly http = inject(HttpClient);

  tickers() {
    return this.http.get<TickerSummary[]>('/api/tickers');
  }

  examples() {
    return this.http.get<ExampleOutcome[]>('/api/examples');
  }

  history(limit = 8) {
    const params = new HttpParams().set('limit', String(limit));
    return this.http.get<HistoryItem[]>('/api/history', { params });
  }

  score(ticker: string, horizon: number, threshold: number) {
    const params = new HttpParams().set('horizon', String(horizon)).set('threshold', String(threshold));
    return this.http.get<ScoreResponse>(`/api/score/${encodeURIComponent(ticker)}`, { params });
  }
}
