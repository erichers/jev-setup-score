export function surfaceHeight(x: number, z: number): number {
  const logit = 1.35 * x + 0.75 * z;
  return 1 / (1 + Math.exp(-logit));
}

function project(x: number, z: number): [number, number] {
  const y = surfaceHeight(x, z);
  const sx = 320 + (x - z) * 62;
  const sy = 268 + (x + z) * 26 - y * 168;
  return [sx, sy];
}

function line(points: Array<[number, number]>): string {
  return points
    .map(([x, y], index) => `${index ? 'L' : 'M'}${x.toFixed(1)} ${y.toFixed(1)}`)
    .join('');
}

export function stillMesh(): string[] {
  const paths: string[] = [];
  const n = 10;
  for (let i = 0; i <= n; i++) {
    const x = -2 + (4 * i) / n;
    const points: Array<[number, number]> = [];
    for (let j = 0; j <= n; j++) {
      const z = -2 + (4 * j) / n;
      points.push(project(x, z));
    }
    paths.push(line(points));
  }
  for (let j = 0; j <= n; j++) {
    const z = -2 + (4 * j) / n;
    const points: Array<[number, number]> = [];
    for (let i = 0; i <= n; i++) {
      const x = -2 + (4 * i) / n;
      points.push(project(x, z));
    }
    paths.push(line(points));
  }
  return paths;
}

function mulberry32(seed: number): () => number {
  let state = seed;
  return () => {
    state |= 0;
    state = (state + 0x6d2b79f5) | 0;
    let t = Math.imul(state ^ (state >>> 15), 1 | state);
    t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t;
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
}

export function sampleWalks(count = 14, steps = 28): Array<Array<[number, number, number]>> {
  const rand = mulberry32(11);
  const walks: Array<Array<[number, number, number]>> = [];
  for (let n = 0; n < count; n++) {
    let x = (rand() - 0.5) * 2.4;
    let z = (rand() - 0.5) * 2.4;
    const walk: Array<[number, number, number]> = [];
    for (let step = 0; step < steps; step++) {
      x = Math.max(-2, Math.min(2, x + (rand() - 0.5) * 0.28));
      z = Math.max(-2, Math.min(2, z + (rand() - 0.5) * 0.28));
      walk.push([x, surfaceHeight(x, z), z]);
    }
    walks.push(walk);
  }
  return walks;
}

export function stillWalks(): string[] {
  return sampleWalks(8, 22).map((walk) => line(walk.map(([x, , z]) => project(x, z))));
}

/** viewBox of the still drawing, cut to its ink plus `pad` (used when WebGL is unavailable). */
export function stillBox(pad = 12): string {
  let x0 = Infinity, x1 = -Infinity, y0 = Infinity, y1 = -Infinity;
  for (let i = 0; i <= 20; i++) {
    for (let j = 0; j <= 20; j++) {
      const [x, y] = project(-2 + i / 5, -2 + j / 5);
      x0 = Math.min(x0, x); x1 = Math.max(x1, x); y0 = Math.min(y0, y); y1 = Math.max(y1, y);
    }
  }
  return `${(x0 - pad).toFixed(0)} ${(y0 - pad).toFixed(0)} ${(x1 - x0 + 2 * pad).toFixed(0)} ${(y1 - y0 + 2 * pad).toFixed(0)}`;
}
