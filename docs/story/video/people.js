// «Злой csproj»: персонажи. Анфас — когда сидят, стоят и говорят; в профиль — когда идут.
//
// Руки считаются в 3D (x вправо, y вниз, z к камере) двухзвенной инверсной кинематикой с «полюсом»
// локтя (вниз, чуть наружу и вперёд), а рисуются проекцией на экран: поднятая к губам кружка даёт локоть
// перед грудью и почти вертикальное предплечье, как у живого человека, а не «ломает» руку вбок.
// Шаг привязан к пройденному пути: опорная стопа стоит на полу и не скользит.

const CAST = {
  lesha: { skin: '#F1C6A4', skinD: '#DDA887', hair: '#3A2A20', top: '#2F6F8F', topD: '#245872', pants: '#3B4252', pantsD: '#2E3441',
           style: 'hoodie', hairStyle: 'messy', phase: 0.1 },
  marina: { skin: '#EFC5A6', skinD: '#D9A98A', hair: '#9C4128', top: '#8B2F4B', topD: '#6F2239', pants: '#3E3A4A', pantsD: '#302D3A',
            style: 'cardigan', hairStyle: 'bun', glasses: true, phase: 0.55 },
  gena: { skin: '#E2AE8A', skinD: '#C99373', hair: '#6B5A4B', top: '#557A5B', topD: '#40604A', pants: '#4A5263', pantsD: '#3A4150',
          style: 'flannel', hairStyle: 'short', beard: true, phase: 0.9 },
};
const TAU = Math.PI * 2;

/* ─── геометрия ─────────────────────────────────────────────────────────────────────────── */
const v3 = {
  add: (a, b) => [a[0] + b[0], a[1] + b[1], a[2] + b[2]],
  sub: (a, b) => [a[0] - b[0], a[1] - b[1], a[2] - b[2]],
  mul: (a, k) => [a[0] * k, a[1] * k, a[2] * k],
  dot: (a, b) => a[0] * b[0] + a[1] * b[1] + a[2] * b[2],
  len: a => Math.hypot(a[0], a[1], a[2]),
};
const norm3 = a => v3.mul(a, 1 / (v3.len(a) || 1));

/** Двухзвенная ИК в 3D: плечо S → кисть H, длины L1/L2, pole — куда смотрит локоть. */
function ik3(S, H, L1, L2, pole) {
  let d = v3.sub(H, S);
  let dl = v3.len(d);
  const max = L1 + L2 - 0.5;
  if (dl > max) { H = v3.add(S, v3.mul(d, max / dl)); d = v3.sub(H, S); dl = max; }
  dl = Math.max(dl, 1e-3);
  const dir = v3.mul(d, 1 / dl);
  const cosA = clamp((L1 * L1 + dl * dl - L2 * L2) / (2 * L1 * dl), -1, 1);
  const sinA = Math.sqrt(1 - cosA * cosA);
  let p = v3.sub(pole, v3.mul(dir, v3.dot(pole, dir)));
  p = v3.len(p) < 1e-6 ? [0, 1, 0] : norm3(p);
  const E = v3.add(S, v3.add(v3.mul(dir, L1 * cosA), v3.mul(p, L1 * sinA)));
  return { E, H };
}

/** Двухзвенная ИК в плоскости; score(ex, ey) выбирает одно из двух решений (колено вперёд, локоть вниз). */
function ik2(sx, sy, hx, hy, L1, L2, score) {
  let dx = hx - sx, dy = hy - sy, d = Math.hypot(dx, dy);
  const max = L1 + L2 - 0.5;
  if (d > max) { hx = sx + dx / d * max; hy = sy + dy / d * max; dx = hx - sx; dy = hy - sy; d = max; }
  d = Math.max(d, 1e-3);
  const a = Math.atan2(dy, dx);
  const A = Math.acos(clamp((L1 * L1 + d * d - L2 * L2) / (2 * L1 * d), -1, 1));
  const e1 = [sx + L1 * Math.cos(a + A), sy + L1 * Math.sin(a + A)];
  const e2 = [sx + L1 * Math.cos(a - A), sy + L1 * Math.sin(a - A)];
  const e = score(...e1) >= score(...e2) ? e1 : e2;
  return { ex: e[0], ey: e[1], hx, hy };
}

/* ─── ходьба ────────────────────────────────────────────────────────────────────────────── */
/** Путь с трапециевидной скоростью: разгон и торможение по 18 % времени, в середине — ровный шаг. */
function trap(k, a = 0.18) {
  const vmax = 1 / (1 - a);
  if (k <= 0) return { d: 0, v: 0 };
  if (k >= 1) return { d: 1, v: 0 };
  if (k < a) return { d: 0.5 * vmax / a * k * k, v: k / a };
  if (k > 1 - a) { const r = 1 - k; return { d: 1 - 0.5 * vmax / a * r * r, v: r / a }; }
  return { d: 0.5 * vmax * a + vmax * (k - a), v: 1 };
}

/**
 * Проход из x0 в x1 за [t0, t1]. Длина шага подбирается так, чтобы проход начинался и кончался
 * в середине опоры (ноги вместе). phase — фаза шага ближней ноги; amp — 0..1, доля полной скорости.
 */
function walkSeg(t, t0, t1, x0, x1, S0 = 125) {
  const dist = Math.abs(x1 - x0);
  const n = Math.max(1, Math.round(dist / (2 * S0)));
  const S = dist / (2 * n);
  const k = seg(t, t0, t1);
  const { d, v } = trap(k);
  const x = lerp(x0, x1, d);
  return { x, moving: k > 0 && k < 1, dir: x1 >= x0 ? 1 : -1, phase: Math.PI / 2 + Math.PI * (d * dist) / S, S, amp: v };
}

/* ─── анфас ─────────────────────────────────────────────────────────────────────────────── */
const SHOULDER_Y = -138, UPPER = 90, FORE = 96;

/** Рука анфас. target — [x, y] или [x, y, z] в координатах таза; null — опущена вдоль тела. */
function armFront(c, side, target, hold, s) {
  const S = [side * 70, SHOULDER_Y, 0];
  let T = target ? (target.length === 3 ? target : [target[0], target[1], target[1] > 0 ? 8 : 50]) : [side * 74, 46, 4];
  const pole = norm3([side * 0.3, 1, -0.1]);
  const { E, H } = ik3(S, T, UPPER, FORE, pole);
  const d = `M${S[0]},${S[1]} L${E[0].toFixed(1)},${E[1].toFixed(1)} L${H[0].toFixed(1)},${H[1].toFixed(1)}`;
  const o = [`<path d="${d}" stroke="${c.topD}" stroke-width="31" fill="none" stroke-linecap="round" stroke-linejoin="round"/>`,
    `<path d="${d}" stroke="${c.top}" stroke-width="25" fill="none" stroke-linecap="round" stroke-linejoin="round"/>`];
  if (hold === 'mug') o.push(mug(H[0] - side * 16, H[1] + 4, s.mugColor || '#F4F1EA', 1, s.t, s.steam));
  o.push(`<circle cx="${H[0].toFixed(1)}" cy="${H[1].toFixed(1)}" r="13" fill="${c.skin}" stroke="${c.skinD}" stroke-width="2"/>`);
  return { svg: o.join(''), front: H[2] > 20 && H[1] < -150 };
}

/**
 * Персонаж анфас. Начало координат — середина таза (сидит на стуле или стоит).
 * s: { x, y, scale, turn(-1..1), tilt(°), lean(°), look:[x,y], eyes, brow, angry, worried, smile, smirk, mouth,
 *      teeth, o, puff, red, lookDown, sleep, handL/handR: [x,y(,z)] | null, holdL/holdR: 'mug', standing, side }
 * С s.side (1 — вправо, −1 — влево) рисуется в профиль: см. personSide.
 */
function person(who, s) {
  if (s.side) return personSide(who, s);
  const c = CAST[who];
  const sc = s.scale || 1;
  const fx = (s.turn || 0) * 17;
  const out = [`<g transform="translate(${s.x},${s.y}) scale(${sc},${sc}) rotate(${s.lean || 0})">`];
  if (s.standing) {
    for (const side of [-1, 1]) {
      out.push(`<path d="M${side * 27},4 L${side * 28},122 L${side * 30},238" stroke="${c.pants}" stroke-width="42" fill="none" stroke-linecap="round" stroke-linejoin="round"/>`);
      out.push(`<ellipse cx="${side * 36}" cy="252" rx="30" ry="13" fill="#2B2B30"/>`);
    }
  }
  const bob = Math.sin((s.t || 0) * 1.7 + c.phase * 5) * 1.5;
  out.push(`<g transform="translate(0,${bob})">`);
  if (c.style === 'hoodie') out.push(`<path d="M-58,-150 Q0,-120 58,-150 Q70,-185 0,-190 Q-70,-185 -58,-150Z" fill="${c.topD}"/>`);
  out.push(torsoFront(c));
  out.push(`<rect x="-15" y="-182" width="30" height="30" rx="8" fill="${c.skinD}"/>`);
  const arms = [armFront(c, -1, s.handL, s.holdL, s), armFront(c, 1, s.handR, s.holdR, s)];
  arms.filter(a => !a.front).forEach(a => out.push(a.svg));
  out.push(`<g transform="rotate(${s.tilt || 0} 0 -180)">${head(c, s, fx)}</g>`);
  arms.filter(a => a.front).forEach(a => out.push(a.svg));
  out.push('</g></g>');
  return out.join('');
}

function torsoFront(c) {
  const o = [`<path d="M-66,6 L-78,-128 Q-78,-156 -46,-160 L46,-160 Q78,-156 78,-128 L66,6 Z" fill="${c.top}"/>`];
  if (c.style === 'cardigan') {
    o.push(`<path d="M-24,-160 L0,-96 L24,-160 Z" fill="#F6EFE6"/>`);
    o.push(`<path d="M-4,-96 L-4,6 M4,-96 L4,6" stroke="${c.topD}" stroke-width="3"/>`);
    o.push(`<circle cx="0" cy="-70" r="4" fill="#E8D8B0"/><circle cx="0" cy="-40" r="4" fill="#E8D8B0"/><circle cx="0" cy="-10" r="4" fill="#E8D8B0"/>`);
    o.push(`<circle cx="-40" cy="-120" r="6" fill="#D8B45A"/>`);
  } else if (c.style === 'flannel') {
    o.push(`<path d="M-30,-160 L-30,6 L30,6 L30,-160 Z" fill="#2E3238"/>`);
    o.push(`<g transform="translate(0,-98) scale(0.9)"><ellipse cx="0" cy="0" rx="13" ry="17" fill="#15161A"/><ellipse cx="0" cy="4" rx="8" ry="11" fill="#F4F4F4"/><circle cx="-4" cy="-7" r="2" fill="#F4F4F4"/><circle cx="4" cy="-7" r="2" fill="#F4F4F4"/><path d="M-4,-3 L4,-3 L0,1Z" fill="#F2B233"/><ellipse cx="-6" cy="17" rx="5" ry="2.5" fill="#F2B233"/><ellipse cx="6" cy="17" rx="5" ry="2.5" fill="#F2B233"/></g>`);
    for (const x of [-60, -45, 45, 60]) o.push(`<path d="M${x},-150 L${x * 0.9},6" stroke="${c.topD}" stroke-width="4" opacity=".6"/>`);
    for (const y of [-130, -95, -60, -25]) o.push(`<path d="M-76,${y} L-30,${y} M30,${y} L76,${y}" stroke="${c.topD}" stroke-width="4" opacity=".6"/>`);
  } else if (c.style === 'hoodie') {
    o.push(`<path d="M-14,-150 L-18,-100 M14,-150 L18,-100" stroke="#E9E6DF" stroke-width="4" stroke-linecap="round"/>`);
    o.push(`<path d="M-42,-40 Q0,-30 42,-40 L38,0 L-38,0 Z" fill="${c.topD}" opacity=".55"/>`);
  }
  return o.join('');
}

/* ─── профиль ───────────────────────────────────────────────────────────────────────────── */
const HIP_TO_ANKLE = 238, THIGH = 126, SHIN = 126;

/** Где стопа ноги с фазой ph относительно таза: опора — стопа едет назад вровень с ходом тела; перенос — дугой вперёд. */
function footAt(ph, S, lift) {
  const k = ((ph % TAU) + TAU) % TAU;
  if (k < Math.PI) return [S / 2 - S * k / Math.PI, 0];
  const u = (k - Math.PI) / Math.PI;
  return [-S / 2 + S * (0.5 - 0.5 * Math.cos(Math.PI * u)), -lift * Math.sin(Math.PI * u)];
}

/**
 * Персонаж в профиль, лицом вправо (side = 1) или влево (side = −1).
 * s: { x, y, scale, side, phase, S, amp, t, mouth, eyes, brow, smile, lookDown,
 *      near/far: [x, y] — куда тянется ближняя/дальняя рука (вперёд = +x), holdNear: 'mug' }
 */
function personSide(who, s) {
  const c = CAST[who];
  const sc = s.scale || 1, dir = s.side;
  const ph = s.phase ?? Math.PI / 2, amp = s.amp ?? 0, S = s.S || 120;
  const lift = 34 * amp;
  const bob = -4 * amp * Math.abs(Math.sin(ph));
  const out = [`<g transform="translate(${s.x},${s.y}) scale(${sc * dir},${sc})">`];

  // ноги: дальняя темнее
  for (const [p, col] of [[ph + Math.PI, c.pantsD], [ph, c.pants]]) {
    const [fx, fy] = footAt(p, S, lift);
    const ax = fx, ay = HIP_TO_ANKLE + fy;
    const k = ik2(0, bob, ax, ay, THIGH, SHIN, (ex) => ex);
    const toe = fy < -2 ? 8 : 0;
    out.push(`<path d="M0,${bob} L${k.ex.toFixed(1)},${k.ey.toFixed(1)} L${k.hx.toFixed(1)},${k.hy.toFixed(1)}" stroke="${col}" stroke-width="40" fill="none" stroke-linecap="round" stroke-linejoin="round"/>`);
    out.push(`<ellipse cx="${(k.hx + 16).toFixed(1)}" cy="${(k.hy + 13).toFixed(1)}" rx="31" ry="12" fill="#2B2B30" transform="rotate(${toe} ${k.hx} ${k.hy})"/>`);
  }

  out.push(`<g transform="translate(0,${bob}) rotate(${3 * amp + (s.lean || 0)} 0 0)">`);
  const shoulder = [4, -140];
  const arm = (target, swing, col, colD, hold) => {
    let E, H;
    if (target) {
      const k = ik2(shoulder[0], shoulder[1], target[0], target[1], UPPER, FORE, (ex, ey) => ey - 0.6 * ex);
      E = [k.ex, k.ey]; H = [k.hx, k.hy];
    } else {
      const th = swing;
      E = [shoulder[0] + UPPER * Math.sin(th), shoulder[1] + UPPER * Math.cos(th)];
      H = [E[0] + FORE * Math.sin(th + 0.3), E[1] + FORE * Math.cos(th + 0.3)];
    }
    const d = `M${shoulder[0]},${shoulder[1]} L${E[0].toFixed(1)},${E[1].toFixed(1)} L${H[0].toFixed(1)},${H[1].toFixed(1)}`;
    return `<path d="${d}" stroke="${colD}" stroke-width="31" fill="none" stroke-linecap="round" stroke-linejoin="round"/>` +
      `<path d="${d}" stroke="${col}" stroke-width="25" fill="none" stroke-linecap="round" stroke-linejoin="round"/>` +
      (hold === 'mug' ? mug(H[0] + 12, H[1] + 2, s.mugColor || '#F4F1EA', 1, s.t, s.steam) : '') +
      (hold === 'laptop' ? `<g transform="translate(${H[0] - 20},${H[1] + 6})"><rect x="0" y="-5" width="104" height="9" rx="3" fill="#9BA0A9"/><rect x="92" y="-86" width="9" height="84" rx="3" fill="#AEB3BC" transform="rotate(12 96 -3)"/></g>` : '') +
      `<circle cx="${H[0].toFixed(1)}" cy="${H[1].toFixed(1)}" r="13" fill="${c.skin}" stroke="${c.skinD}" stroke-width="2"/>`;
  };
  const sw = 0.42 * amp * Math.cos(ph);
  if (!s.holdFar) out.push(arm(s.far, sw, c.topD, '#00000055', null));
  if (c.style === 'hoodie') out.push(`<path d="M-38,-146 Q-46,-190 -8,-194 Q12,-178 -2,-150Z" fill="${c.topD}"/>`);
  out.push(`<path d="M-28,6 C-40,-50 -42,-110 -30,-150 Q-20,-166 4,-166 Q30,-164 32,-140 C38,-100 36,-40 28,6 Z" fill="${c.top}"/>`);
  if (c.style === 'flannel') {
    for (const x of [-18, 8]) out.push(`<path d="M${x},-160 L${x},4" stroke="${c.topD}" stroke-width="4" opacity=".6"/>`);
    for (const y of [-130, -95, -60, -25]) out.push(`<path d="M-36,${y} L34,${y}" stroke="${c.topD}" stroke-width="4" opacity=".6"/>`);
  }
  out.push(`<rect x="-8" y="-190" width="24" height="36" rx="8" fill="${c.skinD}"/>`);
  out.push(headSide(c, s));
  out.push(arm(s.near, -sw, c.top, c.topD, s.holdNear));
  if (s.holdFar === 'laptop' || s.holdFar === 'mug') out.push(arm(s.far, sw, c.topD, '#00000055', s.holdFar));
  out.push('</g></g>');
  return out.join('');
}

function headSide(c, s) {
  const o = [];
  const hy = -240;
  const open = (s.eyes ?? 1) * blink(s.t || 0, c.phase) * (1 - (s.lookDown || 0) * 0.5);
  if (c.hairStyle === 'bun') o.push(`<circle cx="-40" cy="${hy - 50}" r="28" fill="${c.hair}"/>`);
  o.push(`<ellipse cx="6" cy="${hy}" rx="50" ry="58" fill="${c.skin}"/>`);
  o.push(`<path d="M50,${hy - 10} Q70,${hy + 6} 52,${hy + 16}" fill="${c.skin}" stroke="${c.skinD}" stroke-width="3" stroke-linejoin="round"/>`);
  if (c.beard) {
    o.push(`<path d="M-4,${hy + 12} Q0,${hy + 58} 36,${hy + 64} Q56,${hy + 60} 58,${hy + 40} Q50,${hy + 46} 40,${hy + 44} Q24,${hy + 40} 18,${hy + 26} Q8,${hy + 14} -4,${hy + 12}Z" fill="${c.hair}"/>`);
    o.push(`<path d="M30,${hy + 28} Q46,${hy + 22} 56,${hy + 32}" stroke="${c.hair}" stroke-width="7" fill="none" stroke-linecap="round"/>`);
  }
  // рот
  const m = clamp(s.mouth || 0);
  if (m > 0.06) o.push(`<ellipse cx="44" cy="${hy + 38}" rx="7" ry="${2 + m * 8}" fill="#7A2E2E"/>`);
  else o.push(`<path d="M34,${hy + 37} Q42,${hy + 37 + (s.smile || 0) * 7} 50,${hy + 35}" stroke="#7A2E2E" stroke-width="4" fill="none" stroke-linecap="round"/>`);
  // глаз и бровь
  o.push(`<ellipse cx="34" cy="${hy - 4}" rx="5" ry="${Math.max(0.8, 7 * Math.min(open, 1.1))}" fill="#2A2220"/>`);
  const by = hy - 22 - (s.brow || 0) * 8;
  o.push(`<path d="M24,${by} L44,${by + 2}" stroke="${c.hairStyle === 'bun' ? '#7A3322' : c.hair}" stroke-width="5" stroke-linecap="round"/>`);
  if (c.glasses) o.push(`<rect x="22" y="${hy - 14}" width="26" height="20" rx="7" fill="none" stroke="#6B4E2E" stroke-width="3"/><path d="M22,${hy - 6} L-2,${hy - 8}" stroke="#6B4E2E" stroke-width="3"/>`);
  // ухо и волосы
  o.push(`<ellipse cx="-6" cy="${hy + 2}" rx="9" ry="14" fill="${c.skinD}"/>`);
  if (c.hairStyle === 'messy') {
    o.push(`<path d="M-44,${hy + 26} Q-62,${hy - 34} -12,${hy - 60} Q38,${hy - 70} 58,${hy - 26} L48,${hy - 16} Q32,${hy - 40} 8,${hy - 34} Q-14,${hy - 28} -18,${hy - 6} Q-28,${hy + 10} -44,${hy + 26}Z" fill="${c.hair}"/>`);
    o.push(`<path d="M-6,${hy - 62} L4,${hy - 80} L14,${hy - 60} M20,${hy - 60} L36,${hy - 72} L34,${hy - 52}" fill="${c.hair}" stroke="${c.hair}" stroke-width="6" stroke-linejoin="round"/>`);
  } else if (c.hairStyle === 'short') {
    o.push(`<path d="M-44,${hy + 20} Q-60,${hy - 40} 4,${hy - 60} Q50,${hy - 58} 56,${hy - 22} Q34,${hy - 38} 8,${hy - 34} Q-18,${hy - 26} -24,${hy + 6}Z" fill="${c.hair}"/>`);
  } else {
    o.push(`<path d="M-46,${hy + 30} Q-62,${hy - 40} 4,${hy - 62} Q52,${hy - 60} 56,${hy - 16} Q40,${hy - 42} 10,${hy - 36} Q-20,${hy - 26} -22,${hy + 20}Z" fill="${c.hair}"/>`);
  }
  return o.join('');
}

/** Рука из-за кадра (крупный план доски): плечо ниже кадра; кончик маркера — в точке (px, py). Без маркера — кисть в точке. */
function armFromBelow(c, px, py, marker = '#2C63C9', sleeveScale = 1) {
  const hx = marker ? px - 30 : px, hy = marker ? py + 46 : py;
  const sx = hx - 330, sy = 1240;
  const k = ik2(sx, sy, hx, hy, 420, 420, (ex, ey) => -ex + ey * 0.2);
  const d = `M${sx},${sy} L${k.ex.toFixed(1)},${k.ey.toFixed(1)} L${k.hx.toFixed(1)},${k.hy.toFixed(1)}`;
  return `<path d="${d}" stroke="${c.topD}" stroke-width="${78 * sleeveScale}" fill="none" stroke-linecap="round" stroke-linejoin="round"/>` +
    `<path d="${d}" stroke="${c.top}" stroke-width="${66 * sleeveScale}" fill="none" stroke-linecap="round" stroke-linejoin="round"/>` +
    (marker ? `<path d="M${k.hx + 4},${k.hy - 4} L${px},${py}" stroke="${marker}" stroke-width="16" stroke-linecap="round"/><circle cx="${px}" cy="${py}" r="5" fill="#1d1d1d"/>` : '') +
    `<circle cx="${k.hx}" cy="${k.hy}" r="30" fill="${c.skin}" stroke="${c.skinD}" stroke-width="3"/>` +
    (marker ? `<path d="M${k.hx - 16},${k.hy - 20} Q${k.hx + 4},${k.hy - 30} ${k.hx + 20},${k.hy - 14}" stroke="${c.skinD}" stroke-width="3" fill="none"/>` : '');
}

/* ─── голова анфас ─────────────────────────────────────────────────────────────────────── */
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
