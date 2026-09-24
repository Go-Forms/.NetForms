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
function genaWalk(t, t0, t1, x0, x1) {
  const k = easeIO(seg(t, t0, t1));
  const x = lerp(x0, x1, k);
  const walking = t > t0 && t < t1;
  return { x, walking, walk: Math.abs(x - x0) / 38 };
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

  // Гена с тележкой
  let g;
  if (t < g3 + 0.2) g = genaWalk(t, pull, stop, 2350, 760);
  else g = genaWalk(t, g3 + 0.2, g3 + 3.4, 760, -520);
  const cartX = g.x - 200;
  const place = seg(t, stop, stop + 0.9);
  const placed = t >= stop + 0.9;
  // ноутбук с тележки — на стол Марине Петровне
  let slab = null;
  if (t >= stop && !placed) {
    const k = easeIO(place);
    slab = [lerp(cartX, 440, k), lerp(740, 682, k) - Math.sin(k * Math.PI) * 120];
  }
  const handCart = [-72, 78];
  const genaTurn = t < M('s1.m1') ? -0.2 : t < turn ? -0.55 : t < g3 ? keys(t, [[turn, -0.55], [turn + 0.9, 0.85]]) : 0.8;
  const gena = {
    x: g.x, y: 640, walking: g.walking, walk: g.walk, turn: genaTurn,
    handL: slab ? [slab[0] - g.x + 20, slab[1] - 640] : handCart, handR: [-56, 84],
    mouth: mouthOf('gena', t), smile: t < turn ? 0.35 : 0.15, brow: t >= turn && t < g3 ? 0.25 : 0,
    lean: slab ? -8 * Math.sin(place * Math.PI) : 0,
  };
  const marinaUp = t >= m2 && t < l2;
  const marina = {
    lookDown: marinaUp ? 0 : 1, turn: marinaUp ? 0.25 : 0.1, mouth: mouthOf('marina', t),
    handL: [-46, -74], handR: [72, -76], smile: marinaUp ? -0.1 : 0, brow: marinaUp ? -0.1 : 0,
  };
  const lesha = {
    turn: t < turn ? 0.7 : t < l1 - 0.3 ? keys(t, [[turn + 0.4, 0.7], [turn + 1.0, -0.2]]) : -0.55,
    mouth: mouthOf('lesha', t), handL: [-40, -72], handR: [44, -72],
    eyes: t >= M('s1.g2') && t < l1 ? 1.25 : 1, brow: t >= l2 ? 0.35 : t >= l1 ? 0.1 : 0,
    worried: t >= l1 && t < l2 ? 0.6 : 0, smile: t >= l2 ? 0.55 : 0,
  };
  f.world = office({
    t, mood: 'day', marina, lesha, gena,
    cartAt: [cartX, 790], cartN: placed || slab ? 2 : 3, cartRolling: g.walking,
    laptopMarinaClosed: placed || false,
  }) + (slab ? `<g transform="translate(${slab[0]},${slab[1]})"><rect x="-78" y="-6" width="156" height="12" rx="3" fill="#B8BDC5"/>${penguin(-30, 0, 5)}</g>` : '')
    + (placed ? `<g transform="translate(440,682)"><rect x="-78" y="-6" width="156" height="12" rx="3" fill="#B8BDC5"/>${penguin(-30, 0, 5)}</g>` : '');

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
      lesha: { turn: 0, smile: 0.5, brow: 0.25, handL: [lerp(-40, -14, crack), lerp(-40, -176 - push, crack)], handR: [lerp(44, 14, crack), lerp(-40, -176 - push, crack)] },
      leshaLaptopBack: true, laptopGlow: '#9ad0ff',
    });
    f.view = keys(t, [[sc.start, [1540, 540, 2.0]], [term, [1540, 545, 2.15]]], k => k);
    f.fade = 1 - seg(t, sc.start, sc.start + 0.25);
    return;
  }
  if (t >= lean && t < term2) {
    // откинулся, победная улыбка, глоток кофе
    const grab = lean + 0.3, lift = lean + 0.9, sip = lean + 1.15, down = lean + 1.7;
    const hand = keys(t, [[lean, [-40, -40]], [grab, [-100, -76]], [lift, [-26, -200]], [down + 0.1, [-26, -200]], [down + 0.6, [-66, -178]]]);
    const holding = t >= grab;
    f.world = office({
      t, mood: 'day', marina: { lookDown: 1, handL: [-46, -74], handR: [72, -76] },
      lesha: { turn: -0.1, tilt: -6, smile: t > sip && t < down ? 0 : 0.85, brow: 0.35, eyes: t > sip && t < down ? 0.2 : 1,
               handL: hand, holdL: holding ? 'mug' : null, mugColor: '#2F6F8F', handR: [60, -30], y: 752 },
      leshaLaptopBack: true, leshaMug: holding ? false : undefined,
    });
    f.view = keys(t, [[lean, [1540, 520, 2.25]], [term2, [1540, 520, 2.4]]], k => k);
    return;
  }
  if (t >= freeze && t < term3) {
    f.world = office({
      t: freeze, mood: 'day', marina: { lookDown: 1, handL: [-46, -74], handR: [72, -76] },
      lesha: { turn: -0.1, tilt: -3, eyes: 1.35, o: true, brow: 0.9, handL: [-66, -178], holdL: 'mug', mugColor: '#2F6F8F', handR: [60, -30], y: 752 },
      leshaLaptopBack: true, leshaMug: false,
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
      { x: 430, y: 300, text: 'Wine?', k: seg(t, M('s3.b0'), M('s3.b0') + 0.55) },
      { x: 430, y: 430, text: 'Mono?', k: seg(t, M('s3.b1'), M('s3.b1') + 0.55) },
      { x: 430, y: 560, text: 'Виртуалка с Windows\nна каждом рабочем месте?', k: seg(t, M('s3.b2'), M('s3.b2') + 1.5), size: 92 },
    ];
    let shake = 0;
    for (let i = 0; i < 3; i++) {
      const tb = M(`s3.b${i}`) + (i === 2 ? 1.5 : 0.55) + 0.1;
      if (t >= tb && t < tb + 0.9) shake = Math.sin((t - tb) * 17) * (1 - (t - tb) / 0.9);
    }
    f.world = whiteboardSvg(items) +
      person('lesha', { t, x: 230, y: 880, standing: true, turn: 0.5, handR: [150, -300], handL: [-40, 40], brow: 0.3, smile: 0.2, worried: 0.3 }) +
      `<g transform="translate(${230 + 70 + 70},${880 - 135 - 160})"><rect x="-6" y="-26" width="12" height="40" rx="4" fill="#2C63C9"/></g>` +
      person('gena', { t, x: 1680, y: 880, standing: true, turn: shake * 0.9, tilt: shake * 5, handL: [34, -70], handR: [-34, -66], brow: -0.1 });
    f.view = [960, 540, 1];
  } else if (t < acc) {
    label = 'Депрессия.'; cups = 7;
    const w = M('s3.wipe');
    const wipe = easeIO(seg(t, w, w + 0.8));
    const items = [
      { x: 360, y: 320, text: 'Переписать на Avalonia — 43 формы —\nоценка: 4 месяца', k: seg(t, M('s3.d0'), M('s3.d0') + 1.3), size: 84, color: '#1F4FB5' },
      { x: 360, y: 640, text: 'Переписать на веб — 3 квартала', k: seg(t, M('s3.d1'), M('s3.d1') + 1.0), size: 84, color: '#C0392B' },
    ];
    const lx = lerp(200, 1720, wipe);
    const rubbing = t >= w && t < w + 0.8;
    const handR = rubbing ? [150, -300 - 150 * (0.5 + 0.5 * Math.sin(t * 26))] : [40, 30];
    const handX = lx + (rubbing ? 150 : 0);
    const wipeK = t < w ? 0 : clamp((handX - 128) / 1664);
    f.world = whiteboardSvg(items, wipeK) +
      person('lesha', { t, x: lx, y: 900, standing: true, walking: rubbing, walk: lx / 40, turn: rubbing ? 0.6 : -0.3, worried: 1, brow: -0.3,
        eyes: 0.7, lookDown: rubbing ? 0 : 0.3, handL: [-40, 30], handR });
    f.view = [960, 540, 1];
  } else {
    label = 'Принятие?'; cups = 7;
    f.world = office({
      t, mood: 'night', clock: clock, lesha: { turn: 0.55, lookDown: 0.2, eyes: 0.9, handL: [-40, -72], handR: [44, -72] },
      leshaMonitor: `<rect width="150" height="108" fill="#1b1a24"/>` + [0, 1, 2, 3, 4, 5].map(i => `<rect x="10" y="${10 + i * 15}" width="${40 + (i * 37) % 80}" height="6" fill="#7fb4ff" opacity=".7"/>`).join(''),
      screenGlow: 0.12,
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

  // Гена: входит с кофе, берёт ноутбук, катит к Марине Петровне, потом возвращается к Лёше
  let gx, walking = false, walk = 0;
  const w1 = genaWalk(t, gIn, gIn + 2.4, 820, 1240);
  const w2 = genaWalk(t, roll + 0.8, roll + 3.1, 1240, 780);
  const w3 = genaWalk(t, cof + 0.3, cof + 1.9, 780, 1255);
  if (t < roll + 0.8) ({ x: gx, walking, walk } = w1);
  else if (t < cof + 0.3) ({ x: gx, walking, walk } = w2);
  else ({ x: gx, walking, walk } = w3);
  if (t < gIn) gx = -400;

  const cartX = t < roll + 0.8 ? 1060 : t < cof + 0.3 ? gx - 180 : 600;
  // ноутбук со «Складом»: стол Лёши → тележка → стол Марины Петровны
  let lap = [1310, 580], lapOn = 'lesha';
  const toCart = seg(t, roll, roll + 0.7);
  if (toCart > 0) { lapOn = 'air'; lap = [lerp(1310, cartX - 70, easeIO(toCart)), lerp(580, 668, easeIO(toCart)) - Math.sin(toCart * Math.PI) * 80]; }
  if (toCart >= 1) { lapOn = 'cart'; lap = [cartX - 70, 668]; }
  const toDesk = seg(t, g2 + 0.3, g2 + 0.9);
  if (toDesk > 0) { lapOn = 'air'; lap = [lerp(cartX - 70, 360, easeIO(toDesk)), lerp(668, 584, easeIO(toDesk)) - Math.sin(toDesk * Math.PI) * 70]; }
  if (toDesk >= 1) { lapOn = 'marina'; lap = [360, 584]; }

  const mugPut = seg(t, cof + 1.95, cof + 2.35);
  const holdMug = mugPut < 1;
  const genaHandR = mugPut > 0 ? [lerp(40, 1402 - gx, easeIO(mugPut)), lerp(-40, 684 - 640 - 20, easeIO(mugPut))] : [44, -40];
  let genaHandL = [-40, 60];
  if (lapOn === 'air') genaHandL = [lap[0] + 70 - gx, lap[1] + 40 - 640];
  else if (t >= roll + 0.7 && t < cof + 0.3) genaHandL = [-52, 80];
  const genaTurn = t < look ? 0.2 : t < roll ? 0.6 : t < two ? -0.4 : t < cof ? -0.45 : 0.4;
  const gena = {
    x: gx, y: 640, walking, walk, turn: genaTurn, lookDown: t >= look && t < roll ? 0.5 : t >= cof + 1.9 ? 0.4 : 0,
    handR: genaHandR, holdR: holdMug ? 'mug' : null, steam: true, handL: genaHandL,
    mouth: mouthOf('gena', t), smile: t >= cof + 2 ? 0.35 : 0.1, brow: t >= look && t < roll ? 0.3 : 0,
  };
  const marinaTalk = t >= two && t < cof;
  const marina = {
    lookDown: marinaTalk || (t >= g2 && t < check) ? 0 : 0.8, turn: marinaTalk || (t >= g2 && t < check) ? 0.6 : 0.1,
    mouth: mouthOf('marina', t), handL: [-46, -74], handR: [72, -76], brow: t >= M('s5.m2') && t < cof ? 0.1 : 0,
    smile: t >= M('s5.m2') && t < cof ? 0.25 : 0,
  };
  const sss = 's'.repeat(40 + Math.floor((t - sc.start) * 9));
  const termLines = [{ t: '$ dotnet run', c: '#7ee2a8' }];
  for (let i = 0; i < 6; i++) termLines.push({ t: sss.slice(i * 20, i * 20 + 20) });
  let w = office({
    t, mood: 'morning', clock: 9 * 3600 + (t - sc.start) * 60,
    marina, lesha: { hidden: true, sleepHead: true }, gena,
    leshaMonitor: miniTerminal(150, 108, termLines.filter(l => l.t)),
    leshaLaptopFront: lapOn === 'lesha' ? lap : null, leshaMug: false,
    laptopMarina: lapOn === 'marina' ? lap : null, laptopSel: 0,
    cartAt: [cartX, 790], cartN: 0, cartRolling: walking && t > roll,
    genaMugOnDesk: mugPut >= 1 ? [1402, 684] : null,
  });
  if (lapOn === 'air' || lapOn === 'cart') w += laptopFront(lap[0], lap[1], 140, 88, miniSklad(140, 88, { wall: '#3A6EA5' }));
  f.world = w;

  if (t < gIn + 1.2) f.view = [1470, 540, 1.95];
  else if (t < roll + 0.8) f.view = keys(t, [[gIn + 1.2, [1470, 540, 1.95]], [gIn + 2.6, [1330, 540, 1.6]]]);
  else if (t < check) f.view = keys(t, [[roll + 0.8, [1330, 540, 1.6]], [roll + 3.1, [640, 540, 1.6]]]);
  else if (t < cof + 0.3) f.view = keys(t, [[two, [600, 530, 1.7]], [cof, [610, 530, 1.75]]], k => k);
  else f.view = keys(t, [[cof + 0.3, [610, 530, 1.75]], [cof + 2.0, [1420, 540, 1.8]]]);
  f.fade = Math.max(1 - seg(t, sc.start, sc.start + 0.6), seg(t, sc.end - 0.7, sc.end));
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
