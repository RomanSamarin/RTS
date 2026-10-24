import * as THREE from 'three';

/* =========================================================
   1. РЕНДЕРЕР, СЦЕНА, СВЕТ
   ========================================================= */
const renderer = new THREE.WebGLRenderer({ antialias:true, powerPreference:'high-performance' });
renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
renderer.setSize(window.innerWidth, window.innerHeight);
renderer.shadowMap.enabled = true;
renderer.shadowMap.type = THREE.PCFSoftShadowMap;
document.getElementById('app').appendChild(renderer.domElement);

const scene = new THREE.Scene();
scene.background = new THREE.Color(0x070810);
scene.fog = new THREE.FogExp2(0x070810, 0.020);

const camera = new THREE.PerspectiveCamera(78, window.innerWidth/window.innerHeight, 0.05, 400);
camera.rotation.order = 'YXZ';
scene.add(camera);

scene.add(new THREE.HemisphereLight(0x4a6fa5, 0x0a0a12, 1.1));

const sun = new THREE.DirectionalLight(0x9fc4ff, 1.6);
sun.position.set(14, 28, 10);
sun.castShadow = true;
sun.shadow.mapSize.set(2048, 2048);
sun.shadow.camera.left = -34; sun.shadow.camera.right = 34;
sun.shadow.camera.top = 34;   sun.shadow.camera.bottom = -34;
sun.shadow.camera.far = 90;
sun.shadow.bias = -0.0008;
scene.add(sun);

const lamp1 = new THREE.PointLight(0xff3f70, 90, 45, 2); lamp1.position.set(-16, 6, -14); scene.add(lamp1);
const lamp2 = new THREE.PointLight(0x3fa8ff, 90, 45, 2); lamp2.position.set(16, 6, 14);  scene.add(lamp2);
const lamp3 = new THREE.PointLight(0x2ee6a8, 60, 40, 2); lamp3.position.set(0, 7, -20);  scene.add(lamp3);

/* =========================================================
   2. КАРТА / АРЕНА
   ========================================================= */
const obstacles = [];
const obstacleMeshes = [];

function addBlock(x, z, w, h, d, color = 0x2b3140) {
  const mesh = new THREE.Mesh(
    new THREE.BoxGeometry(w, h, d),
    new THREE.MeshStandardMaterial({ color, roughness:0.72, metalness:0.35 })
  );
  mesh.position.set(x, h/2, z);
  mesh.castShadow = true;
  mesh.receiveShadow = true;
  scene.add(mesh);
  mesh.updateMatrixWorld(true);
  obstacles.push({ mesh, box: new THREE.Box3().setFromObject(mesh) });
  obstacleMeshes.push(mesh);
  return mesh;
}

const floor = new THREE.Mesh(
  new THREE.PlaneGeometry(60, 60),
  new THREE.MeshStandardMaterial({ color:0x14161f, roughness:0.92, metalness:0.2 })
);
floor.rotation.x = -Math.PI/2;
floor.receiveShadow = true;
scene.add(floor);

const grid = new THREE.GridHelper(60, 60, 0x2b3a55, 0x1b2233);
grid.position.y = 0.012;
grid.material.transparent = true;
grid.material.opacity = 0.65;
scene.add(grid);

addBlock(  0, -25, 52, 7, 2, 0x1b2130);
addBlock(  0,  25, 52, 7, 2, 0x1b2130);
addBlock(-25,   0, 2, 7, 52, 0x1b2130);
addBlock( 25,   0, 2, 7, 52, 0x1b2130);

const layout = [
  [-8,-6,3,2.4,3],  [7,-9,2.5,3.5,2.5], [10,6,4,1.6,4],  [-11,8,3,3,3],
  [0,-14,6,2,2],    [-4,14,2.5,2.5,2.5],[14,-14,3,4,3],  [-16,-13,2,2,5],
  [16,14,2.5,2.5,2.5],[-18,16,4,1.8,4], [4,2.5,1.8,1.8,1.8], [-2.5,-2,1.6,2.8,1.6],
  [-20,2,2,2.6,6],  [20,-2,2,2.6,6],   [8,18,5,1.4,3],   [-9,-19,4,2.2,3]
];
layout.forEach(([x,z,w,h,d], i) => addBlock(x, z, w, h, d, i % 3 === 0 ? 0x353d4f : 0x272d3a));

/* =========================================================
   3. ИГРОК
   ========================================================= */
const player = {
  pos: new THREE.Vector3(0, 1.7, 8),
  yaw: 0,
  pitch: 0,
  vy: 0,
  health: 100,
  radius: 0.42,
  eyeHeight: 1.7
};

let state = 'menu';
let score = 0, kills = 0, wave = 1;
let ammo = 30; const magSize = 30;
let reloading = false, reloadTimer = 0;
let fireTimer = 0; const fireCooldown = 0.105;
let mouseDown = false;
let recoilPitch = 0, recoilKick = 0, flashTimer = 0, bobT = 0;
let damageFlash = 0, hitmarkerTimer = 0;
let spawnTimer = 1.2;

const keys = {};

/* =========================================================
   4. ОРУЖИЕ
   ========================================================= */
const weapon = new THREE.Group();
weapon.position.set(0.30, -0.26, -0.50);
camera.add(weapon);

const gunMat    = new THREE.MeshStandardMaterial({ color:0x1e222b, metalness:0.9, roughness:0.32 });
const accentMat = new THREE.MeshStandardMaterial({ color:0x2ee6a8, emissive:0x1f8f6c, emissiveIntensity:1.6, metalness:0.5, roughness:0.4 });

const wBody = new THREE.Mesh(new THREE.BoxGeometry(0.13, 0.16, 0.46), gunMat);
wBody.position.z = -0.10;
weapon.add(wBody);

const wBarrel = new THREE.Mesh(new THREE.CylinderGeometry(0.034, 0.034, 0.36, 10), gunMat);
wBarrel.rotation.x = Math.PI/2;
wBarrel.position.set(0, 0.02, -0.45);
weapon.add(wBarrel);

const wGrip = new THREE.Mesh(new THREE.BoxGeometry(0.10, 0.20, 0.12), gunMat);
wGrip.position.set(0, -0.14, 0.05);
wGrip.rotation.x = -0.20;
weapon.add(wGrip);

const wSight = new THREE.Mesh(new THREE.BoxGeometry(0.05, 0.05, 0.05), accentMat);
wSight.position.set(0, 0.11, -0.20);
weapon.add(wSight);

const muzzle = new THREE.Object3D();
muzzle.position.set(0, 0.02, -0.64);
weapon.add(muzzle);

const muzzleLight = new THREE.PointLight(0xffb066, 0, 7, 2);
muzzleLight.position.copy(muzzle.position);
weapon.add(muzzleLight);

const flashMesh = new THREE.Mesh(
  new THREE.SphereGeometry(0.09, 6, 6),
  new THREE.MeshBasicMaterial({ color:0xffd27a, transparent:true, opacity:0, blending:THREE.AdditiveBlending, depthWrite:false })
);
flashMesh.position.copy(muzzle.position);
weapon.add(flashMesh);

/* =========================================================
   5. ВРАГИ
   ========================================================= */
const enemies = [];

function createEnemy(x, z) {
  const g = new THREE.Group();
  g.position.set(x, 1.15, z);

  const bodyMat = new THREE.MeshStandardMaterial({ color:0x39404f, metalness:0.85, roughness:0.35, flatShading:true });
  const shell = new THREE.Mesh(new THREE.IcosahedronGeometry(0.45, 0), bodyMat);
  shell.castShadow = true;
  g.add(shell);

  const eye = new THREE.Mesh(
    new THREE.SphereGeometry(0.13, 12, 12),
    new THREE.MeshStandardMaterial({ color:0xff2b4a, emissive:0xff2b4a, emissiveIntensity:2.4, roughness:0.3 })
  );
  eye.position.set(0, 0.03, 0.40);
  g.add(eye);

  const ring = new THREE.Mesh(
    new THREE.TorusGeometry(0.62, 0.035, 8, 32),
    new THREE.MeshStandardMaterial({ color:0x2ee6a8, emissive:0x1f8f6c, emissiveIntensity:1.6, metalness:0.6, roughness:0.4 })
  );
  ring.rotation.x = Math.PI/2;
  g.add(ring);

  scene.add(g);

  const e = {
    group: g, shell, eye, ring,
    hp: 3, maxHp: 3,
    speed: 2.0 + Math.min(wave, 12) * 0.16,
    attackCd: 0.8,
    phase: Math.random() * Math.PI * 2,
    dead: false,
    flash: 0,
    hitMeshes: [shell, eye]
  };
  shell.userData = { enemy: e, part: 'body' };
  eye.userData   = { enemy: e, part: 'head' };

  enemies.push(e);
  return e;
}

function findSpawnPoint() {
  for (let i = 0; i < 40; i++) {
    const a = Math.random() * Math.PI * 2;
    const r = 16 + Math.random() * 8;
    const x = Math.cos(a) * r, z = Math.sin(a) * r;
    if (Math.abs(x) > 22 || Math.abs(z) > 22) continue;
    if (blockedAt(x, z, 0.9)) continue;
    if (Math.hypot(x - player.pos.x, z - player.pos.z) < 13) continue;
    return { x, z };
  }
  return { x: 0, z: -20 };
}

function killEnemy(e) {
  e.dead = true;
  spawnExplosion(e.group.position.clone());
  scene.remove(e.group);
  e.shell.geometry.dispose(); e.shell.material.dispose();
  e.eye.geometry.dispose();   e.eye.material.dispose();
  e.ring.geometry.dispose();  e.ring.material.dispose();

  const idx = enemies.indexOf(e);
  if (idx >= 0) enemies.splice(idx, 1);

  kills++;
  score += 100;
  beep(880, 0.10, 'square', 0.07);
  wave = 1 + Math.floor(kills / 8);
  updateHUD();
}

function damageEnemy(e, dmg) {
  if (e.dead) return;
  e.hp -= dmg;
  e.flash = 1;
  score += dmg * 10;
  if (e.hp <= 0) killEnemy(e);
  updateHUD();
}

/* =========================================================
   6. ЭФФЕКТЫ
   ========================================================= */
const effects = [];

function spawnTracer(from, to) {
  const geo = new THREE.BufferGeometry().setFromPoints([from, to]);
  const line = new THREE.Line(geo, new THREE.LineBasicMaterial({
    color: 0xffe08a, transparent: true, opacity: 0.85,
    blending: THREE.AdditiveBlending, depthWrite: false
  }));
  scene.add(line);
  effects.push({ obj: line, life: 0.07, max: 0.07, kind: 'tracer' });
}

function spawnImpact(pos, color) {
  const mesh = new THREE.Mesh(
    new THREE.SphereGeometry(0.10, 8, 8),
    new THREE.MeshBasicMaterial({ color, transparent:true, opacity:1, blending:THREE.AdditiveBlending, depthWrite:false })
  );
  mesh.position.copy(pos);
  scene.add(mesh);
  effects.push({ obj: mesh, life: 0.20, max: 0.20, kind: 'impact' });
}

function spawnExplosion(pos) {
  const mesh = new THREE.Mesh(
    new THREE.SphereGeometry(0.5, 12, 12),
    new THREE.MeshBasicMaterial({ color: 0xff6644, transparent:true, opacity:0.9, blending:THREE.AdditiveBlending, depthWrite:false })
  );
  mesh.position.copy(pos);
  scene.add(mesh);
  effects.push({ obj: mesh, life: 0.42, max: 0.42, kind: 'explosion' });
}

function updateEffects(dt) {
  for (let i = effects.length - 1; i >= 0; i--) {
    const fx = effects[i];
    fx.life -= dt;
    const t = Math.max(0, fx.life / fx.max);
    if (fx.kind === 'explosion') {
      fx.obj.scale.setScalar(1 + (1 - t) * 3.5);
      fx.obj.material.opacity = t * 0.9;
    } else {
      fx.obj.material.opacity = t;
      if (fx.kind === 'impact') fx.obj.scale.setScalar(1 + (1 - t) * 1.6);
    }
    if (fx.life <= 0) {
      scene.remove(fx.obj);
      fx.obj.geometry.dispose();
      fx.obj.material.dispose();
      effects.splice(i, 1);
    }
  }
}

/* =========================================================
   7. ЗВУК
   ========================================================= */
let actx = null, noiseBuf = null;

function initAudio() {
  if (!actx) {
    try { actx = new (window.AudioContext || window.webkitAudioContext)(); } catch (e) { actx = null; }
  }
  if (actx && actx.state === 'suspended') actx.resume();
}

function getNoise() {
  if (!actx) return null;
  if (!noiseBuf) {
    const len = Math.floor(actx.sampleRate * 0.3);
    noiseBuf = actx.createBuffer(1, len, actx.sampleRate);
    const d = noiseBuf.getChannelData(0);
    for (let i = 0; i < len; i++) d[i] = Math.random() * 2 - 1;
  }
  return noiseBuf;
}

function beep(freq, dur, type = 'sine', vol = 0.12) {
  if (!actx) return;
  const t = actx.currentTime;
  const o = actx.createOscillator(), g = actx.createGain();
  o.type = type;
  o.frequency.setValueAtTime(freq, t);
  o.frequency.exponentialRampToValueAtTime(Math.max(30, freq * 0.5), t + dur);
  g.gain.setValueAtTime(vol, t);
  g.gain.exponentialRampToValueAtTime(0.001, t + dur);
  o.connect(g).connect(actx.destination);
  o.start(t); o.stop(t + dur + 0.03);
}

function playShot() {
  if (!actx) return;
  const t = actx.currentTime;

  const o = actx.createOscillator(), g1 = actx.createGain();
  o.type = 'square';
  o.frequency.setValueAtTime(230, t);
  o.frequency.exponentialRampToValueAtTime(48, t + 0.10);
  g1.gain.setValueAtTime(0.10, t);
  g1.gain.exponentialRampToValueAtTime(0.001, t + 0.12);
  o.connect(g1).connect(actx.destination);
  o.start(t); o.stop(t + 0.13);

  const src = actx.createBufferSource();
  src.buffer = getNoise();
  const flt = actx.createBiquadFilter();
  flt.type = 'lowpass';
  flt.frequency.setValueAtTime(2400, t);
  flt.frequency.exponentialRampToValueAtTime(320, t + 0.12);
  const g2 = actx.createGain();
  g2.gain.setValueAtTime(0.20, t);
  g2.gain.exponentialRampToValueAtTime(0.001, t + 0.14);
  src.connect(flt).connect(g2).connect(actx.destination);
  src.start(t); src.stop(t + 0.15);
}

/* =========================================================
   8. СТРЕЛЬБА
   ========================================================= */
const raycaster = new THREE.Raycaster();
const CENTER = new THREE.Vector2(0, 0);

function shoot() {
  ammo--;
  fireTimer = fireCooldown;
  recoilKick = 1;
  recoilPitch += 0.020 + Math.random() * 0.008;
  flashTimer = 0.055;
  playShot();
  updateHUD();

  scene.updateMatrixWorld(true);
  raycaster.setFromCamera(CENTER, camera);

  const targets = obstacleMeshes.slice();
  for (const e of enemies) targets.push(...e.hitMeshes);

  const hits = raycaster.intersectObjects(targets, false);

  const from = new THREE.Vector3();
  muzzle.getWorldPosition(from);

  let end;
  if (hits.length) {
    const h = hits[0];
    end = h.point.clone();
    const ud = h.object.userData;
    if (ud && ud.enemy) {
      const head = ud.part === 'head';
      damageEnemy(ud.enemy, head ? 2 : 1);
      spawnImpact(end, head ? 0xffd980 : 0xff5566);
      showHitmarker(head);
    } else {
      spawnImpact(end, 0xffcc77);
    }
  } else {
    end = raycaster.ray.origin.clone().add(raycaster.ray.direction.clone().multiplyScalar(160));
  }

  spawnTracer(from, end);
}

/* =========================================================
   9. УПРАВЛЕНИЕ
   ========================================================= */
const canvas = renderer.domElement;
const hudEl = document.getElementById('hud');

document.addEventListener('keydown', (e) => {
  keys[e.code] = true;
  if (e.code === 'KeyR') startReload();
  if (e.code === 'Escape') document.exitPointerLock();
  if (['Space', 'ArrowUp', 'ArrowDown', 'ArrowLeft', 'ArrowRight'].includes(e.code)) e.preventDefault();
});
document.addEventListener('keyup', (e) => { keys[e.code] = false; });

document.addEventListener('mousemove', (e) => {
  if (document.pointerLockElement !== canvas) return;
  const sens = 0.0022;
  player.yaw   -= e.movementX * sens;
  player.pitch -= e.movementY * sens;
  const lim = Math.PI / 2 - 0.02;
  player.pitch = Math.max(-lim, Math.min(lim, player.pitch));
});

document.addEventListener('mousedown', (e) => {
  if (e.button === 0 && document.pointerLockElement === canvas && state === 'playing') mouseDown = true;
});
document.addEventListener('mouseup', (e) => { if (e.button === 0) mouseDown = false; });

document.addEventListener('pointerlockchange', () => {
  const locked = document.pointerLockElement === canvas;
  if (locked) {
    if (state === 'paused' || state === 'menu') {
      state = 'playing';
      hideAllScreens();
    }
  } else {
    if (state === 'playing') {
      state = 'paused';
      showScreen('pause');
    }
  }
});

window.addEventListener('resize', () => {
  camera.aspect = window.innerWidth / window.innerHeight;
  camera.updateProjectionMatrix();
  renderer.setSize(window.innerWidth, window.innerHeight);
});


const hpVal = document.getElementById('hpVal');
const healthFill = document.getElementById('healthFill');
const ammoVal = document.getElementById('ammoVal');
const ammoMax = document.getElementById('ammoMax');
const ammoBox = document.getElementById('ammoBox');
const scoreVal = document.getElementById('scoreVal');
const waveVal = document.getElementById('waveVal');
const crosshair = document.getElementById('crosshair');
const hitmarker = document.getElementById('hitmarker');
const vignette = document.getElementById('vignette');

function updateHUD() {
  hpVal.textContent = Math.max(0, Math.ceil(player.health));
  const hp = Math.max(0, player.health);
  healthFill.style.width = hp + '%';
  healthFill.style.background = hp > 50
    ? 'linear-gradient(90deg,#2ee6a8,#7ef7c8)'
    : hp > 25
      ? 'linear-gradient(90deg,#f2c14e,#ffd980)'
      : 'linear-gradient(90deg,#ff4d5e,#ff8a95)';

  ammoVal.textContent = reloading ? '--' : ammo;
  ammoMax.textContent = '/' + magSize;
  ammoBox.classList.toggle('low', !reloading && ammo <= 8);
  scoreVal.textContent = score;
  waveVal.textContent = wave;
}

function showHitmarker(head) {
  hitmarker.classList.add('on');
  hitmarker.classList.toggle('head', head);
  hitmarkerTimer = 0.12;
}

const screens = {
  menu: document.getElementById('menu'),
  pause: document.getElementById('pause'),
  gameover: document.getElementById('gameover')
};
function hideAllScreens() {
  Object.values(screens).forEach(s => s.classList.add('hidden'));
  hudEl.classList.add('on');
}
function showScreen(name) {
  Object.values(screens).forEach(s => s.classList.add('hidden'));
  if (screens[name]) screens[name].classList.remove('hidden');
  if (name === 'menu') hudEl.classList.remove('on');
}

/* =========================================================
   11. КОЛЛИЗИИ
   ========================================================= */
function blockedAt(x, z, r) {
  for (let i = 0; i < obstacles.length; i++) {
    const b = obstacles[i].box;
    if (x > b.min.x - r && x < b.max.x + r && z > b.min.z - r && z < b.max.z + r) return true;
  }
  return false;
}

function movePlayer(dx, dz) {
  const r = player.radius;
  if (!blockedAt(player.pos.x + dx, player.pos.z, r)) player.pos.x += dx;
  if (!blockedAt(player.pos.x, player.pos.z + dz, r)) player.pos.z += dz;
}

/* =========================================================
   12. ЛОГИКА ИГРЫ
   ========================================================= */
function startReload() {
  if (state !== 'playing' || reloading || ammo === magSize) return;
  reloading = true;
  reloadTimer = 1.35;
  beep(400, 0.07, 'square', 0.06);
  setTimeout(() => beep(600, 0.07, 'square', 0.06), 140);
  updateHUD();
}

function damagePlayer(amount) {
  if (state !== 'playing') return;
  player.health -= amount;
  damageFlash = 1;
  beep(110, 0.20, 'sawtooth', 0.16);
  if (player.health <= 0) {
    player.health = 0;
    gameOver();
  }
  updateHUD();
}

function gameOver() {
  state = 'dead';
  mouseDown = false;
  document.getElementById('finalScore').textContent = score;
  document.getElementById('finalKills').textContent = kills;
  document.getElementById('finalWave').textContent = wave;
  showScreen('gameover');
  document.exitPointerLock();
  beep(180, 0.6, 'sawtooth', 0.18);
}

function resetGame() {
  for (const e of enemies) scene.remove(e.group);
  enemies.length = 0;

  for (const fx of effects) { scene.remove(fx.obj); fx.obj.geometry.dispose(); fx.obj.material.dispose(); }
  effects.length = 0;

  player.pos.set(0, 1.7, 8);
  player.yaw = 0;
  player.pitch = 0;
  player.vy = 0;
  player.health = 100;

  ammo = magSize;
  reloading = false;
  reloadTimer = 0;
  fireTimer = 0;
  recoilPitch = 0;
  recoilKick = 0;
  flashTimer = 0;
  damageFlash = 0;
  bobT = 0;
  mouseDown = false;

  score = 0; kills = 0; wave = 1;
  spawnTimer = 1.0;

  updateHUD();
}

function startGame() {
  initAudio();
  resetGame();
  state = 'playing';
  hideAllScreens();
  const p = canvas.requestPointerLock();
  if (p && p.catch) p.catch(() => {});
}

/* =========================================================
   13. ОБНОВЛЕНИЕ
   ========================================================= */
function updateEnemies(dt) {
  const time = performance.now() * 0.001;

  for (const e of enemies) {
    if (e.dead) continue;

    e.ring.rotation.z += dt * 2.2;
    e.group.position.y = 1.15 + Math.sin(time * 2 + e.phase) * 0.10;

    if (e.flash > 0) {
      e.flash = Math.max(0, e.flash - dt * 4);
      e.shell.material.emissive.setRGB(e.flash * 0.9, e.flash * 0.15, e.flash * 0.2);
    }

    const dx = player.pos.x - e.group.position.x;
    const dz = player.pos.z - e.group.position.z;
    const dist = Math.hypot(dx, dz) || 0.0001;

    e.group.rotation.y = Math.atan2(dx, dz);

    if (dist > 1.9) {
      const nx = dx / dist, nz = dz / dist;
      const step = e.speed * dt;
      if (!blockedAt(e.group.position.x + nx * step, e.group.position.z, 0.5))
        e.group.position.x += nx * step;
      if (!blockedAt(e.group.position.x, e.group.position.z + nz * step, 0.5))
        e.group.position.z += nz * step;
      e.attackCd = Math.max(e.attackCd, 0.45);
    } else {
      e.attackCd -= dt;
      if (e.attackCd <= 0) {
        e.attackCd = 1.3;
        damagePlayer(9);
      }
    }
  }
}

function update(dt) {
  const sprint = keys['ShiftLeft'] || keys['ShiftRight'];
  const speed = sprint ? 8.5 : 5.5;

  let ix = 0, iz = 0;
  if (keys['KeyW'] || keys['ArrowUp'])    iz -= 1;
  if (keys['KeyS'] || keys['ArrowDown'])  iz += 1;
  if (keys['KeyA'] || keys['ArrowLeft'])  ix -= 1;
  if (keys['KeyD'] || keys['ArrowRight']) ix += 1;

  const ilen = Math.hypot(ix, iz);
  if (ilen > 0) { ix /= ilen; iz /= ilen; }

  const fx = -Math.sin(player.yaw), fz = -Math.cos(player.yaw);
  const rx =  Math.cos(player.yaw), rz = -Math.sin(player.yaw);

  const mvx = (fx * -iz + rx * ix) * speed * dt;
  const mvz = (fz * -iz + rz * ix) * speed * dt;
  if (ilen > 0) movePlayer(mvx, mvz);

  const onGround = player.pos.y <= player.eyeHeight + 0.001;
  if (keys['Space'] && onGround && player.vy <= 0) player.vy = 5.4;
  player.vy -= 17 * dt;
  player.pos.y += player.vy * dt;
  if (player.pos.y <= player.eyeHeight) {
    player.pos.y = player.eyeHeight;
    player.vy = 0;
  }

  const moving = ilen > 0 && player.pos.y <= player.eyeHeight + 0.001;
  bobT += dt * (sprint ? 13 : 9) * (moving ? 1 : 0);
  const bobY = moving ? Math.sin(bobT) * 0.045 : 0;

  recoilPitch = Math.max(0, recoilPitch - dt * 0.10);
  recoilKick  = Math.max(0, recoilKick  - dt * 8.5);

  camera.position.set(player.pos.x, player.pos.y + bobY, player.pos.z);
  camera.rotation.y = player.yaw;
  camera.rotation.x = player.pitch + recoilPitch;
  camera.updateMatrixWorld(true);

  weapon.position.z = -0.50 + recoilKick * 0.085;
  weapon.rotation.x = recoilKick * 0.35;

  flashTimer = Math.max(0, flashTimer - dt);
  const f = flashTimer / 0.055;
  flashMesh.material.opacity = f;
  flashMesh.scale.setScalar(0.6 + f * 0.9);
  muzzleLight.intensity = f * 45;

  fireTimer -= dt;
  if (reloading) {
    reloadTimer -= dt;
    if (reloadTimer <= 0) {
      reloading = false;
      ammo = magSize;
      beep(700, 0.06, 'square', 0.07);
      updateHUD();
    }
  } else if (mouseDown && fireTimer <= 0) {
    if (ammo > 0) shoot();
    else startReload();
  }

  updateEnemies(dt);

  spawnTimer -= dt;
  const maxAlive = 4 + Math.min(wave, 6);
  if (spawnTimer <= 0) {
    if (enemies.length < maxAlive) {
      const p = findSpawnPoint();
      createEnemy(p.x, p.z);
    }
    spawnTimer = Math.max(0.70, 2.4 - wave * 0.12);
  }

  damageFlash = Math.max(0, damageFlash - dt * 2.2);
  vignette.style.opacity = damageFlash * 0.85;

  if (hitmarkerTimer > 0) {
    hitmarkerTimer -= dt;
    if (hitmarkerTimer <= 0) hitmarker.classList.remove('on');
  }

  crosshair.classList.toggle('hit', recoilKick > 0.25);
}

/* =========================================================
   14. ЦИКЛ
   ========================================================= */
const clock = new THREE.Clock();

function animate() {
  requestAnimationFrame(animate);
  const dt = Math.min(clock.getDelta(), 0.05);
  if (state === 'playing') update(dt);
  updateEffects(dt);
  renderer.render(scene, camera);
}

/* =========================================================
   15. КНОПКИ
   ========================================================= */
document.getElementById('startBtn').addEventListener('click', (e) => {
  e.stopPropagation();
  startGame();
});
document.getElementById('restartBtn').addEventListener('click', (e) => {
  e.stopPropagation();
  startGame();
});

document.addEventListener('click', () => {
  if (document.pointerLockElement === canvas) return;
  if (state === 'paused' || (state === 'playing' && !document.pointerLockElement)) {
    const p = canvas.requestPointerLock();
    if (p && p.catch) p.catch(() => {});
  }
});

/* =========================================================
   СТАРТ
   ========================================================= */
updateHUD();
showScreen('menu');
animate();