import { BarChart, CandlestickChart, LineChart } from 'echarts/charts';
import { GridComponent, LegendComponent, MarkLineComponent, TooltipComponent } from 'echarts/components';
import { type EChartsCoreOption, use } from 'echarts/core';
import { CanvasRenderer } from 'echarts/renderers';
import { ChartPoint, ScoreResponse } from './models';

use([
  CandlestickChart,
  LineChart,
  BarChart,
  GridComponent,
  TooltipComponent,
  LegendComponent,
  MarkLineComponent,
  CanvasRenderer,
]);

export interface ChartPalette {
  text: string;
  muted: string;
  line: string;
  up: string;
  down: string;
  ma20: string;
  ma50: string;
  ma200: string;
  accent: string;
}

export function palette(theme: 'light' | 'dark'): ChartPalette {
  if (theme === 'dark') {
    return {
      text: '#f2f2ee',
      muted: '#a1a1a1',
      line: 'rgba(242, 242, 238, 0.14)',
      up: '#f2f2ee',
      down: '#6e6e6c',
      ma20: '#cefb31',
      ma50: '#8a8a8a',
      ma200: '#5c5c5c',
      accent: '#cefb31',
    };
  }
  return {
    text: '#141414',
    muted: '#5c5c5c',
    line: 'rgba(20, 20, 20, 0.12)',
    up: '#141414',
    down: '#a3a3a1',
    ma20: '#4f7a00',
    ma50: '#8a8a8a',
    ma200: '#b5b5b5',
    accent: '#4f7a00',
  };
}

const MONTHS = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];
function shortMonth(value: string): string {
  const m = /^(\d{4})-(\d{2})/.exec(String(value));
  return m ? `${MONTHS[Number(m[2]) - 1]} ${m[1].slice(2)}` : String(value);
}

function reduceMotionChart(): boolean {
  return window.matchMedia('(prefers-reduced-motion: reduce)').matches;
}

function base(colors: ChartPalette, dates: string[]): EChartsCoreOption {
  const reduce = reduceMotionChart();
  return {
    backgroundColor: 'transparent',
    animation: !reduce,
    animationDuration: reduce ? 0 : 360,
    animationEasing: 'cubicOut',
    textStyle: { fontFamily: 'Inter, sans-serif', color: colors.muted },
    grid: { left: 8, right: 12, top: 32, bottom: 8, containLabel: true },
    tooltip: {
      trigger: 'axis',
      backgroundColor: colors.text,
      borderWidth: 0,
      textStyle: { color: colors.text === '#141414' ? '#f4f4f1' : '#141414', fontFamily: 'Inter, sans-serif', fontSize: 12 },
    },
    xAxis: {
      type: 'category',
      data: dates,
      axisLine: { lineStyle: { color: colors.line } },
      axisTick: { show: false },
      axisLabel: {
        color: colors.muted,
        fontSize: 12,
        margin: 8,
        hideOverlap: true,
        interval: Math.max(0, Math.ceil(dates.length / 4) - 1),
        formatter: (value: string) => shortMonth(value),
      },
    },
  };
}

function yAxis(colors: ChartPalette, scale = false) {
  return {
    type: 'value' as const,
    scale,
    axisLabel: { color: colors.muted, fontSize: 12 },
    splitLine: { lineStyle: { color: colors.line } },
  };
}

export function priceOption(points: ChartPoint[], colors: ChartPalette): EChartsCoreOption {
  const dates = points.map((point) => point.date);
  return {
    ...base(colors, dates),
    legend: {
      data: ['Price', '20-day', '50-day', '200-day'],
      textStyle: { color: colors.muted, fontFamily: 'Inter, sans-serif', fontSize: 12 },
      top: 0,
    },
    yAxis: yAxis(colors, true),
    series: [
      {
        name: 'Price',
        type: 'candlestick',
        data: points.map((point) => [point.open, point.close, point.low, point.high]),
        itemStyle: {
          color: colors.up,
          color0: colors.down,
          borderColor: colors.up,
          borderColor0: colors.down,
        },
      },
      line('20-day', points.map((point) => point.ma20), colors.ma20, 0),
      line('50-day', points.map((point) => point.ma50), colors.ma50, 50),
      line('200-day', points.map((point) => point.ma200), colors.ma200, 100),
    ],
  };
}

export function rsiOption(points: ChartPoint[], colors: ChartPalette): EChartsCoreOption {
  const dates = points.map((point) => point.date);
  return {
    ...base(colors, dates),
    // Room on the right for the 30 and 70 guide labels (label width + 8).
    grid: { left: 8, right: 32, top: 32, bottom: 8, containLabel: true },
    yAxis: { ...yAxis(colors), min: 0, max: 100 },
    series: [
      {
        id: 'rsi',
        name: 'RSI',
        type: 'line',
        data: points.map((point) => point.rsi),
        showSymbol: false,
        lineStyle: { width: 1.6, color: colors.accent },
        itemStyle: { color: colors.accent },
        areaStyle: { color: colors.accent, opacity: reduceMotionChart() ? 0.14 : 0 },
        markLine: {
          symbol: 'none',
          label: { color: colors.muted, fontSize: 12 },
          lineStyle: { color: colors.muted, type: 'dashed' },
          data: [{ yAxis: 30 }, { yAxis: 70 }],
        },
      },
    ],
  };
}

export function macdOption(points: ChartPoint[], colors: ChartPalette): EChartsCoreOption {
  const dates = points.map((point) => point.date);
  return {
    ...base(colors, dates),
    legend: {
      data: ['MACD', 'Signal', 'Histogram'],
      textStyle: { color: colors.muted, fontFamily: 'Inter, sans-serif', fontSize: 12 },
      top: 0,
    },
    yAxis: yAxis(colors, true),
    series: [
      {
        name: 'Histogram',
        type: 'bar',
        // Series color drives the legend swatch; bars keep ink up / gray down.
        itemStyle: { color: colors.up },
        data: points.map((point) => ({
          value: point.macdHistogram,
          itemStyle: { color: (point.macdHistogram ?? 0) >= 0 ? colors.up : colors.down },
        })),
      },
      line('MACD', points.map((point) => point.macd), colors.accent),
      line('Signal', points.map((point) => point.macdSignal), colors.ma200),
    ],
  };
}

export function calibrationOption(score: ScoreResponse, colors: ChartPalette): EChartsCoreOption {
  const buckets = score.backtest.calibration.filter((bucket) => bucket.count > 0);
  const labels = buckets.map((bucket) => bucket.label);
  return {
    ...base(colors, labels),
    legend: {
      data: ['Predicted', 'Actual'],
      textStyle: { color: colors.muted, fontFamily: 'Inter, sans-serif', fontSize: 12 },
      top: 0,
    },
    yAxis: { ...yAxis(colors), min: 0, max: 1 },
    series: [
      {
        name: 'Predicted',
        type: 'bar',
        data: buckets.map((bucket) => bucket.meanPredicted),
        itemStyle: { color: colors.accent },
      },
      {
        name: 'Actual',
        type: 'bar',
        data: buckets.map((bucket) => bucket.actualRate),
        itemStyle: { color: colors.ma50 },
      },
    ],
  };
}

export function equityOption(score: ScoreResponse, colors: ChartPalette, showArea: boolean): EChartsCoreOption {
  const dates = score.backtest.equity.map((point) => point.date);
  return {
    ...base(colors, dates),
    legend: {
      data: ['Score rule', 'Buy and hold'],
      textStyle: { color: colors.muted, fontFamily: 'Inter, sans-serif', fontSize: 12 },
      top: 0,
    },
    yAxis: yAxis(colors, true),
    series: [
      {
        ...line('Score rule', score.backtest.equity.map((point) => point.strategy), colors.accent, 0),
        id: 'strategy',
        areaStyle: { color: colors.accent, opacity: showArea ? 0.14 : 0 },
      },
      line('Buy and hold', score.backtest.equity.map((point) => point.buyHold), colors.ma200, 80),
    ],
  };
}

function line(name: string, data: Array<number | null>, color: string, delay = 0) {
  return {
    name,
    type: 'line' as const,
    data,
    showSymbol: false,
    lineStyle: { width: 1.5, color },
    itemStyle: { color },
    animationDuration: 360,
    animationEasing: 'cubicOut' as const,
    animationDelay: delay,
  };
}
