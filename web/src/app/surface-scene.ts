import { OrbitControls } from 'three/addons/controls/OrbitControls.js';
import * as THREE from 'three';
import { fitResting } from './camera-fit';
import { installAccentMask } from './accent-mask';
import { AdaptiveQuality, webglAvailable } from './quality';
import { paintStill } from './scene-still';
import { sampleWalks, surfaceHeight } from './surface-math';

type ThemeName = 'light' | 'dark';

export type SurfaceState = 'live' | 'lost' | 'fallback';

export interface SurfaceHandle {
  /** Repaint after a theme change (the view renders on demand, so nothing polls the theme). */
  refresh(): void;
  dispose(): void;
}

/**
 * spin=false is the reduced-motion still: the same scene and resting camera, no auto-rotate, and a frame is drawn only
 * when something changes (theme, size, or a drag after Rotate is pressed). Frames are requested on demand in both modes;
 * with spin the request repeats while auto-rotate runs, and auto-rotate runs only while Rotate is pressed.
 *
 * `still` gets the R6 fallback: the same scene projected through the same camera into SVG. It is painted when WebGL
 * cannot start ('fallback') and while the WebGL context is lost ('lost'); 'live' comes back on webglcontextrestored.
 */
export function mountSurface(
  host: HTMLElement,
  theme: () => ThemeName,
  bindOrbit: ((fn: (on: boolean) => void) => void) | undefined,
  spin: boolean,
  still: SVGSVGElement,
  onState: (state: SurfaceState) => void,
): SurfaceHandle {
  const scene = new THREE.Scene();
  const camera = new THREE.PerspectiveCamera(32, 1, 0.1, 40);
  camera.position.set(4.4, 3.2, 4.6);

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
  // vertex colors run from neutral to the accent with height: the material is the accent series (neutral pixels are
  // achromatic, so only the lime part counts as accent in the checker)
  material.userData['token'] = 'accent';
  const mesh = new THREE.Mesh(geometry, material);
  scene.add(mesh);

  // White vertex colors, so the material color is the grid color on screen (light #60605c 5.7:1, dark #6b6b6b 3.5:1).
  const grid = new THREE.GridHelper(4, 8, 0xffffff, 0xffffff);
  grid.position.y = 0;
  scene.add(grid);

  const walks = sampleWalks();
  const lines: THREE.Line[] = [];
  for (const walk of walks) {
    const points = walk.map(([x, y, z]) => new THREE.Vector3(x, y * 1.55 + 0.045, z));
    const lineGeo = new THREE.BufferGeometry().setFromPoints(points);
    const lineMat = new THREE.LineBasicMaterial({ color: 0xcefb31 });
    lineMat.userData['token'] = 'accent';
    lineMat.userData['stillWidth'] = 1.6;
    const line = new THREE.Line(lineGeo, lineMat);
    lines.push(line);
    scene.add(line);
  }

  // Subject points for the resting-camera fit: the surface at full height and the grid floor under it.
  const fitPoints: THREE.Vector3[] = [];
  for (let i = 0; i <= 8; i++) {
    for (let j = 0; j <= 8; j++) {
      const x = -2 + i / 2;
      const z = -2 + j / 2;
      fitPoints.push(new THREE.Vector3(x, surfaceHeight(x, z) * 1.55, z), new THREE.Vector3(x, 0, z));
    }
  }
  // Resting view looks across the slope (perpendicular to the logit gradient 1.35x + 0.75z), so the S-curve reads left
  // to right over the grid floor. From 1024 up the camera sits 10 degrees higher so the sample paths clear the ridge.
  const rest = { azimuth: Math.atan2(-0.75, 1.35), target: new THREE.Vector3(0, 0.5, 0) };
  const restCamera = (width: number, height: number, target: THREE.Vector3) => {
    camera.aspect = width / height;
    camera.updateProjectionMatrix();
    target.copy(rest.target);
    fitResting(camera, target, fitPoints, {
      azimuth: rest.azimuth,
      minEl: 10,
      maxEl: 62,
      // wide frames: 3% clear top and bottom (>= 16px at 560), camera raised up to 10 degrees but never so far that the
      // drawing narrows below 94% of the frame aspect, so fill on the full inner frame stays >= 0.94^2 * 0.94 = 83% (R6)
      margin: window.innerWidth >= 1024 ? 0.94 : 0.96,
      lift: window.innerWidth >= 1024 ? 10 : 0,
      keep: 0.94,
    });
  };

  let painted: ThemeName | null = null;
  const applyTheme = (next: ThemeName): boolean => {
    if (painted === next) return false;
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
    const lineColor = next === 'dark' ? 0xcefb31 : 0x4f7a00;
    for (const line of lines) {
      (line.material as THREE.LineBasicMaterial).color.setHex(lineColor);
    }
    const gridColor = next === 'dark' ? 0x6b6b6b : 0x60605c;
    const mats = Array.isArray(grid.material) ? grid.material : [grid.material];
    for (const mat of mats) {
      if (mat instanceof THREE.Material && 'color' in mat) {
        (mat as THREE.LineBasicMaterial).color.setHex(gridColor);
      }
    }
    return true;
  };

  const disposeScene = () => {
    geometry.dispose();
    material.dispose();
    for (const line of lines) {
      line.geometry.dispose();
      (line.material as THREE.Material).dispose();
    }
    const mats = Array.isArray(grid.material) ? grid.material : [grid.material];
    for (const mat of mats) mat.dispose();
    grid.geometry.dispose();
  };

  const paintFallback = () => {
    const width = Math.max(1, host.clientWidth);
    const height = Math.max(1, host.clientHeight);
    applyTheme(theme());
    const target = new THREE.Vector3();
    restCamera(width, height, target);
    paintStill(THREE, still, scene, camera, width, height);
  };

  let renderer: THREE.WebGLRenderer | null = null;
  try {
    if (!webglAvailable()) throw new Error('WebGL is unavailable');
    renderer = new THREE.WebGLRenderer({ antialias: true, alpha: true, powerPreference: 'high-performance' });
    if (!renderer.getContext()) throw new Error('WebGL is unavailable');
  } catch {
    renderer?.dispose();
    // WebGL could not start: the still is the same scene from the same resting camera.
    paintFallback();
    onState('fallback');
    const ro = new ResizeObserver(() => paintFallback());
    ro.observe(host);
    return {
      refresh: () => paintFallback(),
      dispose: () => {
        ro.disconnect();
        disposeScene();
      },
    };
  }
  const gl = renderer;
  gl.setClearColor(0x000000, 0);
  gl.outputColorSpace = THREE.SRGBColorSpace;
  gl.toneMapping = THREE.NoToneMapping;
  const canvas = gl.domElement;
  canvas.style.width = '100%';
  canvas.style.height = '100%';
  canvas.style.display = 'block';
  canvas.style.touchAction = 'pan-y';
  canvas.dataset['engine'] = 'three';
  host.appendChild(canvas);

  const controls = new OrbitControls(camera, canvas);
  controls.enableDamping = true;
  controls.dampingFactor = 0.08;
  controls.enablePan = false;
  controls.enableZoom = false;
  controls.rotateSpeed = 0.65;
  // MOTION 3.8: never auto-rotate on load. Once settled the camera holds the still pose; the slow turn (motion on)
  // runs only while Rotate is pressed.
  controls.autoRotate = false;
  controls.autoRotateSpeed = 0.45;
  controls.target.copy(rest.target);
  controls.enabled = false;
  controls.update();

  let running = false;
  let lost = false;
  let raf = 0;
  const quality = new AdaptiveQuality(host, (ratio) => {
    gl.setPixelRatio(ratio);
    gl.setSize(Math.max(1, host.clientWidth), Math.max(1, host.clientHeight), false);
    request();
  });

  const draw = () => {
    applyTheme(theme());
    gl.render(scene, camera);
    host.dataset['3d'] = 'settled';
  };
  // On demand: one frame per request; the frame asks for the next one only while the view is moving.
  function request(): void {
    if (!raf && running && !lost) raf = requestAnimationFrame(frame);
  }
  const frame = (now: number) => {
    raf = 0;
    if (!running || lost) return;
    const moving = controls.update();
    draw();
    if (controls.autoRotate || moving) {
      quality.frame(now);
      request();
    } else quality.idle();
  };
  controls.addEventListener('change', () => request());

  bindOrbit?.((on) => {
    controls.enabled = on;
    controls.autoRotate = spin && on;
    canvas.style.touchAction = on ? 'none' : 'pan-y';
    if (!on) resize();
    request();
  });

  const resize = () => {
    const width = Math.max(1, host.clientWidth);
    const height = Math.max(1, host.clientHeight);
    if (!controls.enabled) {
      // back to the resting camera, refit for the new frame
      restCamera(width, height, controls.target);
      controls.update();
    } else {
      camera.aspect = width / height;
      camera.updateProjectionMatrix();
    }
    gl.setPixelRatio(quality.ratio());
    gl.setSize(width, height, false);
  };

  const setRunning = (next: boolean) => {
    if (next === running) return;
    running = next;
    if (running) request();
    else {
      cancelAnimationFrame(raf);
      raf = 0;
      quality.idle();
    }
  };

  // Context loss (iOS drops contexts in background tabs): show the still of the same scene and hide the controls;
  // three restores its GL state on webglcontextrestored, then the live view comes back.
  const onLost = (event: Event) => {
    event.preventDefault();
    lost = true;
    cancelAnimationFrame(raf);
    raf = 0;
    controls.enabled = false;
    controls.autoRotate = false;
    canvas.style.touchAction = 'pan-y';
    paintFallback();
    restCamera(Math.max(1, host.clientWidth), Math.max(1, host.clientHeight), controls.target);
    controls.update();
    onState('lost');
  };
  const onRestored = () => {
    lost = false;
    painted = null;
    resize();
    draw();
    onState('live');
    request();
  };
  canvas.addEventListener('webglcontextlost', onLost);
  canvas.addEventListener('webglcontextrestored', onRestored);

  installAccentMask(THREE, gl, scene, camera);
  resize();
  quality.push();
  draw();

  const resizeObserver = new ResizeObserver(() => {
    if (lost) {
      paintFallback();
      return;
    }
    resize();
    draw();
  });
  resizeObserver.observe(host);

  const visibility = new IntersectionObserver(
    (entries) => setRunning(entries.some((entry) => entry.isIntersecting) && !document.hidden),
    { threshold: 0.12 },
  );
  visibility.observe(host);
  const onHide = () => setRunning(!document.hidden && Boolean(host.getBoundingClientRect().height));
  document.addEventListener('visibilitychange', onHide);

  return {
    refresh: () => {
      if (lost) paintFallback();
      else draw();
    },
    dispose: () => {
      setRunning(false);
      visibility.disconnect();
      resizeObserver.disconnect();
      document.removeEventListener('visibilitychange', onHide);
      canvas.removeEventListener('webglcontextlost', onLost);
      canvas.removeEventListener('webglcontextrestored', onRestored);
      controls.dispose();
      disposeScene();
      gl.dispose();
      canvas.remove();
    },
  };
}

function tone(theme: ThemeName, p: number): [number, number, number] {
  const low = theme === 'dark' ? [0.24, 0.24, 0.23] : [0.82, 0.82, 0.79];
  const high = theme === 'dark' ? [0.8, 1, 0] : [0.31, 0.48, 0];
  return [
    low[0] + (high[0] - low[0]) * p,
    low[1] + (high[1] - low[1]) * p,
    low[2] + (high[2] - low[2]) * p,
  ];
}
