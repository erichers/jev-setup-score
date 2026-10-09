import { spawnSync } from 'node:child_process';
import { createServer } from 'node:http';
import { readFileSync, readdirSync, statSync } from 'node:fs';
import { extname, join, normalize } from 'node:path';
import { fileURLToPath } from 'node:url';

const webRoot = fileURLToPath(new URL('..', import.meta.url));
const prefix = '/apps/jev-setup-score';
const forbidden = ["'/api/", '"/api/', '`/api/'];

function walk(dir, acc = []) {
  for (const name of readdirSync(dir)) {
    if (name === 'node_modules' || name === 'dist' || name === '.angular') continue;
    const path = join(dir, name);
    if (statSync(path).isDirectory()) walk(path, acc);
    else if (/\.(ts|html)$/.test(name)) acc.push(path);
  }
  return acc;
}

function fail(message) {
  console.error(message);
  process.exit(1);
}

for (const file of walk(join(webRoot, 'src'))) {
  const text = readFileSync(file, 'utf8');
  for (const pattern of forbidden) {
    if (text.includes(pattern)) {
      fail(`${file} contains an absolute API path (${pattern}). Use a relative api/... URL.`);
    }
  }
}

const build = spawnSync(
  'npx',
  ['ng', 'build', '--base-href', `${prefix}/`, '--output-path', 'dist/mamp-check'],
  {
    cwd: webRoot,
    stdio: 'inherit',
    env: { ...process.env, NG_CLI_ANALYTICS: 'false' },
  },
);
if (build.status !== 0) process.exit(build.status ?? 1);

const candidates = [
  join(webRoot, 'dist', 'mamp-check', 'browser'),
  join(webRoot, 'dist', 'mamp-check'),
];
const browserRoot = candidates.find((path) => statSafe(path) && statSync(join(path, 'index.html')).isFile());
if (!browserRoot) fail('The sub-path build did not produce index.html.');

const index = readFileSync(join(browserRoot, 'index.html'), 'utf8');
if (!index.includes(`<base href="${prefix}/">`)) {
  fail(`index.html is missing <base href="${prefix}/">.`);
}
if (/<(?:script|link)\b[^>]+(?:src|href)="\//.test(index)) {
  fail('Built index.html has a root-absolute script or stylesheet. Assets must stay relative to the base href.');
}

const scripts = [];
for (const file of walkFiles(browserRoot)) {
  if (!file.endsWith('.js')) continue;
  const text = readFileSync(file, 'utf8');
  for (const pattern of forbidden) {
    if (text.includes(pattern)) fail(`${file} contains ${pattern}`);
  }
  scripts.push(file);
}
if (scripts.length === 0) fail('The sub-path build produced no JavaScript.');

const port = await listen(browserRoot);
const origin = `http://127.0.0.1:${port}`;
const home = await fetch(`${origin}${prefix}/`);
if (!home.ok) fail(`Sub-path home returned ${home.status}.`);
const html = await home.text();
if (!html.includes(`<base href="${prefix}/">`)) fail('Served home page lost the base href.');

const asset = html.match(/src="([^"]+\.js)"/);
if (!asset) fail('Served home page has no script.');
const assetUrl = new URL(asset[1], `${origin}${prefix}/`);
if (!assetUrl.pathname.startsWith(`${prefix}/`)) {
  fail(`Script resolved outside the sub-path: ${assetUrl.pathname}`);
}
const assetResponse = await fetch(assetUrl);
if (!assetResponse.ok) fail(`Script ${assetUrl.pathname} returned ${assetResponse.status}.`);

const deep = await fetch(`${origin}${prefix}/methodology`);
if (!deep.ok) fail(`Deep link returned ${deep.status}.`);
const deepHtml = await deep.text();
if (!deepHtml.includes('<app-root>')) fail('Deep link did not fall back to the application shell.');

const pdf = new URL('api/score/SPY/report.pdf?horizon=10&threshold=60', `${origin}${prefix}/`);
if (pdf.pathname !== `${prefix}/api/score/SPY/report.pdf`) {
  fail(`PDF URL resolved to ${pdf.pathname}`);
}

console.log(`Sub-path check passed on ${origin}${prefix}/`);
process.exit(0);

function statSafe(path) {
  try {
    return statSync(path).isDirectory();
  } catch {
    return false;
  }
}

function walkFiles(dir, acc = []) {
  for (const name of readdirSync(dir)) {
    const path = join(dir, name);
    if (statSync(path).isDirectory()) walkFiles(path, acc);
    else acc.push(path);
  }
  return acc;
}

function listen(root) {
  const types = {
    '.html': 'text/html; charset=utf-8',
    '.js': 'text/javascript; charset=utf-8',
    '.css': 'text/css; charset=utf-8',
    '.svg': 'image/svg+xml',
    '.json': 'application/json',
  };
  const server = createServer((request, response) => {
    const url = new URL(request.url ?? '/', 'http://127.0.0.1');
    let path = decodeURIComponent(url.pathname);
    if (path === prefix) {
      response.writeHead(302, { Location: `${prefix}/` });
      response.end();
      return;
    }
    if (!path.startsWith(`${prefix}/`)) {
      response.writeHead(404);
      response.end('missing prefix');
      return;
    }
    const relative = normalize(path.slice(prefix.length + 1)).replace(/^(\.\.(\/|\\|$))+/, '');
    let file = join(root, relative);
    if (!statSafeFile(file)) file = join(root, 'index.html');
    const body = readFileSync(file);
    response.writeHead(200, { 'Content-Type': types[extname(file)] ?? 'application/octet-stream' });
    response.end(body);
  });
  return new Promise((resolve) => {
    server.listen(0, '127.0.0.1', () => {
      const address = server.address();
      resolve(typeof address === 'object' && address ? address.port : 0);
    });
  });
}

function statSafeFile(path) {
  try {
    return statSync(path).isFile();
  } catch {
    return false;
  }
}
