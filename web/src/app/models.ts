export interface TickerSummary {
  symbol: string;
  name: string;
  kind: string;
  asOf: string;
  last: number;
  changePercent: number;
}

export interface ExampleOutcome {
  symbol: string;
  name: string;
  featureDate: string;
  labelDate: string;
  score: number;
  probability: number;
  forwardReturn: number;
  up: boolean;
  outcome: string;
}

export interface HistoryItem {
  id: number;
  symbol: string;
  horizon: number;
  threshold: number;
  score: number;
  probability: number;
  asOf: string;
  dataSource: string;
  summary: string;
  createdUtc: string;
}

export interface Factor {
  key: string;
  label: string;
  display: string;
  reading: string;
  coefficient: number;
  contribution: number;
}

export interface Coefficient {
  key: string;
  label: string;
  weight: number;
  meaning: string;
}

export interface ChartPoint {
  date: string;
  open: number;
  high: number;
  low: number;
  close: number;
  volume: number;
  ma20: number | null;
  ma50: number | null;
  ma200: number | null;
  rsi: number | null;
  macd: number | null;
  macdSignal: number | null;
  macdHistogram: number | null;
}

export interface CalibrationBucket {
  label: string;
  meanPredicted: number;
  actualRate: number;
  count: number;
}

export interface EquityPoint {
  date: string;
  strategy: number;
  buyHold: number;
}

export interface Backtest {
  foldCount: number;
  predictionCount: number;
  tradeCount: number;
  hitRate: number;
  brier: number;
  baseRate: number;
  baseRateBrier: number;
  honesty: string;
  calibration: CalibrationBucket[];
  equity: EquityPoint[];
  strategyMultiple: number;
  buyHoldMultiple: number;
}

export interface ScoreResponse {
  ticker: string;
  name: string;
  horizon: number;
  threshold: number;
  asOf: string;
  dataSource: 'live' | 'cached' | string;
  dataNote: string;
  score: number;
  probability: number;
  summary: string;
  trainingRows: number;
  modelConverged: boolean;
  factors: Factor[];
  coefficients: Coefficient[];
  chart: ChartPoint[];
  backtest: Backtest;
  reportUrl: string;
}

export const HORIZONS = [5, 10, 20] as const;

export const EXAMPLE_TICKERS = [
  'SPY',
  'QQQ',
  'AAPL',
  'NVDA',
  'TSLA',
  'MSFT',
  'AMZN',
  'GOOGL',
  'META',
  'AMD',
  'JPM',
  'NFLX',
  'AVGO',
  'COST',
  'WMT',
] as const;
