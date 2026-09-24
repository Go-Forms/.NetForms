// «Злой csproj»: экраны — терминал, редактор, браузер, рабочий стол, чат, доска.

const PROMPT = '<span class="pr">$</span> ';
const cursorOn = t => Math.floor(t * 2.2) % 2 === 0;

/** Окно терминала. lines — массив HTML-строк; вставляются как есть. */
function terminal({ x = 60, y = 50, w = 1480, h = 820, title = 'lesha@sklad-laptop: ~/sklad', lines, font = 28 }) {
  // как настоящий терминал: сверху вниз, старые строки уходят за верхний край
  const perRow = Math.floor((w - 88) / (font * 0.6));
  const maxRows = Math.floor((h - 46 - 52) / (font * 1.5));
  const rows = l => Math.max(1, Math.ceil(l.replace(/<[^>]*>/g, '').replace(/&[a-z]+;/g, 'x').length / perRow));
  let total = lines.reduce((a, l) => a + rows(l), 0);
  let from = 0;
  while (total > maxRows && from < lines.length - 1) total -= rows(lines[from++]);
  return `<div class="term" style="left:${x}px;top:${y}px;width:${w}px;height:${h}px">
    <div class="bar">${esc(title)}</div>
    <div class="body" style="font-size:${font}px">${lines.slice(from).map(l => `<div class="ln">${l}</div>`).join('')}</div></div>`;
}

/** Строка с командой: набранная часть + курсор (пока идёт набор или команда последняя). */
function cmd(id, t, { showCursor = false } = {}) {
  const ty = TL.typing[id];
  const n = typed(id, t);
  const txt = esc(ty.text.slice(0, n));
  const typing = n > 0 && n < ty.text.length;
  const enter = TL.marks[id + '.enter'];
  const waiting = enter === undefined ? true : t < enter;
  const cur = (typing || (showCursor && (waiting || n === 0) && cursorOn(t))) ? '<span class="cur"></span>' : '';
  return PROMPT + txt + cur;
}
const promptLine = t => PROMPT + (cursorOn(t) ? '<span class="cur"></span>' : '');

/* ─── редактор Sklad.csproj ─────────────────────────────────────────────────────────────── */
function xml(line) {
  // простая подсветка XML: теги, атрибуты, строки
  return esc(line)
    .replace(/(&lt;\/?)([\w.]+)/g, '<span class="x-pun">$1</span><span class="x-tag">$2</span>')
    .replace(/ ([\w.]+)=(&quot;[^&]*&quot;)/g, ' <span class="x-attr">$1</span>=<span class="x-str">$2</span>')
    .replace(/(\/?&gt;)/g, '<span class="x-pun">$1</span>');
}

/**
 * rows: [{ text, gut:'a'|'m'|null, glow:[from,len]|null, caret:col|null }]
 */
function editor({ rows, dirty, title = 'Sklad.csproj', panel = null, curRow = -1, t = 0, scroll = 0 }) {
  const body = rows.map((r, i) => {
    let html;
    if (r.glow) {
      const [a, n] = r.glow;
      html = xml(r.text.slice(0, a)) + `<span class="${r.glowClass || 'glow'}">${xml(r.text.slice(a, a + n))}</span>` + xml(r.text.slice(a + n));
    } else if (r.caret !== undefined && r.caret !== null) {
      html = xml(r.text.slice(0, r.caret)) + (cursorOn(t * 1.3) || r.solid ? '<span class="caret"></span>' : '<span class="caret" style="opacity:0"></span>') + xml(r.text.slice(r.caret));
    } else {
      html = xml(r.text);
    }
    return `<div class="row ${i === curRow ? 'cur-row' : ''}" style="${r.style || ''}"><span class="num">${i + 1}</span>${r.gut ? `<span class="gut ${r.gut}"></span>` : ''}${html}</div>`;
  }).join('');
  return `<div class="editor"><div class="tabs"><div class="tab">${title}${dirty ? '<span class="dot">●</span>' : ''}</div></div>
    <div class="crumbs">~/sklad › Sklad.csproj</div><div class="code"><div class="scroll" style="transform:translateY(${-scroll}px)">${body}</div></div>${panel || ''}</div>`;
}

/* ─── браузер ───────────────────────────────────────────────────────────────────────────── */
function browser({ tabs, active = 0, url, page, tabW = 300 }) {
  const tabHtml = tabs.map((tt, i) => `<div class="btab ${i === active ? 'on' : ''}" style="width:${tabW}px">${esc(tt)}</div>`).join('');
  return `<div class="browser"><div class="chrome"><div class="tabrow">${tabHtml}</div>
    <div class="addr"><div class="url">🔒&nbsp; ${esc(url)}</div></div></div><div class="page">${page}</div></div>`;
}

const WIN_GLYPH = `<svg width="26" height="26" viewBox="0 0 26 26" style="vertical-align:-5px;margin-right:10px"><rect x="1" y="1" width="11" height="11" fill="currentColor"/><rect x="14" y="1" width="11" height="11" fill="currentColor"/><rect x="1" y="14" width="11" height="11" fill="currentColor"/><rect x="14" y="14" width="11" height="11" fill="currentColor"/></svg>`;

function downloadPage(focus = 0) {
  const btn = (label, glyph) => `<span style="display:inline-block;margin:0 10px 10px 0;padding:12px 22px;border-radius:8px;background:#512bd4;color:#fff;font:700 24px/1 Onest">${glyph ? WIN_GLYPH : ''}${label}</span>`;
  const row = (os, items, glyph) => `<tr><td style="padding:18px 20px;font:700 26px Onest;color:#222;width:220px;vertical-align:top">${os}</td><td style="padding:12px 0">${items.map(i => btn(i, glyph)).join('')}</td></tr>`;
  const card = (title, rows, hl) => `<div style="border:2px solid ${hl ? '#512bd4' : '#e3e3e3'};border-radius:16px;margin-bottom:28px;${hl ? `box-shadow:0 0 0 ${8 * focus}px rgba(81,43,212,.15)` : ''}">
      <div style="padding:20px 26px;border-bottom:1px solid #eee;font:800 34px Onest;color:#171717">${title}</div>
      <table style="border-collapse:collapse;width:100%">${rows}</table></div>`;
  return `<div style="padding:40px 120px;font-family:Onest">
    <div style="font:800 56px Onest;color:#171717;margin-bottom:10px">Download .NET 10.0</div>
    <div style="font:400 24px Onest;color:#555;margin-bottom:34px">Runtimes for running apps · SDK for building apps</div>
    <div style="display:grid;grid-template-columns:1fr 1fr;gap:34px">
      <div>${card('.NET Runtime 10.0.0', row('Linux', ['Arm32', 'Arm64', 'x64']) + row('macOS', ['Arm64', 'x64']) + row('Windows', ['x64', 'x86', 'Arm64']))}
           ${card('ASP.NET Core Runtime 10.0.0', row('Linux', ['Arm64', 'x64']) + row('Windows', ['x64', 'Arm64']))}</div>
      <div>${card('Windows Desktop Runtime 10.0.0', row('Windows', ['x64', 'x86', 'Arm64'], true), true)}
        <div style="font:400 22px/1.5 Onest;color:#777;padding:0 8px">Windows Forms and WPF apps.</div></div>
    </div></div>`;
}

const NF_LOGO = `<svg viewBox="0 0 256 256" width="100%" height="100%"><defs><linearGradient id="nfbg" x1="0" y1="0" x2="1" y2="1"><stop offset="0" stop-color="#6A3DE8"/><stop offset="1" stop-color="#2A1A8A"/></linearGradient></defs><rect x="8" y="8" width="240" height="240" rx="52" fill="url(#nfbg)"/><rect x="44" y="60" width="168" height="136" rx="10" fill="#F4F2FB"/><path d="M44 70a10 10 0 0 1 10-10h148a10 10 0 0 1 10 10v18H44z" fill="#FFFFFF"/><rect x="44" y="87" width="168" height="2" fill="#D9D3F2"/><circle cx="186" cy="74" r="5" fill="#E0457B"/><circle cx="170" cy="74" r="5" fill="#C9C2E8"/><circle cx="154" cy="74" r="5" fill="#C9C2E8"/><rect x="56" y="70" width="52" height="8" rx="4" fill="#B7AEE0"/><rect x="60" y="104" width="64" height="9" rx="4.5" fill="#6A3DE8"/><rect x="60" y="122" width="136" height="20" rx="4" fill="#FFFFFF" stroke="#B7AEE0" stroke-width="2"/><rect x="138" y="160" width="60" height="24" rx="5" fill="#6A3DE8"/><rect x="152" y="169.5" width="32" height="5" rx="2.5" fill="#FFFFFF"/></svg>`;

/** Сайт NetForms (как site/ru/index.html), фразы подсвечиваются маркером по мере чтения. hl — [0..1]×4. */
function netformsPage(hl) {
  const mk = (i, txt) => {
    const k = hl[i] || 0;
    return `<span style="background:linear-gradient(90deg, rgba(255,214,90,.75) ${k * 100}%, transparent ${k * 100}%);border-radius:6px;padding:0 4px">${txt}</span>`;
  };
  const code = s => `<code style="font:600 .92em 'JetBrains Mono';background:#e7e1fb;color:#3b2a9a;padding:2px 8px;border-radius:6px">${s}</code>`;
  return `<div style="position:absolute;inset:0;background:#eceef3 radial-gradient(#c9cbd8 1.4px, transparent 1.6px) 0 0/22px 22px;font-family:Onest;color:#1a1730">
    <div style="display:flex;align-items:center;justify-content:space-between;padding:26px 110px">
      <div style="display:flex;align-items:center;gap:14px;font:800 32px Onest"><span style="width:48px;height:48px;display:inline-block">${NF_LOGO}</span>NetForms</div>
      <div style="display:flex;gap:34px;font:500 22px Onest;color:#4a4764"><span>Зачем</span><span>Состояние</span><span>Начать</span><span>Документация</span><span>GitHub</span></div></div>
    <div style="display:grid;grid-template-columns:1.15fr .85fr;gap:60px;padding:40px 110px">
      <div>
        <div style="font:600 20px/1 'JetBrains Mono';color:#5b34d6;letter-spacing:.04em;margin-bottom:24px">System.Windows.Forms · .NET 10 · Windows + Linux</div>
        <div style="font:800 76px/1.05 Onest;margin-bottom:30px">WinForms <span style="text-decoration:line-through;text-decoration-color:#d63f6e;text-decoration-thickness:6px;color:#75728c">только для Windows</span> везде.</div>
        <div style="font:400 34px/1.55 Onest;color:#2c2946">NetForms — это Windows Forms для .NET 10, одинаково работающий на Windows и Linux.
          ${mk(0, 'Те же пространства имён.')} ${mk(1, 'Тот же ' + code('Application.Run') + '.')} ${mk(2, 'Тот же ' + code('InitializeComponent') + '.')}
          ${mk(3, 'Ваш код компилируется без изменений.')}</div>
        <div style="display:flex;gap:18px;margin-top:40px"><span style="padding:18px 34px;border-radius:12px;background:#5b34d6;color:#fff;font:700 26px Onest">Начать</span>
          <span style="padding:18px 34px;border-radius:12px;background:#fff;border:2px solid #d6d5e2;font:700 26px Onest">Перевести готовое приложение</span></div>
      </div>
      <div style="padding-top:30px"><div style="background:#fff;border-radius:14px;box-shadow:0 12px 32px -12px rgba(26,23,48,.35);overflow:hidden">
        <div style="display:flex;justify-content:space-between;padding:14px 18px;border-bottom:1px solid #d6d5e2;font:700 22px Onest"><span>Склад</span><span style="color:#75728c">–&nbsp; □&nbsp; ×</span></div>
        <div style="padding:18px;font:400 20px Onest"><table style="width:100%;border-collapse:collapse">
          <tr style="background:#f6f6fa;font-weight:700"><td style="padding:10px">Наименование</td><td>Кол-во</td><td>Ячейка</td></tr>
          <tr><td style="padding:10px">Бумага офисная А4</td><td>212</td><td>А-1-03</td></tr>
          <tr style="background:#5b34d6;color:#fff"><td style="padding:10px">Картридж 12A</td><td>9</td><td>Б-2-01</td></tr>
          <tr><td style="padding:10px">Папка-регистратор</td><td>147</td><td>А-2-07</td></tr></table>
          <div style="text-align:right;margin-top:20px"><span style="display:inline-block;padding:10px 26px;border:2px dashed #5b34d6;border-radius:8px;font-weight:700">Сохранить</span></div></div></div>
        <div style="margin-top:16px;font:600 18px 'JetBrains Mono';color:#5b34d6">Anchor&nbsp; Bottom, Right</div></div>
    </div></div>`;
}

/* ─── старый рабочий стол со «Склад.exe» ─────────────────────────────────────────────────── */
const SKLAD_ICON = `<svg viewBox="0 0 96 96" width="96" height="96"><defs><linearGradient id="boxT" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#F3C98B"/><stop offset="1" stop-color="#D99A4E"/></linearGradient><linearGradient id="boxL" x1="0" y1="0" x2="1" y2="0"><stop offset="0" stop-color="#C47F36"/><stop offset="1" stop-color="#B06D2A"/></linearGradient><linearGradient id="gl" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#fff" stop-opacity=".75"/><stop offset="1" stop-color="#fff" stop-opacity="0"/></linearGradient></defs>
  <path d="M48,10 L86,28 L48,46 L10,28Z" fill="url(#boxT)"/><path d="M10,28 L48,46 L48,88 L10,70Z" fill="url(#boxL)"/><path d="M86,28 L48,46 L48,88 L86,70Z" fill="#C8873F"/>
  <path d="M29,19 L67,37 L67,50 L29,32Z" fill="#E9D7B8" opacity=".9"/><path d="M14,36 L44,50 L44,62 L14,48Z" fill="#2F6FB6"/><text x="16" y="52" font-family="Onest" font-weight="800" font-size="8" fill="#fff" transform="rotate(25 16 52)">СКЛАД</text>
  <path d="M48,10 L86,28 L48,46 L10,28Z" fill="url(#gl)"/>
  <rect x="2" y="66" width="26" height="26" rx="3" fill="#fff" stroke="#999"/><path d="M8,86 Q8,74 20,74 L20,70 L26,77 L20,84 L20,80 Q12,80 8,86Z" fill="#1f5fbf"/></svg>`;

function oldDesktop(t, tipK) {
  const folder = `<svg viewBox="0 0 96 96" width="96" height="96"><path d="M8,24 h28 l8,8 h44 v48 h-80z" fill="#E8B84A"/><path d="M8,36 h80 v44 h-80z" fill="#F4CE67"/></svg>`;
  const xls = `<svg viewBox="0 0 96 96" width="96" height="96"><path d="M20,8 h40 l18,18 v62 h-58z" fill="#fff" stroke="#9aa"/><rect x="28" y="40" width="42" height="36" fill="#2E8B57" opacity=".85"/><path d="M28,52 h42 M28,64 h42 M42,40 v36 M56,40 v36" stroke="#fff" stroke-width="2"/></svg>`;
  const bin = `<svg viewBox="0 0 96 96" width="96" height="96"><path d="M26,26 h44 l-5,58 h-34z" fill="#DCE6EE" stroke="#8aa0b4" stroke-width="2"/><rect x="22" y="18" width="52" height="9" rx="3" fill="#B7C7D5"/></svg>`;
  const icons = [
    { x: 40, y: 40, pic: SKLAD_ICON, name: 'Склад.exe', sel: true },
    { x: 40, y: 220, pic: folder, name: 'Документы' },
    { x: 40, y: 400, pic: xls, name: 'Остатки_2011.xls' },
    { x: 40, y: 580, pic: bin, name: 'Корзина' },
  ];
  const tip = tipK > 0 ? `<div class="tip" style="left:210px;top:128px;opacity:${tipK}">Склад.exe\nОписание файла: Складской учёт\nДата изменения: 14.03.2011 9:12\nРазмер: 2,41 МБ</div>` : '';
  return `<div class="desk-screen"><div class="mon"><div class="wp">
    ${icons.map(i => `<div class="icon ${i.sel ? 'sel' : ''}" style="left:${i.x}px;top:${i.y}px;padding:10px 4px"><div class="pic">${i.pic}</div>${i.name}</div>`).join('')}
    ${tip}<div class="taskbar">08:47&nbsp;&nbsp;21.09.2026</div></div></div></div>`;
}

/* ─── чат ───────────────────────────────────────────────────────────────────────────────── */
function chatToast(x, y, hover) {
  return `<div class="toast" style="left:${x}px;top:${y}px">
    <div class="hd"><div class="ava">Г</div><div><div class="nm">Гена</div><div class="app">рабочий чат · сейчас</div></div></div>
    <div class="msg">держи, вдруг пригодится</div>
    <div class="card" style="${hover ? 'outline:3px solid #9c83ff' : ''}"><span style="width:64px;height:64px;flex:0 0 64px">${NF_LOGO}</span>
      <div><div class="t1">NetForms — Windows Forms for Windows and Linux</div><div class="t2">go-forms.github.io/.NetForms</div></div></div></div>`;
}

/* ─── маркерная доска ───────────────────────────────────────────────────────────────────── */
function whiteboardSvg(items, wipe = 0) {
  // items: [{ x, y, text, k(0..1 — сколько написано), color, size }]
  const o = [`<rect x="-200" y="-200" width="2400" height="1500" fill="#C9CED6"/>`,
    `<rect x="110" y="70" width="1700" height="900" rx="16" fill="#8E949E"/>`,
    `<rect x="128" y="88" width="1664" height="864" rx="8" fill="#FBFCFD"/>`,
    `<path d="M160,120 L620,120 L420,700 L160,700Z" fill="#FFFFFF" opacity=".55"/>`,
    `<rect x="300" y="952" width="1320" height="26" rx="6" fill="#7F858F"/>`,
    `<rect x="1300" y="936" width="90" height="18" rx="6" fill="#2C63C9"/><rect x="1400" y="936" width="90" height="18" rx="6" fill="#D23B3B"/>`];
  o.push(`<clipPath id="wipeC"><rect x="${128 + 1664 * wipe}" y="88" width="1700" height="900"/></clipPath>`);
  o.push(`<g clip-path="url(#wipeC)">`);
  for (const it of items) {
    const n = Math.round((it.text.length) * clamp(it.k));
    if (n <= 0) continue;
    const lines = it.text.slice(0, n).split('\n');
    lines.forEach((ln, i) => o.push(`<text x="${it.x}" y="${it.y + i * (it.size || 96) * 1.05}" font-family="Caveat" font-weight="700" font-size="${it.size || 96}" fill="${it.color || '#1F4FB5'}">${esc(ln)}</text>`));
  }
  o.push('</g>');
  if (wipe > 0 && wipe < 1) {
    o.push(`<rect x="128" y="88" width="${1664 * wipe}" height="864" fill="#C9D3E2" opacity=".18"/>`);
  }
  return o.join('');
}
