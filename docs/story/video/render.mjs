// Покадровая съёмка index.html в Chromium (Playwright).
//   node render.mjs stills 5,12.5,30   — отдельные кадры в build/stills/
//   node render.mjs frames [from] [to] — все кадры ролика в build/frames/ (JPEG), 4 вкладки параллельно
import { chromium } from 'playwright-core';
import fs from 'node:fs';
import path from 'node:path';
import url from 'node:url';

const here = path.dirname(url.fileURLToPath(import.meta.url));
const TL = JSON.parse(fs.readFileSync(path.join(here, 'build/timeline.json'), 'utf8'));
const page0 = url.pathToFileURL(path.join(here, 'index.html')).href;
const exe = process.env.CHROMIUM || (fs.existsSync('/opt/pw-browsers/chromium-1194/chrome-linux/chrome') ? '/opt/pw-browsers/chromium-1194/chrome-linux/chrome' : undefined);

async function open(browser) {
  const ctx = await browser.newContext({ viewport: { width: 1920, height: 1080 }, deviceScaleFactor: 1 });
  const page = await ctx.newPage();
  page.on('pageerror', e => { console.error('pageerror:', e.message); process.exitCode = 1; });
  page.on('console', m => { if (m.type() === 'error') console.error('console:', m.text()); });
  await page.goto(page0);
  await page.evaluate(() => window.ready);
  const cdp = await ctx.newCDPSession(page);
  return { page, cdp };
}

async function shot({ page, cdp }, t, file, quality) {
  await page.evaluate(tt => window.renderAt(tt), t);
  const r = await cdp.send('Page.captureScreenshot', quality
    ? { format: 'jpeg', quality, optimizeForSpeed: true }
    : { format: 'png' });
  fs.writeFileSync(file, Buffer.from(r.data, 'base64'));
}

const [mode, a, b] = process.argv.slice(2);
const browser = await chromium.launch({ executablePath: exe, args: ['--font-render-hinting=none', '--disable-gpu'] });
if (mode === 'stills') {
  const dir = b ? path.resolve(b) : path.join(here, 'build/stills');
  fs.mkdirSync(dir, { recursive: true });
  const tab = await open(browser);
  for (const s of a.split(',')) {
    const t = parseFloat(s);
    await shot(tab, t, path.join(dir, `t${t.toFixed(2).padStart(7, '0')}.png`));
  }
} else {
  const dir = path.join(here, 'build/frames');
  fs.mkdirSync(dir, { recursive: true });
  const total = Math.ceil(TL.duration * TL.fps);
  const from = a ? parseInt(a) : 0, to = b ? parseInt(b) : total;
  const workers = parseInt(process.env.WORKERS || '4');
  let next = from, done = 0;
  const t0 = Date.now();
  await Promise.all(Array.from({ length: workers }, async () => {
    const tab = await open(browser);
    while (next < to) {
      const i = next++;
      await shot(tab, i / TL.fps, path.join(dir, `f${String(i).padStart(5, '0')}.jpg`), 94);
      if (++done % 300 === 0) console.log(`${done}/${to - from} кадров, ${((Date.now() - t0) / 1000).toFixed(0)} с`);
    }
  }));
  console.log(`готово: ${to - from} кадров за ${((Date.now() - t0) / 1000).toFixed(0)} с`);
}
await browser.close();
