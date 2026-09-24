// «Злой csproj»: режиссура. Каждая сцена заполняет кадр f по времени t (см. engine.js).
// Метки времени (M('…')) задаёт story.py; здесь — что происходит между ними.

const SCENES = {};
const FILM = () => TL.film.meta;

/** Затемнение на стыках сцен. */
function dip(t, sc, inDur = 0.3, outDur = 0.3) {
  return Math.max(inDur ? 1 - seg(t, sc.start, sc.start + inDur) : 0, outDur ? seg(t, sc.end - outDur, sc.end) : 0);
}

/** Тряска от ударов по клавишам и «двери сейфа». */
function shakeAt(t) {
  let dx = 0, dy = 0, r = 0;
  TL.sfx.forEach((e, i) => {
    if (e.t > t) return;
    const age = t - e.t;
    let a = 0;
    if (e.name === 'slam') a = e.gain * 16 * Math.exp(-age * 15);
    else if (e.name === 'safe') a = 46 * Math.exp(-age * 5);
    if (a < 0.2) return;
    const ph = age * 70 + i;
    dx += a * Math.sin(ph) * (hash(i) > 0.5 ? 1 : -1);
    dy += a * Math.cos(ph * 1.3) * 0.7;
    r += a * 0.02 * Math.sin(ph * 0.7);
  });
  return [dx, dy, r];
}

/** Курсор по опорным точкам [[t, x, y], ...]; press — моменты кликов. */
function cursorPath(t, pts, clicks = []) {
  const p = keys(t, pts.map(([tt, x, y]) => [tt, [x, y]]));
  const press = clicks.some(c => t >= c && t < c + 0.12);
  return [p[0], p[1], press];
}

function keycap(label, x, y, t0, t, dur = 0.55, size = 54) {
  if (t < t0 || t > t0 + dur) return '';
  const k = seg(t, t0, t0 + 0.12);
  const out = seg(t, t0 + dur - 0.15, t0 + dur);
  const press = t - t0 < 0.1 ? 6 : 0;
  return `<div class="keycap" style="left:${x}px;top:${y + press}px;font-size:${size}px;transform:translate(-50%,0) scale(${0.7 + 0.3 * back(k)});opacity:${1 - out}">${label}</div>`;
}

function coffeeBadge(n, t, clockSecs) {
  return `<div class="coffee"><svg width="54" height="54" viewBox="-50 -50 100 100">${wallClock(0, 0, 40, clockSecs)}</svg><span class="e">☕</span>×${n}</div>`;
}

const line = id => TL.lines.find(l => l.id === id);
const inLine = (id, t, pad = 0) => { const l = line(id); return t >= l.t0 - pad && t <= l.t1 + pad; };

/* ═══ 0. Заставка ════════════════════════════════════════════════════════════════════════ */
SCENES.s0 = (t, f, sc) => {
  const pre = typedText('s0.pre', t);
  const tt = M('s0.title');
  const k = seg(t, tt, tt + 0.25);
  const shake = t >= tt ? Math.exp(-(t - tt) * 9) * 18 : 0;
  const sub = seg(t, tt + 0.5, tt + 1.0);
  f.noSubs = true;
  f.screen = `<div style="position:absolute;inset:0;background:radial-gradient(70% 70% at 50% 45%, #231c3d 0%, #0f0d17 70%)">
    <div style="position:absolute;left:0;right:0;top:${t < tt ? 500 : 330}px;text-align:center;font:500 34px 'JetBrains Mono';color:#8a86a3;opacity:${t < tt ? 1 : 0.9}">${esc(pre)}${t < tt && cursorOn(t) ? '<span class="cur"></span>' : ''}</div>
    ${t >= tt ? `<div style="position:absolute;left:0;right:0;top:400px;text-align:center;transform:translate(${Math.sin(t * 90) * shake}px,${Math.cos(t * 70) * shake}px) scale(${1.5 - 0.5 * easeO(k)});opacity:${k}">
      <span style="font:900 170px/1 Unbounded;color:#fff;letter-spacing:-.02em">Злой</span>
      <span style="font:800 170px/1 'JetBrains Mono';color:#ff6f98;margin-left:.3em">csproj</span></div>
      <div style="position:absolute;left:0;right:0;top:640px;text-align:center;font:500 34px Onest;color:#b7b2cc;opacity:${sub}">короткий фильм о WinForms на Linux</div>` : ''}
  </div>`;
  f.fade = seg(t, sc.end - 0.35, sc.end);
};

/* ═══ 1. Офис, понедельник, утро ═════════════════════════════════════════════════════════ */
/** Гена с тележкой в профиль: руки на ручке, тележка впереди. */
function genaPushing(w, extra = {}) {
  const cartX = w.x + w.dir * 213;
  return {
    cartX,
    gena: { x: w.x, y: 640, side: w.dir, phase: w.phase, S: w.S, amp: w.amp, near: [79, 0], far: [73, 4], ...extra },
  };
}

SCENES.s1 = (t, f, sc) => {
  const pull = M('s1.pull'), stop = M('s1.cartStop'), turn = M('s1.turn'), mon = M('s1.monitor');
  const l1 = M('s1.l1'), m2 = M('s1.m2'), l2 = M('s1.l2'), g3 = M('s1.g3');

  if (t >= mon && t < l1) {
    // Крупно: монитор Лёши, ярлык из 2011 года
    const k = seg(t, mon, l1);
    f.screen = oldDesktop(t, seg(t, mon + 0.7, mon + 0.95));
    f.screenTransform = `scale(${1 + 0.06 * k})`;
    f.screenOrigin = '20% 20%';
    f.cursor = [330 + 6 * Math.sin(t * 2), 230 + 4 * Math.cos(t * 2.3), false];
    return;
  }

  const GX = 763;
  const mouth = mouthOf('gena', t);
  let gena, cartX, carried = '', slabs = [], cartN = 3;
  if (t < stop) {
    // въезжает справа, толкая тележку
    const w = walkSeg(t, pull, stop, 2020, GX, 150);
    ({ gena, cartX } = genaPushing(w, { mouth }));
  } else if (t < g3 + 0.1) {
    // стоит анфас; правой рукой (слева в кадре) переносит верхний ноутбук с тележки на стол Марины Петровны
    cartX = GX - 213;
    const k = easeIO(seg(t, stop + 0.1, stop + 0.95));
    const lift = Math.sin(k * Math.PI) * 50;
    const slabAt = [lerp(cartX, 575, k), lerp(790 - 138, 682, k) - lift];
    const moving = t >= stop + 0.1 && t < stop + 0.95;
    if (t >= stop + 0.1) cartN = 2;
    if (moving) carried = slab(slabAt[0], slabAt[1]);
    if (t >= stop + 0.95) slabs = [[575, 682]];
    const hand = moving ? [slabAt[0] + 52 - GX, slabAt[1] - 640, 40] : t < stop + 0.1 ? [-79, 0, 20] : [-72, 36, 12];
    const genaTurn = t < M('s1.m1') ? -0.25 : t < turn ? -0.55 : keys(t, [[turn, -0.55], [turn + 0.9, 0.85]]);
    gena = {
      x: GX, y: 640, turn: genaTurn, handL: hand, handR: null,
      lean: moving ? -9 * Math.sin(k * Math.PI) : 0,
      mouth, smile: t < turn ? 0.35 : 0.15, brow: t >= turn ? 0.25 : 0,
    };
  } else {
    // «Ну собери» — уходит, толкая тележку
    const w = walkSeg(t, g3 + 0.1, g3 + 4.1, GX, -420, 150);
    ({ gena, cartX } = genaPushing(w, { mouth }));
    cartN = 2;
    slabs = [[575, 682]];
  }
  const marinaUp = t >= m2 && t < l2;
  const marina = {
    lookDown: marinaUp ? 0 : 0.8, turn: marinaUp ? 0.25 : -0.15, mouth: mouthOf('marina', t),
    handL: [-46, -74], handR: [96, -72], smile: marinaUp ? -0.1 : 0, brow: marinaUp ? -0.1 : 0,
  };
  const lesha = {
    turn: t < l1 - 0.2 ? 0 : -0.55, lookDown: t < l1 - 0.2 ? 0.35 : 0,
    mouth: mouthOf('lesha', t), handL: [-40, -64, 70], handR: [44, -64, 70],
    eyes: t >= M('s1.g2') && t < l1 ? 1.25 : 1, brow: t >= l2 ? 0.35 : t >= l1 ? 0.1 : 0,
    worried: t >= l1 && t < l2 ? 0.6 : 0, smile: t >= l2 ? 0.55 : 0,
  };
  f.world = office({
    t, mood: 'day', marina, lesha, gena, leshaSetup: 'pc', slabs, carried,
    cartAt: [cartX, 790], cartN, cartRolling: !!gena.side,
  });

  // камера
  if (t < pull) f.view = keys(t, [[sc.start, [1265, 272, 3.4]], [pull, [1265, 276, 3.0]]], k => k);
  else if (t < stop) f.view = keys(t, [[pull, [1265, 276, 3.0]], [pull + 1.8, [960, 560, 1.0]]]);
  else if (t < turn) f.view = keys(t, [[stop, [960, 560, 1.0]], [stop + 1.0, [600, 560, 1.5]]]);
  else if (t < l1) f.view = keys(t, [[turn, [600, 560, 1.5]], [turn + 1.0, [1180, 540, 1.22]]]);
  else if (t < m2) f.view = [1540, 520, 2.1];
  else if (t < l2) f.view = keys(t, [[m2, [395, 500, 2.9]], [l2, [395, 500, 3.05]]], k => k);
  else if (t < g3) f.view = [1540, 520, 2.1];
  else f.view = keys(t, [[g3, [900, 560, 1.05]], [sc.end, [860, 560, 1.08]]], k => k);

  // титр
  const tr = M('s1.titr');
  if (t >= tr && t < pull + 0.8) {
    const txt = 'С 1 числа все рабочие места переводятся на Linux.';
    const n = Math.round(txt.length * seg(t, tr, tr + 1.6));
    const out = seg(t, pull + 0.4, pull + 0.8);
    f.overlay = `<div class="titr" style="left:0;right:0;bottom:120px;text-align:center;opacity:${1 - out}">
      <span style="display:inline-block;padding:18px 34px;background:rgba(10,10,20,.82);font:700 50px Onest;border-radius:10px">${esc(txt.slice(0, n))}</span></div>`;
  }
  f.fade = dip(t, sc, 0.5, 0.25);
};

/* ═══ 2. Рабочее место Лёши, день ════════════════════════════════════════════════════════ */
function s2Terminal(t) {
  const L = [];
  const E = id => M(id + '.enter');
  L.push(cmd('s2.c1', t, { showCursor: true }));
  if (t >= E('s2.c1')) L.push(cmd('s2.c2', t, { showCursor: true }));
  if (t >= M('s2.err1')) {
    L.push('<span class="err">error NETSDK1100: To build a project targeting Windows on this operating system,</span>');
    L.push('<span class="err">set the EnableWindowsTargeting property to true.</span>');
    L.push(cmd('s2.c3', t, { showCursor: true }));
  }
  if (t >= E('s2.c3')) {
    const spin = '⠋⠙⠹⠸⠼⠴⠦⠧⠇⠏'[Math.floor(t * 12) % 10];
    L.push(`<span class="dim">  Determining projects to restore... ${t < M('s2.ok') ? spin : ''}</span>`);
  }
  if (t >= M('s2.ok')) {
    L.push('<span class="okc">Build succeeded.</span>');
    L.push('    0 Warning(s)');
    L.push('    0 Error(s)');
    L.push('');
  }
  if (t >= M('s2.term2')) L.push(cmd('s2.c4', t, { showCursor: true }));
  if (t >= M('s2.err2')) {
    const t2 = line('s2.t2');
    const k = seg(t, t2.t0, t2.t1);
    const hlA = t >= t2.t0 && k < 0.58, hlB = t >= t2.t0 && k >= 0.58 && t <= t2.t1 + 0.3;
    const hover = t >= M('s2.click') - 0.3;
    L.push('You must install or update .NET to run this application.');
    L.push('');
    L.push(`<span class="${hlA ? 'hl' : ''}">Framework: 'Microsoft.WindowsDesktop.App', version '10.0.0' (x64)</span>`);
    L.push('.NET location: /usr/lib/dotnet');
    L.push('');
    L.push(`<span class="err ${hlB ? 'hl' : ''}">No frameworks were found.</span>`);
    L.push('');
    L.push('To install missing framework, download:');
    L.push(`<span class="lnk ${hover ? 'hover' : ''}">https://aka.ms/dotnet-core-applaunch?framework=Microsoft.WindowsDesktop.App&amp;framework_version=10.0.0&amp;arch=x64&amp;rid=ubuntu.24.04-x64</span>`);
    L.push(promptLine(t));
  }
  return terminal({ lines: L });
}

function leshaTermFace(t) {
  const err1 = M('s2.err1'), ok = M('s2.ok'), err2 = M('s2.err2');
  const e = { mouth: mouthOf('lesha', t), t };
  if (t < err1) Object.assign(e, { smile: 0.45, brow: 0.2 });
  else if (t < M('s2.l1')) Object.assign(e, { brow: 0.35, eyes: 1.1 });
  else if (t < ok) Object.assign(e, { smile: 0.5, brow: 0.3 });
  else if (t < err2) Object.assign(e, { smile: 0.8, brow: 0.3 });
  else if (t < M('s2.l2')) Object.assign(e, { worried: 0.7, brow: -0.1, eyes: 1.15 });
  else Object.assign(e, { angry: 0.5, brow: -0.2 });
  return e;
}

SCENES.s2 = (t, f, sc) => {
  const term = M('s2.term'), lean = M('s2.lean'), term2 = M('s2.term2'), freeze = M('s2.freeze'), term3 = M('s2.term3');
  const page = M('s2.page');
  if (t < term) {
    const crack = seg(t, sc.start + 0.3, sc.start + 0.9) * (1 - seg(t, sc.start + 1.2, sc.start + 1.6));
    const push = t > sc.start + 0.7 && t < sc.start + 0.85 ? 10 : 0;
    f.world = office({
      t, mood: 'day', marina: { lookDown: 1, handL: [-46, -74], handR: [72, -76] },
      lesha: { turn: 0, smile: 0.5, brow: 0.25, handL: [lerp(-40, -10, crack), lerp(-60, -112 - push, crack), 70], handR: [lerp(44, 10, crack), lerp(-60, -112 - push, crack), 70] },
      leshaSetup: 'laptop', laptopGlow: '#9ad0ff',
    });
    f.view = keys(t, [[sc.start, [1540, 540, 2.0]], [term, [1540, 545, 2.15]]], k => k);
    f.fade = 1 - seg(t, sc.start, sc.start + 0.25);
    return;
  }
  if (t >= lean && t < term2) {
    // откинулся, победная улыбка, глоток кофе
    const grab = lean + 0.3, lift = lean + 0.9, sip = lean + 1.15, down = lean + 1.7;
    const hand = keys(t, [[lean, [-40, -60, 70]], [grab, [-150, -64, 40]], [lift, [-25, -185, 60]], [down + 0.1, [-25, -185, 60]], [down + 0.6, [-46, -150, 60]]]);
    const holding = t >= grab;
    f.world = office({
      t, mood: 'day', marina: { lookDown: 1, handL: [-46, -74], handR: [72, -76] },
      lesha: { turn: -0.1, tilt: -6, smile: t > sip && t < down ? 0 : 0.85, brow: 0.35, eyes: t > sip && t < down ? 0.2 : 1,
               handL: hand, holdL: holding ? 'mug' : null, mugColor: LESHA_MUG, handR: [44, -60, 70], y: 752 },
      leshaSetup: 'laptop', leshaMug: holding ? false : undefined,
    });
    f.view = keys(t, [[lean, [1540, 520, 2.25]], [term2, [1540, 520, 2.4]]], k => k);
    return;
  }
  if (t >= freeze && t < term3) {
    f.world = office({
      t: freeze, mood: 'day', marina: { lookDown: 1, handL: [-46, -74], handR: [72, -76] },
      lesha: { turn: -0.1, tilt: -3, eyes: 1.35, o: true, brow: 0.9, handL: [-46, -150, 60], holdL: 'mug', mugColor: LESHA_MUG, handR: [44, -60, 70], y: 752 },
      leshaSetup: 'laptop', leshaMug: false,
    });
    f.view = keys(t, [[freeze, [1532, 505, 2.9]], [term3, [1532, 505, 3.15]]], k => k);
    f.worldFilter = 'saturate(.25) contrast(1.15) brightness(.95)';
    f.overlay = `<div style="position:absolute;inset:0;box-shadow:inset 0 0 220px rgba(0,0,0,.6)"></div>`;
    return;
  }
  if (t >= page) {
    const z = easeIO(seg(t, page + 0.3, page + 2.0));
    f.screen = browser({ tabs: ['Download .NET 10.0 (Linux, macOS, and Windows)'], url: 'dotnet.microsoft.com/en-us/download/dotnet/10.0', page: downloadPage(z) });
    f.screenTransform = `scale(${1 + 0.45 * z})`;
    f.screenOrigin = '1350px 420px';
    f.cam = { expr: { t, worried: 1, brow: -0.2, eyes: 0.85, mouth: mouthOf('lesha', t) * 0.6 }, mood: 'day' };
    f.fade = seg(t, sc.end - 0.3, sc.end);
    return;
  }
  f.screen = s2Terminal(t);
  f.cam = { expr: leshaTermFace(t), mood: 'day' };
  if (t >= term3) {
    const c = M('s2.click');
    if (t > c - 1.2) f.cursor = cursorPath(t, [[c - 1.2, 1500, 520], [c - 0.1, 700, 818]], [c]);
  }
};

/* ─── доска: текст пишется рукой с маркером; маркер ведёт конец строки ───────────────────── */
let _measure = null;
function textWidth(str, size) {
  if (!_measure) _measure = document.createElement('canvas').getContext('2d');
  _measure.font = `700 ${size}px Caveat`;
  return _measure.measureText(str).width;
}

function boardShot(t, items, from, to, wipeAt = null) {
  const shown = items.map(it => ({ ...it, k: seg(t, it.t0, it.t0 + it.dur) }));
  let pen = null;
  for (const it of shown) {
    if (t < it.t0 - 0.35 || t > it.t0 + it.dur + 0.25) continue;
    const n = Math.round(it.text.length * it.k);
    const lines = it.text.slice(0, n).split('\n');
    const li = lines.length - 1;
    const x = it.x + textWidth(lines[li], it.size);
    const y = it.y + li * it.size * 1.05 - it.size * 0.22;
    pen = [x + 6, y];
  }
  let wipe = 0, arm = '';
  if (wipeAt !== null && t >= wipeAt) {
    // стирает рукавом: предплечье идёт слева направо и трёт вверх-вниз по обеим строкам
    const k = seg(t, wipeAt, wipeAt + 1.0);
    const px = lerp(200, 1760, easeIO(k));
    wipe = clamp((px - 128) / 1664);
    if (k < 1) arm = armFromBelow(CAST.lesha, px, 470 + 190 * Math.sin(t * 22), null, 1.25);
  } else if (pen) {
    arm = armFromBelow(CAST.lesha, pen[0], pen[1] + 8 * Math.sin(t * 40));
  } else {
    // между строками рука отходит вниз, к краю кадра
    const last = [...items].reverse().find(it => t >= it.t0 + it.dur) || items[0];
    const k = seg(t, last.t0 + last.dur + 0.25, last.t0 + last.dur + 0.7);
    if (k < 1) arm = armFromBelow(CAST.lesha, lerp(700, 420, k), lerp(600, 1160, k));
  }
  return whiteboardSvg(shown, wipe) + arm;
}

/* ═══ 3. Пять стадий ═════════════════════════════════════════════════════════════════════ */
const FORUM_TABS = ['winforms linux — Поиск', 'Can I run WinForms on Linux?', 'Mono WinForms (2008)', 'Wine + .NET 8 ???',
  'WindowsDesktop.App linux', 'NETSDK1100 — что делать', 'Avalonia vs WinForms', 'Переписать на веб за выходные',
  'Is WinForms dead?', 'dotnet/winforms: Linux support', 'Wine: .NET Desktop Runtime', 'WinForms в Docker?!'];

SCENES.s3 = (t, f, sc) => {
  const den = M('s3.denial'), ang = M('s3.anger'), bar = M('s3.bargain'), dep = M('s3.depr'), acc = M('s3.accept');
  const clock = keys(t, [[den, 11 * 3600], [acc, 23 * 3600 + 20 * 60], [sc.end, 23 * 3600 + 40 * 60]], k => k);
  let label = '', cups = 2;
  if (t < ang) {
    label = 'Отрицание.';
    const L = [];
    for (let i = 0; i < 3; i++) {
      const id = `s3.r${i}`;
      if (i > 0 && t < M(`s3.r${i - 1}.out`)) break;
      L.push(cmd(id, t, { showCursor: true }));
      if (t >= M(id + '.out')) {
        L.push('You must install or update .NET to run this application.');
        L.push(`<span class="err">No frameworks were found.</span>`);
      }
    }
    f.screen = terminal({ lines: L, y: 200, h: 760 });
    f.cam = { expr: { t, eyes: 0.95, brow: 0, mouth: 0 }, mood: 'day' };
  } else if (t < bar) {
    label = 'Гнев.'; cups = 3;
    const k = seg(t, ang, ang + 3.4);
    const n = 3 + Math.floor(easeI(k) * 48);
    const tabs = Array.from({ length: n }, (_, i) => FORUM_TABS[i % FORUM_TABS.length]);
    const tabW = Math.max(26, Math.min(300, 1880 / n - 2));
    const page = `<div style="padding:50px 140px;font-family:Onest">
      <div style="font:800 46px Onest;color:#202124;margin-bottom:26px">WinForms на Linux?</div>
      ${[['Вопрос', 'Есть WinForms-приложение, 43 формы. Как запустить на Linux?', '#f1f3f4'],
         ['Ответ', 'Никак. Windows only.', '#fde8ec'], ['Ответ', 'Попробуйте Wine.', '#fff'], ['Ответ', 'Перепишите на Avalonia.', '#fff'],
         ['Ответ', 'Перепишите на веб.', '#fff'], ['Ответ', 'Зачем вам Linux?', '#fff']].map(([a, b, bg]) =>
        `<div style="padding:22px 28px;margin-bottom:14px;border:1px solid #e0e0e0;border-radius:12px;background:${bg}"><div style="font:700 20px Onest;color:#70757a">${a}</div><div style="font:500 32px Onest;color:#202124;margin-top:6px">${b}</div></div>`).join('')}</div>`;
    f.screen = browser({ tabs, active: n - 1, url: 'forum.example/t/winforms-na-linux', page, tabW });
    f.cam = { expr: { t, angry: 1, red: 0.7, teeth: true, brow: -0.4 }, mood: 'anger' };
  } else if (t < dep) {
    label = 'Торг.'; cups = 5;
    const items = [
      { x: 330, y: 330, text: 'Wine?', t0: M('s3.b0'), dur: 0.8, size: 104 },
      { x: 330, y: 470, text: 'Mono?', t0: M('s3.b1'), dur: 0.8, size: 104 },
      { x: 330, y: 620, text: 'Виртуалка с Windows\nна каждом рабочем месте?', t0: M('s3.b2'), dur: 2.4, size: 96 },
    ];
    let shake = 0;
    for (const it of items) {
      const tb = it.t0 + it.dur + 0.05;
      if (t >= tb && t < tb + 0.8) shake = Math.sin((t - tb) * 18) * (1 - (t - tb) / 0.8);
    }
    f.world = boardShot(t, items, bar, dep) +
      person('gena', { t, x: 1665, y: 1000, scale: 1.25, standing: true, turn: shake * 0.9, tilt: shake * 5,
        handL: [34, -70, 45], handR: [-34, -66, 50], brow: -0.15, lookDown: 0.1 });
    f.view = [960, 540, 1];
  } else if (t < acc) {
    label = 'Депрессия.'; cups = 7;
    const w = M('s3.wipe');
    const items = [
      { x: 300, y: 330, text: 'Переписать на Avalonia — 43 формы —\nоценка: 4 месяца', t0: M('s3.d0'), dur: 2.0, size: 86 },
      { x: 300, y: 640, text: 'Переписать на веб — 3 квартала', t0: M('s3.d1'), dur: 1.4, size: 86, color: '#C0392B' },
    ];
    f.world = boardShot(t, items, dep, acc, w);
    f.view = [960, 540, 1];
  } else {
    label = 'Принятие?'; cups = 7;
    f.world = office({
      t, mood: 'night', clock: clock, lesha: { turn: 0, lookDown: 0.3, eyes: 0.9, handL: [-40, -60, 70], handR: [44, -60, 70] },
      leshaSetup: 'laptop', laptopGlow: '#9ad0ff', screenGlow: 0.12,
    });
    f.view = keys(t, [[acc, [1500, 520, 1.45]], [sc.end, [1540, 530, 1.9]]], k => k);
  }
  const lk = seg(t, [den, ang, bar, dep, acc].filter(x => x <= t).pop(), [den, ang, bar, dep, acc].filter(x => x <= t).pop() + 0.2);
  f.overlay = `<div class="stage-label" style="opacity:${lk};transform:translateX(${(1 - lk) * -30}px)">${label}</div>` + coffeeBadge(cups, t, clock);
  f.fade = dip(t, sc, 0.2, 0.3);
};

/* ═══ 4. Ночь ════════════════════════════════════════════════════════════════════════════ */
const CSPROJ = [
  '<Project Sdk="Microsoft.NET.Sdk">', '', '  <PropertyGroup>', '    <OutputType>WinExe</OutputType>',
  '    <TargetFramework>net8.0-windows</TargetFramework>', '    <Nullable>enable</Nullable>',
  '    <UseWindowsForms>true</UseWindowsForms>', '    <ImplicitUsings>enable</ImplicitUsings>', '  </PropertyGroup>', '', '</Project>',
];
const ADD_INDENT = ['  ', '    ', '    ', '    ', '  ', ''];

function csprojAt(t) {
  const rows = CSPROJ.map(text => ({ text }));
  const bs = M('s4.bs');
  let caretRow = -1, caretCol = 0, dirty = false;
  const edit = M('s4.edit');
  if (t >= edit + 0.15) { caretRow = 4; caretCol = rows[4].text.indexOf('</Target'); }
  if (t >= bs - 0.35 && t < bs) rows[4].glow = null, rows[4].sel = true;
  if (t >= bs) {
    dirty = true;
    const nten = typed('s4.ten', t);
    let fw = 'net8.0';
    if (nten >= 1) fw = 'net.0';
    if (nten >= 2) fw = 'net1.0';
    if (nten >= 3) fw = 'net10.0';
    rows[4].text = `    <TargetFramework>${fw}</TargetFramework>`;
    rows[4].gut = 'm';
    caretRow = 4; caretCol = 21 + (nten === 0 ? 4 : nten === 1 ? 3 : nten === 2 ? 4 : 5);
  }
  // строка UseWindowsForms: три удара
  let useRow = 6;
  if (t >= M('s4.del0') - 0.2) { caretRow = 6; caretCol = rows[6].text.length; }
  if (t >= M('s4.del0')) { rows[6].text = '    <UseWindowsForms>true'; caretCol = rows[6].text.length; }
  if (t >= M('s4.del1')) { rows[6].text = '    '; caretCol = 4; }
  if (t >= M('s4.del2')) { rows.splice(6, 1); useRow = -1; caretRow = 5; caretCol = rows[5].text.length; }
  // вставка ItemGroup перед </Project>
  const addStart = TL.typing['s4.add'].t0;
  if (t >= addStart - 0.25) {
    const endIdx = rows.length - 1; // </Project>
    const txt = typedText('s4.add', t);
    const parts = txt.split('\n');
    const newRows = parts.slice(0, -1).map((p, i) => ({ text: ADD_INDENT[i] + p, gut: 'a' }));
    const cur = parts[parts.length - 1];
    const ci = parts.length - 1;
    if (ci < ADD_INDENT.length) {
      const curRow = { text: ADD_INDENT[ci] + cur, gut: 'a' };
      rows.splice(endIdx, 0, ...newRows, curRow);
      caretRow = endIdx + newRows.length; caretCol = curRow.text.length;
    } else {
      rows.splice(endIdx, 0, ...newRows);
      caretRow = endIdx + newRows.length; caretCol = 0;
    }
  }
  if (t >= M('s4.save')) dirty = false;
  if (caretRow >= 0 && rows[caretRow]) { rows[caretRow].caret = caretCol; rows[caretRow].solid = t >= bs && t < M('s4.save'); }
  // подсветка «виноватых» строк, пока Лёша их называет
  const z1 = line('s4.z1'), z2 = line('s4.z2');
  if (t >= z1.t0 && t < z2.t0 + 0.1 && t < edit) rows[4].glow = [rows[4].text.indexOf('-windows'), 8], rows[4].caret = null;
  if (t >= z2.t0 && t < edit && useRow === 6) rows[6].glow = [4, rows[6].text.length - 4], rows[6].caret = null;
  if (rows[4].sel) {
    rows[4].caret = null;
    rows[4].glow = [rows[4].text.indexOf('-windows'), 8];
    rows[4].glowClass = 'selb';
  }
  return { rows, dirty, caretRow };
}

function editorHtml(t, panelHtml, panelK) {
  const { rows, dirty, caretRow } = csprojAt(t);
  return editor({ rows, dirty, curRow: caretRow, t, panel: panelHtml, scroll: panelK * 240 });
}

function leshaNightFace(t) {
  const e = { t, mouth: mouthOf('lesha', t) };
  const r1 = M('s4.r1'), r4e = M('s4.r4.end'), edit = M('s4.edit'), save = M('s4.save'), run = M('s4.run');
  const sil = M('s4.silence'), win = M('s4.window');
  if (t < M('s4.chat')) Object.assign(e, { angry: 0.9, teeth: !speaking('lesha', t), brow: -0.3, red: 0.15 });
  else if (t < r1) Object.assign(e, { angry: 0.3, brow: 0.1, look: [0.8, 0.5] });
  else if (t < edit) {
    const k = seg(t, r1, r4e);
    Object.assign(e, { brow: 0.1 + 0.9 * k, eyes: 1 + 0.35 * k });
    if (t >= M('s4.l1')) Object.assign(e, { brow: 1, eyes: 1.35 });
  } else if (t < save) Object.assign(e, { angry: 1, red: 0.25 + 0.45 * seg(t, edit, save), teeth: true, brow: -0.5 });
  else if (t < run) Object.assign(e, { angry: 0.5, smirk: 1, smile: 0.2, brow: -0.2 });
  else if (t < sil) Object.assign(e, { angry: 0.3, brow: 0 });
  else if (t < win) Object.assign(e, { puff: seg(t, sil + 0.3, sil + 0.8), eyes: 1.2, brow: 0.4 });
  else if (t < M('s4.l4')) Object.assign(e, { eyes: 1.35, brow: 1, o: !speaking('lesha', t), mouth: e.mouth });
  else Object.assign(e, { brow: 0.8, smile: 0.6, eyes: 1.1 });
  return e;
}

SCENES.s4 = (t, f, sc) => {
  const chat = M('s4.chat'), link = M('s4.link'), page = M('s4.page'), edit = M('s4.edit'), save = M('s4.save');
  const run = M('s4.run'), win = M('s4.window');
  f.cam = { expr: leshaNightFace(t), mood: 'night' };
  if (t >= page && t < edit) {
    const hl = ['s4.r1', 's4.r2', 's4.r3', 's4.r4'].map(id => { const l = line(id); return seg(t, l.t0 - 0.1, l.t1 - 0.2); });
    const z = easeIO(seg(t, page + 0.5, M('s4.r1')));
    f.screen = browser({ tabs: ['NetForms — Windows Forms for Windows and Linux'], url: 'go-forms.github.io/.NetForms/ru/', page: netformsPage(hl) });
    f.screenTransform = `scale(${1 + 0.32 * z})`;
    f.screenOrigin = '0px 620px';
    f.fade = 0;
    return;
  }
  const panelK = easeO(seg(t, run, run + 0.3));
  let panel = null;
  if (t >= run) {
    const ty = TL.typing['s4.cr'];
    const L = [cmd('s4.cr', t, { showCursor: true })];
    if (t >= M('s4.cr.enter')) L.push(cursorOn(t) ? '<span class="cur"></span>' : '');
    panel = `<div class="panel" style="height:${300 * panelK}px"><div class="ph">ТЕРМИНАЛ</div><div class="pb">${L.join('\n')}</div></div>`;
    void ty;
  }
  f.screen = editorHtml(t, panel, panelK);
  if (t < M('s4.chat') + 99 && t < page) {
    if (t >= chat) {
      const k = easeO(seg(t, chat, chat + 0.35));
      f.toast = chatToast(1920 - 560 * k + 20, 420, t >= link - 0.35);
      if (t > chat + 0.9) f.cursor = cursorPath(t, [[chat + 0.9, 1100, 600], [link - 0.1, 1560, 640]], [link]);
    }
  }
  // ⌫ и Ctrl+S
  let o = '';
  o += keycap('⌫ Backspace', 960, 800, M('s4.bs') - 0.04, t);
  for (let i = 0; i < 3; i++) o += keycap('⌫', 960, 800, M(`s4.del${i}`) - 0.04, t, 0.36);
  o += keycap('Ctrl + S', 960, 760, save - 0.05, t, 1.1, 76);
  if (t >= save && t < save + 0.25) o += `<div style="position:absolute;inset:0;background:#fff;opacity:${0.45 * (1 - seg(t, save, save + 0.25))}"></div>`;
  f.overlay = o;
  f.shake = shakeAt(t);

  if (t >= win) {
    const meta = FILM();
    const [cw, ch] = meta.mainClient;
    const u = 1.18;
    const k = easeO(seg(t, win, win + 0.4));
    const s = u * (0.93 + 0.07 * k);
    const W = cw * s, H = (ch + 38) * s;
    const x = (1920 - W) / 2, y = (1080 - H) / 2 - 16;
    // клики по вкладкам
    let src = 'main-0';
    for (let i = 0; i < 6; i++) if (t >= M(`s4.tab${i}`)) src = i < 5 ? `main-tab${i + 1}` : 'main-back';
    f.win = { src, title: meta.mainTitle, x, y, s, opacity: k };
    f.screenFilter = `brightness(${1 - 0.45 * k})`;
    const tabsAt = i => {
      const r = meta.tabs[i < 5 ? i + 1 : 0];
      return [x + (r[0] + r[2] / 2) * s, y + (38 + r[1] + r[3] / 2) * s];
    };
    const pts = [[M('s4.tab0') - 0.8, 1300, 900]];
    const clicks = [];
    for (let i = 0; i < 6; i++) {
      const c = M(`s4.tab${i}`);
      const [px, py] = tabsAt(i);
      pts.push([c - 0.05, px, py]);
      pts.push([c + 0.12, px, py]);
      clicks.push(c);
    }
    if (t > M('s4.tab0') - 0.8) f.cursor = cursorPath(t, pts, clicks);
  }
  f.fade = Math.max(1 - seg(t, sc.start, sc.start + 0.4), seg(t, sc.end - 0.6, sc.end));
};

/* ═══ 5. Утро ════════════════════════════════════════════════════════════════════════════ */
function marinaBackFg() {
  const c = CAST.marina;
  return `<svg viewBox="0 0 1920 1080" width="1920" height="1080" style="position:absolute;inset:0">
    <path d="M-80,1100 Q-60,990 60,960 L330,960 Q430,980 450,1100Z" fill="${c.top}"/>
    <ellipse cx="195" cy="880" rx="118" ry="132" fill="${c.hair}"/>
    <circle cx="195" cy="735" r="54" fill="${c.hair}"/><path d="M158,728 Q195,698 232,728" stroke="#B9B2AA" stroke-width="5" fill="none" opacity=".7"/>
    <ellipse cx="80" cy="890" rx="14" ry="24" fill="${c.skinD}"/><ellipse cx="310" cy="890" rx="14" ry="24" fill="${c.skinD}"/>
  </svg>`;
}

function genaBackFg() {
  const c = CAST.gena;
  return `<svg viewBox="0 0 1920 1080" width="1920" height="1080" style="position:absolute;inset:0">
    <path d="M1380,1100 Q1400,960 1540,930 L1820,930 Q1960,960 1980,1100Z" fill="${c.top}"/>
    ${[1470, 1560, 1650, 1740].map(x => `<path d="M${x},940 L${x - 20},1090" stroke="${c.topD}" stroke-width="10" opacity=".6"/>`).join('')}
    <ellipse cx="1680" cy="840" rx="112" ry="128" fill="${c.hair}"/>
    <ellipse cx="1566" cy="850" rx="16" ry="26" fill="${c.skinD}"/><ellipse cx="1794" cy="850" rx="16" ry="26" fill="${c.skinD}"/>
    <path d="M1600,960 Q1680,990 1760,960 L1760,1000 L1600,1000Z" fill="${c.skinD}"/>
  </svg>`;
}

SCENES.s5 = (t, f, sc) => {
  const gIn = M('s5.genaIn'), look = M('s5.look'), roll = M('s5.roll'), g2 = M('s5.g2'), check = M('s5.check');
  const two = M('s5.two'), cof = M('s5.coffee');

  if (t >= check && t < two) {
    // из-за плеча Марины Петровны: «Приход», поиск, количество, F2
    const meta = FILM();
    const [cw, ch] = meta.mainClient;
    const u = 1.22;
    const W = cw * u, H = (ch + 38) * u;
    const x = (1920 - W) / 2 + 60, y = 70;
    const prihod = M('s5.prihod'), dlgT = M('s5.dialog'), sr = M('s5.search'), row = M('s5.row'), qty = M('s5.qty'), f2 = M('s5.f2');
    let src = 'main-back';
    if (t >= prihod - 0.45) src = 'main-hover-prihod';
    if (t >= f2 + 0.12) src = 'main-saved';
    f.win = { src, title: meta.mainTitle, x, y, s: u };
    const [dw, dh] = meta.dialogClient;
    const dx = x + (W - dw * u) / 2, dy = y + (H - (dh + 38) * u) / 2 + 20;
    if (t >= dlgT && t < f2 + 0.12) {
      const n = typed('s5.find', t);
      let dsrc = n > 0 ? `prihod-s${n}` : 'prihod-0';
      if (t >= row) dsrc = 'prihod-row';
      if (typed('s5.num', t) >= 2) dsrc = 'prihod-qty';
      const k = easeO(seg(t, dlgT, dlgT + 0.2));
      f.dlg = { src: dsrc, title: meta.dialogTitle, x: dx, y: dy + (1 - k) * 12, s: u, opacity: k };
    }
    const bp = meta.btnPrihod, ts = meta.txtSearch, gr = meta.goodsRow0, nq = meta.numQty;
    const mainPt = (r, ox = 0.5) => [x + (r[0] + r[2] * ox) * u, y + (38 + r[1] + r[3] / 2) * u];
    const dlgPt = (r, ox = 0.5) => [dx + (r[0] + r[2] * ox) * u, dy + (38 + r[1] + r[3] / 2) * u];
    const pts = [[check, 1200, 700], [prihod - 0.5, ...mainPt(bp)], [prihod + 0.2, ...mainPt(bp)],
      [sr - 0.4, ...dlgPt(ts, 0.3)], [sr + 0.1, ...dlgPt(ts, 0.3)], [row - 0.3, ...dlgPt(gr, 0.3)], [row + 0.1, ...dlgPt(gr, 0.3)],
      [qty - 0.3, ...dlgPt(nq, 0.3)], [f2 + 0.5, ...dlgPt(nq, 0.3)], [f2 + 1.2, 1500, 820]];
    f.cursor = cursorPath(t, pts, [prihod, sr, row, qty]);
    f.screen = `<div style="position:absolute;inset:0;background:linear-gradient(160deg,#3b3a47,#1f1d27)">
      <div style="position:absolute;left:40px;top:20px;right:40px;bottom:-40px;border-radius:30px;background:#16171b"></div>
      <div style="position:absolute;left:70px;top:44px;right:70px;bottom:0;background:radial-gradient(90% 90% at 40% 30%, #4d6f9a, #253754)"></div>
      <div style="position:absolute;left:70px;top:44px;right:70px;height:32px;background:#101114;color:#e8e8e8;font:600 18px/32px 'Noto Sans';text-align:center">вт 22 сен 09:02</div></div>`;
    f.fg = marinaBackFg() + keycap('F2', 1280, 860, f2 - 0.04, t, 0.9, 72);
    if (t >= f2 + 0.2) {
      const k = seg(t, f2 + 0.2, f2 + 0.5) * (1 - seg(t, two - 0.3, two));
      f.overlay = `<div style="position:absolute;left:${x}px;top:${y + (38 + ch - 22) * u}px;width:${680 * u}px;height:${22 * u}px;border-radius:4px;box-shadow:0 0 0 4px rgba(76,201,138,${0.9 * k}), 0 0 30px rgba(76,201,138,${0.6 * k})"></div>`;
    }
    f.fade = 0;
    return;
  }

  // Врезки: экран ноутбука Лёши (уснул на клавише «s») и второй монитор со «Складом» из-за плеча Гены
  if (t < gIn) {
    const n = 60 + Math.floor((t - sc.start) * 14);
    const L = ['<span class="pr">$</span> dotnet run', '', '<span class="pr">$</span> ' + 's'.repeat(n) + '<span class="cur"></span>'];
    f.screen = `<div style="position:absolute;inset:0;background:#0d0c12"></div>` +
      terminal({ x: 110, y: 70, w: 1700, h: 940, title: 'lesha@sklad-laptop: ~/sklad', lines: L, font: 34 }) +
      `<div style="position:absolute;inset:0;background:radial-gradient(80% 70% at 30% 20%, rgba(255,190,120,.10), transparent 70%)"></div>`;
    f.screenTransform = `scale(${1.04 - 0.04 * seg(t, sc.start, gIn)})`;
    f.fade = 1 - seg(t, sc.start, sc.start + 0.5);
    return;
  }
  const g1 = M('s5.g1');
  if (t >= look && t < g1) {
    const meta = FILM();
    const u = 1.18;
    f.screen = `<div style="position:absolute;inset:0;background:#15161a"><div style="position:absolute;left:60px;top:30px;right:60px;bottom:30px;border-radius:24px;background:#23262c"></div>
      <div style="position:absolute;left:90px;top:60px;right:90px;bottom:60px;background:radial-gradient(90% 90% at 40% 30%, #4d6f9a, #253754)"></div>
      <div style="position:absolute;left:90px;top:60px;right:90px;height:34px;background:#101114;color:#e8e8e8;font:600 19px/34px 'Noto Sans';text-align:center">вт 22 сен 08:51</div></div>`;
    f.win = { src: 'main-back', title: meta.mainTitle, x: 210, y: 130, s: u };
    f.fg = genaBackFg();
    f.screenTransform = `scale(${1 + 0.03 * seg(t, look, g1)})`;
    return;
  }

  // Гена: входит с кофе, смотрит, берёт ноутбук, несёт Марине Петровне, потом ставит кофе рядом с Лёшей
  const GA = 1195, GB = 735;
  const mouth = mouthOf('gena', t);
  let gena, lap = null, lapCarried = '', marinaLaptop = null, mugOnDesk = null;
  const pickK = easeIO(seg(t, roll, roll + 0.8));
  if (t < look) {
    const w = walkSeg(t, gIn, gIn + 2.5, 560, GA, 150);
    gena = w.moving ? { x: w.x, y: 640, side: 1, phase: w.phase, S: w.S, amp: w.amp, near: [56, -60], holdNear: 'mug', steam: true, mouth }
                    : { x: GA, y: 640, handL: [-44, -60, 50], holdL: 'mug', steam: true, turn: 0.55, lookDown: 0.3, mouth };
    lap = [1296, 594];
  } else if (t < roll + 0.95) {
    // «Хм.» — и левой рукой (справа в кадре) берёт ноутбук со стола Лёши
    lap = [lerp(1296, 1212, pickK), lerp(594, 540, pickK) - Math.sin(pickK * Math.PI) * 20];
    const hand = t >= roll ? [lap[0] + 22 - GA, lap[1] + 48 - 640, 35] : null;
    gena = { x: GA, y: 640, handL: [-44, -60, 50], holdL: 'mug', steam: true, handR: hand, turn: t < roll ? 0.55 : 0.3,
             lookDown: t < roll ? 0.35 : 0.2, brow: 0.3, mouth };
    if (t >= roll) { lapCarried = laptopBack(lap[0], lap[1], 150, 84); lap = null; }
  } else if (t < g2) {
    // несёт ноутбук к Марине Петровне и ставит ей на стол, не останавливаясь
    const w = walkSeg(t, roll + 1.0, roll + 3.4, GA, GB, 150);
    const pk = easeIO(seg(t, roll + 3.4, roll + 3.95));
    const put = t >= roll + 3.4;
    const lean = put ? 18 * Math.sin(Math.min(pk, 1) * Math.PI * 0.5) * (1 - seg(t, roll + 3.95, g2)) : 0;
    const near = put ? [lerp(62, 150, pk), lerp(-40, 30, pk)] : [62, -40];
    gena = { x: w.x, y: 640, side: -1, phase: w.phase, S: w.S, amp: w.amp, near, holdNear: t < roll + 3.95 ? 'laptop' : null,
             far: [52, -60], holdFar: 'mug', steam: true, lean, mouth };
    if (t >= roll + 3.95) marinaLaptop = [500, 594];
  } else if (t < cof + 0.3) {
    marinaLaptop = [500, 594];
    gena = { x: GB, y: 640, handL: [-44, -60, 50], holdL: 'mug', steam: true, turn: t < two ? -0.45 : -0.5,
             mouth, smile: t >= M('s5.m2') ? 0.2 : 0.05, brow: t >= M('s5.g3') && t < M('s5.m2') ? 0.2 : 0 };
  } else {
    // к Лёше, ставит кофе рядом — наклонившись к столу
    marinaLaptop = [500, 594];
    const w = walkSeg(t, cof + 0.3, cof + 2.2, GB, 1200, 150);
    const pk = easeIO(seg(t, cof + 2.2, cof + 2.75));
    const back = seg(t, cof + 2.9, cof + 3.4);
    const lean = 24 * pk * (1 - back);
    const th = lean * Math.PI / 180;
    const wx = 1300 - 1200 - 12, wy = 684 - 640 - 6;           // куда встанет кружка, от таза
    const tgt = [wx * Math.cos(th) + wy * Math.sin(th), -wx * Math.sin(th) + wy * Math.cos(th)];
    const placed = t >= cof + 2.75;
    const near = placed ? [lerp(tgt[0], 30, back), lerp(tgt[1], 40, back)] : [lerp(56, tgt[0], pk), lerp(-60, tgt[1], pk)];
    gena = { x: w.x, y: 640, side: 1, phase: w.phase, S: w.S, amp: w.amp, near, holdNear: placed ? null : 'mug', steam: true, lean,
             far: null, mouth, smile: back > 0 ? 0.4 : 0, lookDown: 0.3 };
    if (placed) mugOnDesk = [1300, 684];
  }
  const marinaTalk = t >= two && t < cof;
  const marina = {
    lookDown: marinaTalk || (t >= g2 && t < check) ? 0 : 0.5, turn: marinaTalk || (t >= g2 && t < check) ? 0.6 : 0.3,
    mouth: mouthOf('marina', t), handL: [-46, -74], handR: [120, -70, 60], brow: t >= M('s5.m2') && t < cof ? 0.1 : 0,
    smile: t >= M('s5.m2') && t < cof ? 0.25 : 0,
  };
  f.world = office({
    t, mood: 'morning', clock: 8 * 3600 + 50 * 60 + (t - sc.start) * 60,
    marina, lesha: { hidden: true, sleepHead: true }, gena, leshaSetup: 'morning', leshaLaptop: lap, leshaMug: false,
    marinaLaptop, genaMugOnDesk: mugOnDesk, carried: lapCarried,
  });

  if (t < gIn + 1.4) f.view = [1470, 540, 1.9];
  else if (t < roll + 1.0) f.view = keys(t, [[gIn + 1.4, [1470, 540, 1.9]], [gIn + 2.6, [1320, 540, 1.6]]]);
  else if (t < check) f.view = keys(t, [[roll + 1.0, [1320, 540, 1.6]], [roll + 3.4, [660, 540, 1.6]]]);
  else if (t < cof + 0.3) f.view = keys(t, [[two, [600, 530, 1.7]], [cof, [610, 530, 1.75]]], k => k);
  else f.view = keys(t, [[cof + 0.3, [610, 530, 1.75]], [cof + 2.3, [1340, 540, 1.8]]]);
  f.fade = seg(t, sc.end - 0.7, sc.end);
};

/* ═══ 6. Эпилог ══════════════════════════════════════════════════════════════════════════ */
function logoAnim(t, t0, size) {
  const k = t - t0;
  const pop = back(seg(t, t0, t0 + 0.45));
  // форма меняет размер, кнопка едет за правым нижним углом (Anchor = Bottom | Right)
  const rw = 168 + 40 * Math.sin(Math.max(0, k - 0.6) * 2.4) * seg(t, t0 + 0.6, t0 + 1.0);
  const rh = 136 + 26 * Math.sin(Math.max(0, k - 0.6) * 2.4 + 0.8) * seg(t, t0 + 0.6, t0 + 1.0);
  const fx = 44 - (rw - 168) / 2, fy = 60 - (rh - 136) / 2;
  const x2 = fx + rw, y2 = fy + rh;
  return `<svg viewBox="0 0 256 256" width="${size}" height="${size}" style="transform:scale(${pop})"><defs><linearGradient id="nfbg2" x1="0" y1="0" x2="1" y2="1"><stop offset="0" stop-color="#6A3DE8"/><stop offset="1" stop-color="#2A1A8A"/></linearGradient></defs>
    <rect x="8" y="8" width="240" height="240" rx="52" fill="url(#nfbg2)"/>
    <rect x="${fx}" y="${fy}" width="${rw}" height="${rh}" rx="10" fill="#F4F2FB"/>
    <path d="M${fx} ${fy + 10}a10 10 0 0 1 10-10h${rw - 20}a10 10 0 0 1 10 10v18H${fx}z" fill="#FFFFFF"/>
    <rect x="${fx}" y="${fy + 27}" width="${rw}" height="2" fill="#D9D3F2"/>
    <circle cx="${x2 - 26}" cy="${fy + 14}" r="5" fill="#E0457B"/><circle cx="${x2 - 42}" cy="${fy + 14}" r="5" fill="#C9C2E8"/><circle cx="${x2 - 58}" cy="${fy + 14}" r="5" fill="#C9C2E8"/>
    <rect x="${fx + 12}" y="${fy + 10}" width="52" height="8" rx="4" fill="#B7AEE0"/>
    <rect x="${fx + 16}" y="${fy + 44}" width="64" height="9" rx="4.5" fill="#6A3DE8"/>
    <rect x="${fx + 16}" y="${fy + 62}" width="${rw - 32}" height="20" rx="4" fill="#FFFFFF" stroke="#B7AEE0" stroke-width="2"/>
    <rect x="${x2 - 74}" y="${y2 - 36}" width="60" height="24" rx="5" fill="#6A3DE8"/><rect x="${x2 - 60}" y="${y2 - 26.5}" width="32" height="5" rx="2.5" fill="#FFFFFF"/></svg>`;
}

SCENES.s6 = (t, f, sc) => {
  const o1 = M('s6.o1'), tr1 = M('s6.titr1'), calm = M('s6.calm'), o3 = M('s6.o3'), tr2 = M('s6.titr2'), logo = M('s6.logo'), fin = M('s6.final');
  const bg = `<div style="position:absolute;inset:0;background:radial-gradient(80% 80% at 50% 40%, #1d1832 0%, #0b0a12 75%)"></div>`;
  const box = (lines, op, font = 40) => `<div style="position:absolute;left:${font < 36 ? 110 : 180}px;right:${font < 36 ? 110 : 180}px;top:0;bottom:0;display:flex;flex-direction:column;justify-content:center;font:400 ${font}px/1.55 'JetBrains Mono';color:#d9d5ea;white-space:pre-wrap;opacity:${op}">${lines.map(l => `<div>${l || '&nbsp;'}</div>`).join('')}</div>`;
  let html = bg;
  if (t < calm) {
    const L = [cmd('s6.c1', t, { showCursor: true })];
    if (t >= o1) {
      L.push(' Sklad.csproj | 9 <span class="add">+++++++</span><span class="del">--</span>');
      L.push(' 1 file changed, 7 insertions(+), 2 deletions(-)');
    }
    html += box(L, 1 - seg(t, tr1, tr1 + 0.35));
    if (t >= tr1) {
      const a = seg(t, tr1 + 0.2, tr1 + 0.6), b = seg(t, tr1 + 1.2, tr1 + 1.6), out = seg(t, calm - 0.35, calm);
      html += `<div style="position:absolute;left:0;right:0;top:360px;text-align:center;font:800 84px/1.3 Onest;color:#fff;opacity:${1 - out}">
        <div style="opacity:${a};transform:translateY(${(1 - a) * 20}px)">Шесть часов на поиски.</div>
        <div style="opacity:${b};transform:translateY(${(1 - b) * 20}px)">Сорок секунд на <span style="font-family:'JetBrains Mono';color:#9c83ff">.csproj</span>.</div></div>`;
    }
  } else if (t < logo) {
    const L = [cmd('s6.c2', t, { showCursor: true })];
    if (t >= M('s6.c2.enter')) L.push(cmd('s6.c3', t, { showCursor: true }));
    const out = [
      'Sklad.csproj: target net8.0-windows, WinForms: yes', 'Changed:',
      '  - Remove &lt;UseWindowsForms&gt;true&lt;/UseWindowsForms&gt;.',
      '  - Reference NetForms: &lt;PackageReference Include="NetForms" Version="0.1.0-preview.1" /&gt;',
      '  - Target framework net8.0-windows → net10.0 (so it builds and runs on Linux too).',
      '  - Add &lt;Using Include="System.Drawing" /&gt; and &lt;Using Include="System.Windows.Forms" /&gt; (the implicit usings UseWindowsForms gave).',
      '<span class="okc">The sources compile against NetForms.</span>'];
    if (t >= o3) out.forEach((l, i) => { if (t >= o3 + i * 0.22) L.push(l); });
    const dim = seg(t, tr2, tr2 + 0.4);
    html += box(L, 1 - 0.9 * dim, 27);
    if (t >= tr2) {
      const a = seg(t, tr2 + 0.1, tr2 + 0.5), out2 = seg(t, logo - 0.3, logo);
      html += `<div style="position:absolute;left:0;right:0;top:450px;text-align:center;font:800 italic 88px/1.2 Onest;color:#fff;opacity:${a * (1 - out2)};text-shadow:0 10px 40px rgba(0,0,0,.8)">…а можно было и не злиться.</div>`;
    }
  } else {
    const up = easeIO(seg(t, fin, fin + 0.6));
    const size = lerp(380, 250, up);
    const y = lerp(250, 150, up);
    html += `<div style="position:absolute;left:0;right:0;top:${y}px;text-align:center">${logoAnim(t, logo, size)}</div>`;
    const cap = seg(t, logo + 0.8, logo + 1.2) * (1 - up);
    html += `<div style="position:absolute;left:0;right:0;top:680px;text-align:center;font:700 38px 'JetBrains Mono';color:#9c83ff;opacity:${cap}">Anchor = Bottom | Right</div>`;
    if (t >= fin) {
      const a = seg(t, fin + 0.3, fin + 0.8), b = seg(t, fin + 0.9, fin + 1.3), c = seg(t, fin + 1.4, fin + 1.8);
      html += `<div style="position:absolute;left:0;right:0;top:460px;text-align:center;color:#fff">
        <div style="font:700 64px/1.2 Unbounded;opacity:${a};transform:translateY(${(1 - a) * 16}px)">NetForms.</div>
        <div style="font:700 50px/1.4 Onest;opacity:${a};margin-top:10px;transform:translateY(${(1 - a) * 16}px)">Твой WinForms — теперь и на Linux.</div>
        <div style="display:inline-block;margin-top:44px;padding:20px 40px;border-radius:16px;background:#5b34d6;font:700 40px 'JetBrains Mono';opacity:${b};box-shadow:0 20px 50px rgba(91,52,214,.45)">dotnet add package NetForms</div>
        <div style="margin-top:36px;font:500 30px Onest;color:#8a86a3;opacity:${c}">github.com/Go-Forms/.NetForms</div></div>`;
    }
  }
  f.screen = html;
  f.fade = Math.max(1 - seg(t, sc.start, sc.start + 0.3), seg(t, M('s6.fade'), sc.end));
};
