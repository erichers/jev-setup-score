import { OrbitControls } from 'three/addons/controls/OrbitControls.js';
import * as THREE from 'three';
import { sampleWalks, surfaceHeight } from './surface-math';

type ThemeName = 'light' | 'dark';

export function mountSurface(host: HTMLElement, theme: () => ThemeName): () => void {
  const renderer = new THREE.WebGLRenderer({
    antialias: true,
    alpha: true,
    powerPreference: 'high-performance',
  });
  if (!renderer.getContext()) {
    renderer.dispose();
    throw new Error('WebGL is unavailable');
  }
  renderer.setClearColor(0x000000, 0);
  renderer.outputColorSpace = THREE.SRGBColorSpace;
  renderer.toneMapping = THREE.NoToneMapping;
  renderer.domElement.style.width = '100%';
  renderer.domElement.style.height = '100%';
  renderer.domElement.style.display = 'block';
  renderer.domElement.style.touchAction = 'none';
  host.appendChild(renderer.domElement);

  const scene = new THREE.Scene();
  const camera = new THREE.PerspectiveCamera(32, 1, 0.1, 40);
  camera.position.set(4.4, 3.2, 4.6);

  const controls = new OrbitControls(camera, renderer.domElement);
  controls.enableDamping = true;
  controls.dampingFactor = 0.08;
  controls.enablePan = false;
  controls.enableZoom = false;
  controls.rotateSpeed = 0.65;
  controls.autoRotate = true;
  controls.autoRotateSpeed = 0.45;
  controls.target.set(0, 0.5, 0);
  controls.update();

  scene.add(new THREE.AmbientLight(0xffffff, 0.62));
  const key = new THREE.DirectionalLight(0xffffff, 1.45);
  key.position.set(4, 7, 3);
  scene.add(key);
  const fill = new THREE.DirectionalLight(0xffffff, 0.35);
  fill.position.set(-5, 2, -3);
  scene.add(fill);

  const segments = 42;
  const geometry = new THREE.PlaneGeometry(4, 4, segments, segments);
  geometry.rotateX(-Math.PI / 2);
  const position = geometry.attributes['position'] as THREE.BufferAttribute;
  const colors = new Float32Array(position.count * 3);
  const colorAttr = new THREE.BufferAttribute(colors, 3);
  geometry.setAttribute('color', colorAttr);
  const material = new THREE.MeshStandardMaterial({
    vertexColors: true,
    metalness: 0.08,
    roughness: 0.46,
    side: THREE.DoubleSide,
  });
  const mesh = new THREE.Mesh(geometry, material);
  scene.add(mesh);

  const grid = new THREE.GridHelper(4, 8, 0x8a8a8a, 0x8a8a8a);
  grid.position.y = 0;
  scene.add(grid);

  const walks = sampleWalks();
  const lines: THREE.Line[] = [];
  for (const walk of walks) {
    const points = walk.map(([x, y, z]) => new THREE.Vector3(x, y * 1.55 + 0.045, z));
    const lineGeo = new THREE.BufferGeometry().setFromPoints(points);
    const lineMat = new THREE.LineBasicMaterial({ color: 0xccff00 });
    const line = new THREE.Line(lineGeo, lineMat);
    lines.push(line);
    scene.add(line);
  }

  let painted: ThemeName | null = null;
  const applyTheme = (next: ThemeName) => {
    if (painted === next) return;
    painted = next;
    for (let i = 0; i < position.count; i++) {
      const x = position.getX(i);
      const z = position.getZ(i);
      const p = surfaceHeight(x, z);
      position.setY(i, p * 1.55);
      const [r, g, b] = tone(next, p);
      colors[i * 3] = r;
      colors[i * 3 + 1] = g;
      colors[i * 3 + 2] = b;
    }
    position.needsUpdate = true;
    colorAttr.needsUpdate = true;
    geometry.computeVertexNormals();
    const lineColor = next === 'dark' ? 0xccff00 : 0x4f7a00;
    for (const line of lines) {
      (line.material as THREE.LineBasicMaterial).color.setHex(lineColor);
    }
    const gridColor = next === 'dark' ? 0x3a3a3a : 0xc6c6c2;
    const mats = Array.isArray(grid.material) ? grid.material : [grid.material];
    for (const mat of mats) {
      if (mat instanceof THREE.Material && 'color' in mat) {
        (mat as THREE.LineBasicMaterial).color.setHex(gridColor);
      }
    }
  };

  const resize = () => {
    const width = Math.max(1, host.clientWidth);
    const height = Math.max(1, host.clientHeight);
    camera.aspect = width / height;
    camera.updateProjectionMatrix();
    renderer.setPixelRatio(Math.min(window.devicePixelRatio || 1, 2));
    renderer.setSize(width, height, false);
  };

  let running = false;
  let raf = 0;
  const frame = () => {
    if (!running) return;
    raf = requestAnimationFrame(frame);
    applyTheme(theme());
    controls.update();
    renderer.render(scene, camera);
  };
  const setRunning = (next: boolean) => {
    if (next === running) return;
    running = next;
    if (running) frame();
    else cancelAnimationFrame(raf);
  };

  resize();
  applyTheme(theme());
  renderer.render(scene, camera);

  const resizeObserver = new ResizeObserver(() => {
    resize();
    if (!running) renderer.render(scene, camera);
  });
  resizeObserver.observe(host);

  const onScreen = () =>
    setRunning(!document.hidden && Boolean(host.getBoundingClientRect().height));
  const visibility = new IntersectionObserver(
    (entries) => setRunning(entries.some((entry) => entry.isIntersecting) && !document.hidden),
    { threshold: 0.12 },
  );
  visibility.observe(host);
  const onHide = () => onScreen();
  document.addEventListener('visibilitychange', onHide);

  return () => {
    setRunning(false);
    visibility.disconnect();
    resizeObserver.disconnect();
    document.removeEventListener('visibilitychange', onHide);
    controls.dispose();
    geometry.dispose();
    material.dispose();
    for (const line of lines) {
      line.geometry.dispose();
      (line.material as THREE.Material).dispose();
    }
    const mats = Array.isArray(grid.material) ? grid.material : [grid.material];
    for (const mat of mats) mat.dispose();
    grid.geometry.dispose();
    renderer.dispose();
    renderer.domElement.remove();
  };
}

function tone(theme: ThemeName, p: number): [number, number, number] {
  const low = theme === 'dark' ? [0.09, 0.09, 0.08] : [0.82, 0.82, 0.79];
  const high = theme === 'dark' ? [0.8, 1, 0] : [0.31, 0.48, 0];
  return [
    low[0] + (high[0] - low[0]) * p,
    low[1] + (high[1] - low[1]) * p,
    low[2] + (high[2] - low[2]) * p,
  ];
}
