// «Злой csproj»: общие помощники, персонажи и офис. Всё рисуется детерминированно от времени t
// (секунды ролика), без CSS-анимаций: render.mjs снимает кадр за кадром.

/* ─── время и интерполяция ─────────────────────────────────────────────────────────────── */
const clamp = (x, a = 0, b = 1) => Math.min(b, Math.max(a, x));
const lerp = (a, b, k) => a + (b - a) * k;
const seg = (t, a, b) => (b <= a ? (t >= a ? 1 : 0) : clamp((t - a) / (b - a)));
const easeIO = k => (k < 0.5 ? 4 * k * k * k : 1 - Math.pow(-2 * k + 2, 3) / 2);
const easeO = k => 1 - Math.pow(1 - k, 3);
const easeI = k => k * k * k;
const back = k => { const c = 1.9; return 1 + (c + 1) * Math.pow(k - 1, 3) + c * Math.pow(k - 1, 2); };
const M = name => {
  const v = TL.marks[name];
  if (v === undefined) throw new Error('нет метки ' + name);
  return v;
};
const esc = s => String(s).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
// детерминированный шум
const hash = n => { const x = Math.sin(n * 127.1 + 311.7) * 43758.5453; return x - Math.floor(x); };
const noise1 = x => { const i = Math.floor(x), f = x - i, u = f * f * (3 - 2 * f); return lerp(hash(i), hash(i + 1), u) * 2 - 1; };

/** Ключевые кадры: [[t, value], ...] → значение с плавностью ease. value — число или массив. */
function keys(t, kf, ease = easeIO) {
  if (t <= kf[0][0]) return kf[0][1];
  for (let i = 1; i < kf.length; i++) {
    if (t <= kf[i][0]) {
      const [t0, v0] = kf[i - 1], [t1, v1] = kf[i];
      const k = ease(seg(t, t0, t1));
      return Array.isArray(v0) ? v0.map((v, j) => lerp(v, v1[j], k)) : lerp(v0, v1, k);
    }
  }
  return kf[kf.length - 1][1];
}

/** Громкость реплики персонажа в момент t (0..1) — для губ. */
function mouthOf(who, t) {
  for (const l of TL.lines) {
    if (l.who !== who || t < l.t0 || t > l.t1) continue;
    const f = (t - l.t0) * TL.fps;
    const i = Math.floor(f), k = f - i;
    return lerp(l.env[i] || 0, l.env[i + 1] || 0, k);
  }
  return 0;
}
const speaking = (who, t) => TL.lines.some(l => l.who === who && t >= l.t0 - 0.05 && t <= l.t1 + 0.05);

/** Сколько символов набрано к моменту t. */
function typed(id, t) {
  const ty = TL.typing[id];
  if (!ty) return 0;
  let n = 0;
  for (const x of ty.times) if (t >= x) n++;
  return n;
}
const typedText = (id, t) => TL.typing[id].text.slice(0, typed(id, t));

/** Моргание: сдвиг по фазе для каждого персонажа. */
function blink(t, phase) {
  const period = 3.7 + phase * 0.6;
  const x = (t + phase * 1.3) % period;
  return x < 0.12 ? Math.abs(x - 0.06) / 0.06 : 1;
}

/* ─── персонажи ─────────────────────────────────────────────────────────────────────────── */
const CAST = {
  lesha: { skin: '#F1C6A4', skinD: '#DDA887', hair: '#3A2A20', top: '#2F6F8F', topD: '#245872', pants: '#3B4252',
           style: 'hoodie', hairStyle: 'messy', phase: 0.1 },
  marina: { skin: '#EFC5A6', skinD: '#D9A98A', hair: '#9C4128', top: '#8B2F4B', topD: '#6F2239', pants: '#3E3A4A',
            style: 'cardigan', hairStyle: 'bun', glasses: true, phase: 0.55 },
  gena: { skin: '#E2AE8A', skinD: '#C99373', hair: '#6B5A4B', top: '#557A5B', topD: '#40604A', pants: '#4A5263',
          style: 'flannel', hairStyle: 'short', beard: true, phase: 0.9 },
};

/** Двухзвенная рука: плечо (sx,sy) → кисть (hx,hy), bend = ±1 — куда сгибается локоть. */
function limb(sx, sy, hx, hy, L1, L2, bend) {
  let dx = hx - sx, dy = hy - sy, d = Math.hypot(dx, dy);
  const max = L1 + L2 - 0.5;
  if (d > max) { hx = sx + dx / d * max; hy = sy + dy / d * max; dx = hx - sx; dy = hy - sy; d = max; }
  const a = Math.atan2(dy, dx);
  const A = Math.acos(clamp((L1 * L1 + d * d - L2 * L2) / (2 * L1 * d), -1, 1));
  const ea = a + bend * A;
  return { ex: sx + L1 * Math.cos(ea), ey: sy + L1 * Math.sin(ea), hx, hy };
}

/**
 * Персонаж анфас. Начало координат — середина таза (сидит на стуле или стоит).
 * s: { x, y, scale, turn(-1..1), tilt(°), lean(°), look:[x,y], eyes(0..1.4), brow(-1..1 — вверх/вниз),
 *      angry(0..1), smile(-1..1), mouth(0..1), teeth, puff, red, lookDown(0..1), handL:[x,y]|null, handR,
 *      standing, walk(фаза), holdL/holdR ('mug'), sleep }
 */
function person(who, s) {
  const c = CAST[who];
  const sc = s.scale || 1;
  const turn = s.turn || 0;
  const fx = turn * 17;
  const out = [];
  out.push(`<g transform="translate(${s.x},${s.y}) scale(${sc * (s.flip ? -1 : 1)},${sc}) rotate(${s.lean || 0})">`);

  // ноги (если стоит)
  if (s.standing) {
    const ph = s.walk || 0;
    const sw = s.walking ? Math.sin(ph) * 22 : 0;
    for (const [side, ang] of [[-1, sw], [1, -sw]]) {
      const hx = side * 28, rad = ang * Math.PI / 180;
      const kx = hx + Math.sin(rad) * 125, ky = Math.cos(rad) * 125;
      const fxx = kx + Math.sin(rad * 0.6) * 125, fyy = ky + 125;
      out.push(`<path d="M${hx},4 L${kx},${ky} L${fxx},${fyy}" stroke="${c.pants}" stroke-width="44" fill="none" stroke-linecap="round" stroke-linejoin="round"/>`);
      out.push(`<ellipse cx="${fxx + side * 8}" cy="${fyy + 18}" rx="32" ry="15" fill="#2B2B30"/>`);
    }
  }

  const bob = s.walking ? Math.abs(Math.sin(s.walk || 0)) * -6 : Math.sin((s.t || 0) * 1.7 + c.phase * 5) * 1.5;
  out.push(`<g transform="translate(0,${bob})">`);

  // руки сзади тела не рисуем; сначала капюшон
  if (c.style === 'hoodie') {
    out.push(`<path d="M-58,-150 Q0,-120 58,-150 Q70,-185 0,-190 Q-70,-185 -58,-150Z" fill="${c.topD}"/>`);
  }
  // туловище
  out.push(`<path d="M-66,6 L-78,-128 Q-78,-156 -46,-160 L46,-160 Q78,-156 78,-128 L66,6 Z" fill="${c.top}"/>`);
  if (c.style === 'cardigan') {
    out.push(`<path d="M-24,-160 L0,-96 L24,-160 Z" fill="#F6EFE6"/>`);
    out.push(`<path d="M-4,-96 L-4,6 M4,-96 L4,6" stroke="${c.topD}" stroke-width="3"/>`);
    out.push(`<circle cx="0" cy="-70" r="4" fill="#E8D8B0"/><circle cx="0" cy="-40" r="4" fill="#E8D8B0"/><circle cx="0" cy="-10" r="4" fill="#E8D8B0"/>`);
    out.push(`<circle cx="-40" cy="-120" r="6" fill="#D8B45A"/>`);
  } else if (c.style === 'flannel') {
    out.push(`<path d="M-30,-160 L-30,6 L30,6 L30,-160 Z" fill="#2E3238"/>`);
    // пингвин на футболке
    out.push(`<g transform="translate(0,-98) scale(0.9)"><ellipse cx="0" cy="0" rx="13" ry="17" fill="#15161A"/><ellipse cx="0" cy="4" rx="8" ry="11" fill="#F4F4F4"/><circle cx="-4" cy="-7" r="2" fill="#F4F4F4"/><circle cx="4" cy="-7" r="2" fill="#F4F4F4"/><path d="M-4,-3 L4,-3 L0,1Z" fill="#F2B233"/><ellipse cx="-6" cy="17" rx="5" ry="2.5" fill="#F2B233"/><ellipse cx="6" cy="17" rx="5" ry="2.5" fill="#F2B233"/></g>`);
    for (const x of [-60, -45, 45, 60]) out.push(`<path d="M${x},-150 L${x * 0.9},6" stroke="${c.topD}" stroke-width="4" opacity=".6"/>`);
    for (const y of [-130, -95, -60, -25]) out.push(`<path d="M-76,${y} L-30,${y} M30,${y} L76,${y}" stroke="${c.topD}" stroke-width="4" opacity=".6"/>`);
  } else if (c.style === 'hoodie') {
    out.push(`<path d="M-14,-150 L-18,-100 M14,-150 L18,-100" stroke="#E9E6DF" stroke-width="4" stroke-linecap="round"/>`);
    out.push(`<path d="M-42,-40 Q0,-30 42,-40 L38,0 L-38,0 Z" fill="${c.topD}" opacity=".55"/>`);
  }
  // шея
  out.push(`<rect x="-15" y="-182" width="30" height="30" rx="8" fill="${c.skinD}"/>`);

  // руки: опущенные — за головой, поднятые к лицу (кружка, хруст пальцами) — перед ней
  const shoulder = { L: [-70, -135], R: [70, -135] };
  const arms = { low: [], high: [] };
  for (const side of ['L', 'R']) {
    const h = s['hand' + side];
    const def = side === 'L' ? [-50, 30] : [50, 30];
    const [hx, hy] = h || def;
    const [sx, sy] = shoulder[side];
    const bend = side === 'L' ? 1 : -1;
    const a = limb(sx, sy, hx, hy, 88, 82, bend);
    const dst = a.hy < -160 ? arms.high : arms.low;
    dst.push(`<path d="M${sx},${sy} L${a.ex},${a.ey} L${a.hx},${a.hy}" stroke="${c.top}" stroke-width="30" fill="none" stroke-linecap="round" stroke-linejoin="round"/>`);
    dst.push(`<circle cx="${a.hx}" cy="${a.hy}" r="14" fill="${c.skin}"/>`);
    if (s['hold' + side] === 'mug') dst.push(mug(a.hx + (side === 'L' ? 6 : -6), a.hy - 6, s.mugColor || '#F4F1EA', 1, s.t, s.steam));
  }
  out.push(...arms.low);

  // голова
  const tilt = s.tilt || 0;
  out.push(`<g transform="rotate(${tilt} 0 -180)">`);
  out.push(head(c, s, fx));
  out.push('</g>');
  out.push(...arms.high);
  out.push('</g></g>');
  return out.join('');
}

function head(c, s, fx) {
  const o = [];
  const ld = s.lookDown || 0;
  const hy = -236;
  // волосы сзади
  if (c.hairStyle === 'bun') {
    o.push(`<circle cx="${fx * 0.3}" cy="${hy - 70}" r="30" fill="${c.hair}"/>`);
    o.push(`<path d="M-60,${hy + 10} Q-66,${hy - 60} 0,${hy - 66} Q66,${hy - 60} 60,${hy + 10} L56,${hy + 34} L-56,${hy + 34}Z" fill="${c.hair}"/>`);
  } else if (c.hairStyle === 'short') {
    o.push(`<path d="M-56,${hy} Q-58,${hy - 58} 0,${hy - 64} Q58,${hy - 58} 56,${hy}Z" fill="${c.hair}"/>`);
  } else {
    o.push(`<path d="M-58,${hy + 6} Q-64,${hy - 62} 0,${hy - 68} Q64,${hy - 62} 58,${hy + 6}Z" fill="${c.hair}"/>`);
  }
  // уши и лицо
  o.push(`<ellipse cx="${-52 + fx * 0.3}" cy="${hy + 4}" rx="10" ry="15" fill="${c.skinD}"/>`);
  o.push(`<ellipse cx="${52 + fx * 0.3}" cy="${hy + 4}" rx="10" ry="15" fill="${c.skinD}"/>`);
  const puff = s.puff || 0;
  o.push(`<ellipse cx="${fx * 0.25}" cy="${hy}" rx="${52 + puff * 6}" ry="60" fill="${c.skin}"/>`);
  if (s.red) o.push(`<ellipse cx="${fx * 0.25}" cy="${hy}" rx="52" ry="60" fill="#E0463A" opacity="${0.45 * s.red}"/>`);
  if (c.style === 'hoodie') o.push(`<path d="M${fx - 26},${hy + 40} Q${fx},${hy + 58} ${fx + 26},${hy + 40} Q${fx},${hy + 50} ${fx - 26},${hy + 40}Z" fill="${c.skinD}" opacity=".5"/>`);
  if (puff) {
    o.push(`<circle cx="${fx - 30}" cy="${hy + 26}" r="${12 * puff}" fill="#F0A58E" opacity=".7"/>`);
    o.push(`<circle cx="${fx + 30}" cy="${hy + 26}" r="${12 * puff}" fill="#F0A58E" opacity=".7"/>`);
  }
  // борода
  if (c.beard) {
    o.push(`<path d="M${fx - 46},${hy + 4} Q${fx - 44},${hy + 70} ${fx},${hy + 72} Q${fx + 44},${hy + 70} ${fx + 46},${hy + 4} Q${fx + 30},${hy + 30} ${fx},${hy + 28} Q${fx - 30},${hy + 30} ${fx - 46},${hy + 4}Z" fill="${c.hair}"/>`);
  }

  // глаза
  const lx = (s.look ? s.look[0] : turn0(s)) * 4, ly = (s.look ? s.look[1] : 0) * 3 + ld * 4;
  const open = (s.eyes === undefined ? 1 : s.eyes) * blink(s.t || 0, c.phase) * (1 - ld * 0.55);
  const ey = hy - 4 + ld * 5;
  for (const side of [-1, 1]) {
    const ex = fx + side * 20;
    if (s.sleep) {
      o.push(`<path d="M${ex - 8},${ey} Q${ex},${ey + 6} ${ex + 8},${ey}" stroke="#2A2220" stroke-width="3.5" fill="none" stroke-linecap="round"/>`);
      continue;
    }
    if ((s.eyes || 1) > 1.15) o.push(`<ellipse cx="${ex}" cy="${ey}" rx="10" ry="${11 * open}" fill="#FFFFFF"/>`);
    o.push(`<ellipse cx="${ex + lx}" cy="${ey + ly}" rx="5.5" ry="${Math.max(0.8, 7.5 * Math.min(open, 1.1))}" fill="#2A2220"/>`);
    // брови
    const b = s.brow || 0, ang = s.angry || 0;
    const by = ey - 17 - b * 9 + ld * 3;
    const inner = by + ang * 7 - (s.worried || 0) * 6, outer = by - ang * 3 + (s.worried || 0) * 3;
    const xi = ex - side * 4, xo = ex + side * 13;
    o.push(`<path d="M${xi - side * 6},${inner} L${xo},${outer}" stroke="${c.hair === '#9C4128' ? '#7A3322' : c.hair}" stroke-width="5" stroke-linecap="round"/>`);
  }
  // очки
  if (c.glasses) {
    const gy = ey + 1;
    o.push(`<g fill="none" stroke="#6B4E2E" stroke-width="3"><rect x="${fx - 34}" y="${gy - 10}" width="27" height="20" rx="7"/><rect x="${fx + 7}" y="${gy - 10}" width="27" height="20" rx="7"/><path d="M${fx - 7},${gy - 2} L${fx + 7},${gy - 2}"/></g>`);
    o.push(`<path d="M${fx - 34},${gy} Q-62,${hy + 40} -52,${hy + 96} M${fx + 34},${gy} Q62,${hy + 40} 52,${hy + 96}" stroke="#C9A45A" stroke-width="1.4" fill="none" opacity=".45"/>`);
  }
  // нос
  o.push(`<path d="M${fx * 1.25 + 1},${hy + 4 + ld * 4} Q${fx * 1.25 + 7},${hy + 18 + ld * 4} ${fx * 1.25},${hy + 20 + ld * 4}" stroke="${c.skinD}" stroke-width="3.5" fill="none" stroke-linecap="round"/>`);
  // рот
  const my = hy + 36 + ld * 6;
  const m = clamp(s.mouth || 0);
  const smile = s.smile || 0;
  const mx = fx * 1.1;
  if (s.sleep) {
    o.push(`<ellipse cx="${mx}" cy="${my}" rx="6" ry="4" fill="#7A2E2E"/>`);
  } else if (m > 0.06 || s.o) {
    const rx = s.o ? 7 : 13 - m * 2, ry = s.o ? 9 : 2 + m * 10;
    o.push(`<path d="M${mx - rx},${my - smile * 3} Q${mx},${my + ry * 2 + smile * 4} ${mx + rx},${my - smile * 3} Q${mx},${my - ry * 0.4} ${mx - rx},${my - smile * 3}Z" fill="#7A2E2E"/>`);
    if (s.teeth) o.push(`<rect x="${mx - rx + 3}" y="${my - 2}" width="${2 * rx - 6}" height="5" rx="2" fill="#FFFFFF"/>`);
  } else if (s.teeth) {
    o.push(`<rect x="${mx - 14}" y="${my - 5}" width="28" height="10" rx="4" fill="#FFFFFF" stroke="#7A2E2E" stroke-width="2.5"/><path d="M${mx - 14},${my} L${mx + 14},${my}" stroke="#C9C1B8" stroke-width="1.5"/>`);
  } else {
    o.push(`<path d="M${mx - 13},${my} Q${mx},${my + smile * 12} ${mx + 13},${my - (s.smirk || 0) * 6}" stroke="#7A2E2E" stroke-width="4" fill="none" stroke-linecap="round"/>`);
  }
  // волосы спереди
  if (c.hairStyle === 'messy') {
    o.push(`<path d="M-56,${hy - 12} Q-60,${hy - 66} -6,${hy - 70} Q40,${hy - 76} 58,${hy - 30} L54,${hy - 14} Q40,${hy - 40} 20,${hy - 36} L24,${hy - 26} Q0,${hy - 44} -20,${hy - 34} L-18,${hy - 22} Q-40,${hy - 34} -56,${hy - 12}Z" fill="${c.hair}"/>`);
    o.push(`<path d="M-10,${hy - 70} L-2,${hy - 86} L8,${hy - 70} M14,${hy - 70} L30,${hy - 80} L30,${hy - 64}" fill="${c.hair}" stroke="${c.hair}" stroke-width="6" stroke-linejoin="round"/>`);
  } else if (c.hairStyle === 'bun') {
    o.push(`<path d="M-56,${hy - 4} Q-54,${hy - 62} 4,${hy - 64} Q58,${hy - 60} 56,${hy - 4} Q46,${hy - 46} 10,${hy - 40} Q-30,${hy - 50} -56,${hy - 4}Z" fill="${c.hair}"/>`);
    o.push(`<path d="M-20,${hy - 62} Q0,${hy - 40} 26,${hy - 58}" stroke="#B9B2AA" stroke-width="3" fill="none" opacity=".7"/>`);
  } else if (c.hairStyle === 'short') {
    o.push(`<path d="M-54,${hy - 6} Q-54,${hy - 56} 0,${hy - 62} Q54,${hy - 56} 54,${hy - 6} Q44,${hy - 34} 26,${hy - 42} Q0,${hy - 50} -26,${hy - 42} Q-44,${hy - 34} -54,${hy - 6}Z" fill="${c.hair}"/>`);
  }
  return o.join('');
}
const turn0 = s => (s.turn || 0) * 1.2;

/** Кружка с паром. */
function mug(x, y, color = '#F4F1EA', sc = 1, t = 0, steam = false, label = '') {
  const o = [`<g transform="translate(${x},${y}) scale(${sc})">`];
  if (steam) {
    for (let i = 0; i < 3; i++) {
      const ph = t * 1.4 + i * 1.7;
      const k = (ph % 2) / 2;
      o.push(`<path d="M${-8 + i * 8},${-26 - k * 30} q${6 * Math.sin(ph * 3)},-8 0,-16" stroke="#FFFFFF" stroke-width="3" fill="none" stroke-linecap="round" opacity="${0.55 * (1 - k)}"/>`);
    }
  }
  o.push(`<path d="M12,-12 q14,0 14,10 q0,10 -14,10" stroke="${color}" stroke-width="5" fill="none"/>`);
  o.push(`<rect x="-16" y="-24" width="30" height="34" rx="5" fill="${color}"/>`);
  o.push(`<rect x="-16" y="-24" width="30" height="6" rx="3" fill="#000" opacity=".08"/>`);
  if (label) o.push(`<text x="-1" y="-2" font-size="7" text-anchor="middle" fill="#8B2F4B" font-family="Onest" font-weight="700">${label}</text>`);
  o.push('</g>');
  return o.join('');
}

/* ─── предметы ─────────────────────────────────────────────────────────────────────────── */
function monitor(x, y, w, h, screenSvg, opts = {}) {
  const o = [];
  o.push(`<rect x="${x + w / 2 - 12}" y="${y + h}" width="24" height="${opts.stand || 40}" fill="#5B606B"/>`);
  o.push(`<rect x="${x + w / 2 - 45}" y="${y + h + (opts.stand || 40) - 8}" width="90" height="10" rx="4" fill="#4A4F58"/>`);
  o.push(`<rect x="${x - 8}" y="${y - 8}" width="${w + 16}" height="${h + 16}" rx="8" fill="#23262C"/>`);
  o.push(`<svg x="${x}" y="${y}" width="${w}" height="${h}" viewBox="0 0 ${opts.vw || w} ${opts.vh || h}" preserveAspectRatio="none">${screenSvg}</svg>`);
  if (opts.glare !== false) o.push(`<path d="M${x},${y} L${x + w * 0.35},${y} L${x + w * 0.1},${y + h} L${x},${y + h}Z" fill="#FFFFFF" opacity=".05"/>`);
  return o.join('');
}

/** Ноутбук экраном к камере. */
function laptopFront(x, y, w, h, screenSvg, opts = {}) {
  const o = [];
  o.push(`<rect x="${x - 10}" y="${y - 10}" width="${w + 20}" height="${h + 20}" rx="10" fill="#2A2D33"/>`);
  o.push(`<svg x="${x}" y="${y}" width="${w}" height="${h}" viewBox="0 0 ${opts.vw || w} ${opts.vh || h}" preserveAspectRatio="none">${screenSvg}</svg>`);
  o.push(`<path d="M${x - 30},${y + h + 10} L${x + w + 30},${y + h + 10} L${x + w + 12},${y + h + 24} L${x - 12},${y + h + 24}Z" fill="#B7BBC3"/>`);
  return o.join('');
}

/** Ноутбук крышкой к камере — с наклейкой-пингвином. */
function laptopBack(x, y, w, h, opts = {}) {
  const o = [];
  o.push(`<path d="M${x},${y + h} L${x + 10},${y} L${x + w - 10},${y} L${x + w},${y + h}Z" fill="#AEB3BC"/>`);
  o.push(`<path d="M${x + 10},${y} L${x + w - 10},${y} L${x + w - 8},${y + 8} L${x + 8},${y + 8}Z" fill="#C5C9D0"/>`);
  if (opts.glow) o.push(`<path d="M${x + 14},${y - 2} L${x + w - 14},${y - 2}" stroke="${opts.glow}" stroke-width="3" opacity=".9"/>`);
  o.push(penguin(x + w / 2, y + h / 2 + 4, h * 0.28));
  o.push(`<rect x="${x - 20}" y="${y + h}" width="${w + 40}" height="12" rx="4" fill="#9BA0A9"/>`);
  return o.join('');
}

function penguin(x, y, r) {
  return `<g transform="translate(${x},${y}) scale(${r / 20})"><circle r="22" fill="#FFFFFF"/><ellipse cx="0" cy="1" rx="12" ry="16" fill="#17181C"/><ellipse cx="0" cy="5" rx="7.5" ry="10" fill="#F6F6F6"/><circle cx="-3.5" cy="-7" r="1.8" fill="#FFF"/><circle cx="3.5" cy="-7" r="1.8" fill="#FFF"/><path d="M-3.5,-3 L3.5,-3 L0,1Z" fill="#F2B233"/><ellipse cx="-5" cy="16" rx="4.5" ry="2" fill="#F2B233"/><ellipse cx="5" cy="16" rx="4.5" ry="2" fill="#F2B233"/></g>`;
}

/** Тележка сисадмина. withLaptops — сколько ноутбуков сверху. */
function cart(x, y, n = 3, t = 0, rolling = false) {
  const o = [`<g transform="translate(${x},${y})">`];
  o.push(`<ellipse cx="0" cy="118" rx="110" ry="12" fill="#000" opacity=".12"/>`);
  o.push(`<rect x="-100" y="-10" width="200" height="12" rx="4" fill="#8E96A3"/>`);
  o.push(`<rect x="-100" y="70" width="200" height="12" rx="4" fill="#8E96A3"/>`);
  o.push(`<rect x="-96" y="-10" width="8" height="112" fill="#6E7682"/><rect x="88" y="-10" width="8" height="112" fill="#6E7682"/>`);
  o.push(`<path d="M92,-10 L110,-70 L128,-70" stroke="#6E7682" stroke-width="8" fill="none" stroke-linecap="round"/>`);
  for (const wx of [-80, 80]) {
    const a = rolling ? t * 600 : 0;
    o.push(`<g transform="translate(${wx},110) rotate(${a})"><circle r="11" fill="#2E3238"/><path d="M-6,0 L6,0" stroke="#8E96A3" stroke-width="3"/></g>`);
  }
  for (let i = 0; i < n; i++) {
    o.push(`<rect x="-78" y="${-24 - i * 13}" width="156" height="12" rx="3" fill="${i % 2 ? '#A9AEB7' : '#B8BDC5'}"/>`);
    o.push(penguin(-40 + (i % 2) * 30, -18 - i * 13, 5));
  }
  for (let i = 0; i < 2; i++) o.push(`<rect x="-70" y="${56 - i * 13}" width="140" height="12" rx="3" fill="#B0B5BE"/>`);
  o.push('</g>');
  return o.join('');
}

/** Стол анфас; сверху кладутся предметы, человек рисуется до стола. */
function desk(x, y, w) {
  return `<g><rect x="${x}" y="${y}" width="${w}" height="18" rx="4" fill="#CFAE82"/>
    <rect x="${x}" y="${y + 14}" width="${w}" height="6" fill="#B5936A"/>
    <rect x="${x + 16}" y="${y + 20}" width="${w - 32}" height="${150}" fill="#DDD5C6"/>
    <rect x="${x + 16}" y="${y + 20}" width="${w - 32}" height="14" fill="#000" opacity=".06"/>
    <rect x="${x + 22}" y="${y + 20}" width="10" height="170" fill="#8D919A"/><rect x="${x + w - 32}" y="${y + 20}" width="10" height="170" fill="#8D919A"/></g>`;
}

function chair(x, y) {
  return `<g><rect x="${x - 70}" y="${y - 200}" width="140" height="190" rx="28" fill="#3A3F4B"/><rect x="${x - 60}" y="${y - 190}" width="120" height="16" rx="8" fill="#FFFFFF" opacity=".06"/></g>`;
}

/* ─── мини-экраны (векторные подобия, для общих планов) ───────────────────────────────── */
function miniSklad(w, h, opts = {}) {
  const o = [`<rect width="${w}" height="${h}" fill="${opts.wall || '#DDE4EA'}"/>`];
  const x = w * 0.06, y = h * 0.08, ww = w * 0.88, hh = h * 0.84;
  o.push(`<rect x="${x}" y="${y}" width="${ww}" height="${hh}" fill="#ECE9D8" stroke="#9AA3AE"/>`);
  o.push(`<rect x="${x}" y="${y}" width="${ww}" height="${hh * 0.09}" fill="${opts.title || '#F4F4F5'}"/>`);
  o.push(`<rect x="${x + 6}" y="${y + hh * 0.13}" width="${ww * 0.15}" height="${hh * 0.07}" fill="#DADADA" stroke="#9A9A9A" stroke-width=".6"/>`);
  o.push(`<rect x="${x + 10 + ww * 0.15}" y="${y + hh * 0.13}" width="${ww * 0.15}" height="${hh * 0.07}" fill="#DADADA" stroke="#9A9A9A" stroke-width=".6"/>`);
  o.push(`<rect x="${x + 14 + ww * 0.3}" y="${y + hh * 0.13}" width="${ww * 0.15}" height="${hh * 0.07}" fill="#DADADA" stroke="#9A9A9A" stroke-width=".6"/>`);
  const gy = y + hh * 0.28;
  o.push(`<rect x="${x + 4}" y="${gy}" width="${ww - 8}" height="${hh * 0.66}" fill="#FFFFFF" stroke="#B0B0B0" stroke-width=".6"/>`);
  const rows = 9;
  for (let i = 0; i <= rows; i++) {
    const ry = gy + (hh * 0.66) * i / rows;
    if (i === (opts.sel ?? -1)) o.push(`<rect x="${x + 4}" y="${ry}" width="${ww - 8}" height="${hh * 0.66 / rows}" fill="#0A6CD6"/>`);
    o.push(`<path d="M${x + 4},${ry} H${x + ww - 4}" stroke="#D5D5D5" stroke-width=".6"/>`);
  }
  for (const k of [0.12, 0.55, 0.65, 0.8]) o.push(`<path d="M${x + 4 + (ww - 8) * k},${gy} V${gy + hh * 0.66}" stroke="#D5D5D5" stroke-width=".6"/>`);
  o.push(`<rect x="${x + 4}" y="${gy}" width="${ww - 8}" height="${hh * 0.66 / rows}" fill="#EFEFEF"/>`);
  return o.join('');
}

function miniDesktop(w, h) {
  const o = [`<defs><linearGradient id="wpD" x1="0" y1="0" x2="1" y2="1"><stop offset="0" stop-color="#2D7FA8"/><stop offset="1" stop-color="#1B4E7A"/></linearGradient></defs>`];
  o.push(`<rect width="${w}" height="${h}" fill="url(#wpD)"/>`);
  o.push(`<path d="M0,${h * 0.7} Q${w * 0.4},${h * 0.5} ${w},${h * 0.72} L${w},${h} L0,${h}Z" fill="#2E9A6B" opacity=".55"/>`);
  o.push(`<rect y="${h * 0.9}" width="${w}" height="${h * 0.1}" fill="#15202C"/>`);
  for (let i = 0; i < 4; i++) {
    o.push(`<rect x="${w * 0.05}" y="${h * (0.06 + i * 0.2)}" width="${w * 0.09}" height="${w * 0.09}" rx="2" fill="${i === 0 ? '#C68A45' : '#E8EEF4'}" opacity="${i === 0 ? 1 : 0.8}"/>`);
    o.push(`<rect x="${w * 0.03}" y="${h * (0.06 + i * 0.2) + w * 0.1}" width="${w * 0.13}" height="${h * 0.025}" fill="#FFFFFF" opacity=".75"/>`);
  }
  return o.join('');
}

function miniTerminal(w, h, lines) {
  const o = [`<rect width="${w}" height="${h}" fill="#15131E"/>`];
  lines.forEach((ln, i) => o.push(`<text x="6" y="${14 + i * 13}" font-family="JetBrains Mono" font-size="11" fill="${ln.c || '#D9D5EA'}">${esc(ln.t)}</text>`));
  return o.join('');
}

/* ─── офис ─────────────────────────────────────────────────────────────────────────────── */
/**
 * Офис целиком. st: { t, mood:'day'|'night'|'morning', gena:{...}|null, marina:{...}, lesha:{...},
 *   cartAt:[x,y]|null, cartN, laptopMarina, leshaLaptopBack, leshaMonitor(svg), marinaMonitor(svg), lamp, ... }
 */
function office(st) {
  const t = st.t;
  const o = [];
  const mood = st.mood || 'day';
  o.push(`<defs>
    <linearGradient id="sky" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="${mood === 'night' ? '#0E1733' : mood === 'morning' ? '#F7C99A' : '#A9D8F0'}"/><stop offset="1" stop-color="${mood === 'night' ? '#1E2B52' : mood === 'morning' ? '#FBE6C4' : '#E6F4FB'}"/></linearGradient>
    <pattern id="plank" width="240" height="46" patternUnits="userSpaceOnUse"><rect width="240" height="46" fill="#C8AD89"/><path d="M0,45.5 H240 M120,0 V46" stroke="#B69A76" stroke-width="2"/></pattern>
    <radialGradient id="lampG"><stop offset="0" stop-color="#000" stop-opacity="1"/><stop offset=".55" stop-color="#000" stop-opacity=".7"/><stop offset="1" stop-color="#000" stop-opacity="0"/></radialGradient>
    <radialGradient id="scrG"><stop offset="0" stop-color="#000" stop-opacity=".95"/><stop offset="1" stop-color="#000" stop-opacity="0"/></radialGradient>
  </defs>`);
  // стена, окно, пол
  o.push(`<rect x="-800" y="-400" width="3600" height="1210" fill="#ECE4D6"/>`);
  o.push(`<rect x="-800" y="560" width="3600" height="240" fill="#E3D8C5"/>`);
  o.push(`<rect x="-800" y="556" width="3600" height="6" fill="#D2C4AC"/>`);
  o.push(`<rect x="-800" y="800" width="3600" height="600" fill="url(#plank)"/>`);
  o.push(`<rect x="-800" y="796" width="3600" height="14" fill="#BCAB91"/>`);
  // окно
  o.push(`<rect x="690" y="110" width="440" height="360" rx="6" fill="#FFFFFF"/>`);
  o.push(`<rect x="704" y="124" width="412" height="332" fill="url(#sky)"/>`);
  const bcol = mood === 'night' ? '#101938' : mood === 'morning' ? '#E2B289' : '#9DBFD6';
  o.push(`<path d="M704,456 V330 H760 V280 H820 V350 H880 V250 H950 V320 H1010 V290 H1060 V360 H1116 V456Z" fill="${bcol}"/>`);
  if (mood === 'night') {
    for (let i = 0; i < 14; i++) o.push(`<rect x="${770 + (i * 37) % 300}" y="${300 + (i * 53) % 130}" width="8" height="10" fill="#F2D27A" opacity="${0.5 + 0.5 * hash(i)}"/>`);
    o.push(`<circle cx="1040" cy="190" r="26" fill="#F4F0DC"/><circle cx="1052" cy="182" r="24" fill="#16224A"/>`);
  }
  if (mood === 'morning') o.push(`<circle cx="780" cy="330" r="46" fill="#FFE2A8" opacity=".9"/>`);
  o.push(`<rect x="904" y="124" width="12" height="332" fill="#FFFFFF"/><rect x="704" y="284" width="412" height="10" fill="#FFFFFF"/>`);
  o.push(`<rect x="680" y="466" width="460" height="16" rx="4" fill="#F4F1EA"/>`);
  // часы
  const clockT = st.clock ?? (9 * 3600 + 5 * 60 + t * 60);
  o.push(wallClock(560, 200, 44, clockT));
  // приказ
  o.push(order(1190, 170));
  // растение
  o.push(`<g transform="translate(1880,800)"><rect x="-38" y="-70" width="76" height="70" rx="8" fill="#D46A43"/><path d="M0,-70 Q-60,-160 -90,-230 M0,-70 Q-10,-200 10,-280 M0,-70 Q50,-170 90,-220 M0,-70 Q30,-130 60,-150" stroke="#4E8A55" stroke-width="16" fill="none" stroke-linecap="round"/></g>`);
  // доска с объявлениями слева
  o.push(`<rect x="60" y="170" width="220" height="150" rx="6" fill="#C9A67A"/><rect x="72" y="182" width="196" height="126" fill="#D8BC93"/>`);
  o.push(`<rect x="88" y="196" width="60" height="44" fill="#FFF6B8" transform="rotate(-4 118 218)"/><rect x="170" y="200" width="70" height="52" fill="#FFFFFF" transform="rotate(3 205 226)"/><rect x="110" y="252" width="80" height="40" fill="#BFE3F5" transform="rotate(-2 150 272)"/>`);

  // Марина: стул, персонаж, стол, предметы
  const mar = st.marina;
  if (mar) {
    o.push(chair(390, 760));
    o.push(person('marina', { t, x: 390, y: 760, ...mar }));
  }
  o.push(desk(130, 690, 520));
  o.push(monitor(170, 540, 150, 100, st.marinaMonitor || miniSklad(150, 100, { sel: 2 }), { stand: 42 }));
  o.push(`<g transform="translate(560,690) rotate(-4)"><rect x="-40" y="-14" width="80" height="14" fill="#FFFFFF"/><rect x="-36" y="-26" width="76" height="12" fill="#F4F0E6"/><rect x="-38" y="-36" width="78" height="10" fill="#FFFFFF"/></g>`);
  o.push(`<g transform="translate(470,686)"><rect x="-22" y="-6" width="44" height="8" rx="2" fill="#3A3E47"/><rect x="-18" y="-30" width="36" height="26" rx="3" fill="#5D6470"/><rect x="-14" y="-26" width="28" height="7" fill="#B8D88C"/></g>`);
  o.push(mug(610, 684, '#FFFFFF', 0.9, t, false, 'БУХ'));
  o.push(`<g transform="translate(150,690)"><rect x="-14" y="-24" width="28" height="24" rx="4" fill="#E08A5B"/><path d="M0,-24 Q-8,-50 0,-60 Q8,-50 0,-24" fill="#6BA36B"/></g>`);
  if (st.laptopMarina) o.push(laptopFront(st.laptopMarina[0], st.laptopMarina[1], 150, 94, miniSklad(150, 94, { wall: '#3A6EA5', sel: st.laptopSel ?? 1 })));

  // Лёша
  const le = st.lesha;
  if (le) o.push(chair(1540, 760));
  if (le && !le.hidden) o.push(person('lesha', { t, x: 1540, y: le.y || 760, ...le }));
  o.push(desk(1290, 690, 500));
  o.push(monitor(1640, 530, 150, 108, st.leshaMonitor || miniDesktop(150, 108), { stand: 44 }));
  if (st.leshaLaptopFront) o.push(laptopFront(st.leshaLaptopFront[0], st.leshaLaptopFront[1], 140, 88, miniSklad(140, 88, { wall: '#3A6EA5' })));
  o.push(`<g transform="translate(1340,690)"><path d="M-12,0 Q-14,-20 0,-22 Q16,-22 14,-8 L22,-10 L14,0Z" fill="#F4C531"/><circle cx="4" cy="-14" r="2" fill="#222"/></g>`);
  if (st.leshaMug !== false) o.push(mug(st.leshaMug ? st.leshaMug[0] : 1440, st.leshaMug ? st.leshaMug[1] : 684, '#2F6F8F', 0.9, t, false));
  if (le && le.sleepHead) o.push(sleepyLesha(t, le));
  if (st.genaMugOnDesk) o.push(mug(st.genaMugOnDesk[0], st.genaMugOnDesk[1], '#F4F1EA', 0.95, t, true));
  if (st.leshaLaptopBack) o.push(laptopBack(1450, 596, 180, 100, { glow: st.laptopGlow }));

  // Гена и тележка
  if (st.cartAt) o.push(cart(st.cartAt[0], st.cartAt[1], st.cartN ?? 3, t, st.cartRolling));
  if (st.gena) o.push(person('gena', { t, standing: true, ...st.gena }));

  // свет
  if (mood === 'night') {
    o.push(`<mask id="nightM"><rect x="-800" y="-400" width="3600" height="1800" fill="#FFF"/>
      <circle cx="1540" cy="520" r="${st.lampR || 520}" fill="url(#lampG)"/>
      <ellipse cx="1500" cy="600" rx="330" ry="260" fill="url(#scrG)" opacity=".9"/></mask>`);
    o.push(`<rect x="-800" y="-400" width="3600" height="1800" fill="#070B1E" opacity="${st.dark ?? 0.82}" mask="url(#nightM)"/>`);
    if (st.screenGlow) o.push(`<ellipse cx="1540" cy="520" rx="200" ry="170" fill="#6EA8FF" opacity="${st.screenGlow}" style="mix-blend-mode:screen"/>`);
    // лампа
    o.push(`<g transform="translate(1745,690)"><rect x="-26" y="-8" width="52" height="8" rx="3" fill="#2E3238"/><path d="M0,-8 L-30,-110 L10,-150" stroke="#2E3238" stroke-width="7" fill="none"/><path d="M-6,-170 L40,-150 L20,-118 Z" fill="#2E3238"/><circle cx="24" cy="-130" r="10" fill="#FFE7A3"/></g>`);
  }
  if (mood === 'morning') {
    o.push(`<path d="M704,456 L1116,456 L1500,1080 L500,1080Z" fill="#FFD9A0" opacity=".16"/>`);
    o.push(`<rect x="-800" y="-400" width="3600" height="1800" fill="#FFB36B" opacity=".07"/>`);
  }
  return o.join('');
}

function sleepyLesha(t, le) {
  const c = CAST.lesha;
  const o = [];
  // голова лежит на столе, руки под ней
  const x = 1520, y = 660;
  const br = Math.sin(t * 1.3) * 2;
  o.push(`<path d="M${x - 150},${y + 30} Q${x - 60},${y - 20} ${x + 10},${y + 26} Q${x + 80},${y - 20} ${x + 170},${y + 30}" stroke="${c.top}" stroke-width="34" fill="none" stroke-linecap="round"/>`);
  o.push(`<path d="M${x - 90},${y + 40} Q${x},${y - 60 + br} ${x + 110},${y + 40}Z" fill="${c.top}"/>`);
  o.push(`<g transform="translate(${x + 10},${y - 10 + br}) rotate(78)">`);
  o.push(`<ellipse cx="0" cy="0" rx="52" ry="60" fill="${c.skin}"/>`);
  o.push(`<path d="M-58,-6 Q-64,-64 0,-70 Q64,-64 58,-6 Q40,-40 20,-36 Q0,-44 -20,-34 Q-40,-34 -58,-6Z" fill="${c.hair}" transform="rotate(0)"/>`);
  o.push(`<path d="M-26,-2 Q-18,4 -10,-2 M10,-2 Q18,4 26,-2" stroke="#2A2220" stroke-width="3.5" fill="none" stroke-linecap="round"/>`);
  o.push(`<ellipse cx="0" cy="32" rx="6" ry="${3 + Math.max(0, Math.sin(t * 1.3)) * 3}" fill="#7A2E2E"/>`);
  o.push('</g>');
  // Z-z-z
  for (let i = 0; i < 3; i++) {
    const k = ((t * 0.45 + i / 3) % 1);
    o.push(`<text x="${x + 70 + k * 60 + i * 6}" y="${y - 70 - k * 110}" font-family="Onest" font-weight="800" font-size="${22 + k * 26}" fill="#5B6E86" opacity="${Math.sin(k * Math.PI)}">z</text>`);
  }
  return o.join('');
}

function wallClock(x, y, r, secs) {
  const h = (secs / 3600) % 12, m = (secs / 60) % 60;
  const ah = h / 12 * 360, am = m / 60 * 360;
  const o = [`<g transform="translate(${x},${y})"><circle r="${r + 6}" fill="#3A3F4B"/><circle r="${r}" fill="#FFFFFF"/>`];
  for (let i = 0; i < 12; i++) o.push(`<rect x="-1.5" y="${-r + 4}" width="3" height="${i % 3 ? 5 : 9}" fill="#3A3F4B" transform="rotate(${i * 30})"/>`);
  o.push(`<rect x="-2.5" y="${-r * 0.5}" width="5" height="${r * 0.55}" rx="2" fill="#2B2F36" transform="rotate(${ah})"/>`);
  o.push(`<rect x="-1.8" y="${-r * 0.8}" width="3.6" height="${r * 0.85}" rx="2" fill="#2B2F36" transform="rotate(${am})"/>`);
  o.push(`<circle r="3.5" fill="#D63F6E"/></g>`);
  return o.join('');
}

function order(x, y) {
  const w = 150, h = 212;
  return `<g transform="translate(${x},${y}) rotate(1.2)">
    <rect x="3" y="4" width="${w}" height="${h}" fill="#000" opacity=".12"/>
    <path d="M0,0 H${w} V${h - 14} L${w - 14},${h} H0Z" fill="#FFFFFF"/>
    <path d="M${w},${h - 14} L${w - 14},${h - 14} L${w - 14},${h}Z" fill="#E6E2DA"/>
    <text x="${w / 2}" y="26" font-family="PT Serif" font-weight="700" font-size="13" text-anchor="middle" fill="#1A1A1A">ПРИКАЗ № 47</text>
    <text x="${w / 2}" y="40" font-family="PT Serif" font-size="7.5" text-anchor="middle" fill="#333">от 14.09.2026</text>
    <text x="${w / 2}" y="58" font-family="PT Serif" font-style="italic" font-size="8" text-anchor="middle" fill="#333">О переходе на Linux</text>
    <text font-family="PT Serif" font-size="9.4" fill="#111"><tspan x="14" y="84">С 1 числа все рабочие</tspan><tspan x="14" y="97">места переводятся</tspan><tspan x="14" y="110">на Linux.</tspan></text>
    <rect x="14" y="122" width="118" height="3" fill="#DDD"/><rect x="14" y="130" width="96" height="3" fill="#DDD"/><rect x="14" y="138" width="110" height="3" fill="#DDD"/>
    <text x="14" y="176" font-family="PT Serif" font-size="7.5" fill="#333">Директор</text>
    <path d="M60,178 q10,-16 18,-4 q6,8 14,-8 q6,-8 12,2" stroke="#2146A8" stroke-width="1.6" fill="none"/>
    <circle cx="110" cy="176" r="15" fill="none" stroke="#6D7FC8" stroke-width="1.4" opacity=".6"/>
    <rect x="-10" y="-8" width="34" height="14" fill="#F3E9B5" opacity=".75" transform="rotate(-30 7 -1)"/>
    <rect x="${w - 24}" y="-8" width="34" height="14" fill="#F3E9B5" opacity=".75" transform="rotate(30 ${w - 7} -1)"/>
    <rect x="-10" y="${h - 8}" width="34" height="14" fill="#F3E9B5" opacity=".75" transform="rotate(28 7 ${h - 1})"/>
  </g>`;
}

/* ─── Лёша крупно (для «окна с веб-камерой») ───────────────────────────────────────────── */
function facecam(t, expr, mood = 'day') {
  const bg = mood === 'night' ? '#18203D' : mood === 'anger' ? '#5A2A2A' : '#D9E4EA';
  const s = { t, x: 150, y: 470, scale: 1.18, ...expr };
  const glow = mood === 'night' ? `<rect width="300" height="300" fill="#5E8CFF" opacity=".16"/>` : '';
  return `<svg viewBox="0 0 300 300" width="300" height="300"><rect width="300" height="300" fill="${bg}"/>${person('lesha', s)}${glow}</svg>`;
}
