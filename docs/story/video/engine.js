// Покадровый движок: renderAt(t) собирает кадр из сцен (scenes.js) и раскладывает его по слоям.
// render.mjs вызывает renderAt для каждого кадра и снимает скриншот; в браузере можно смотреть
// вживую: index.html?t=12.5 — один кадр, index.html?play — проигрывание.

(function () {
  const $ = id => document.getElementById(id);
  const L = {
    world: $('world'), screen: $('screen'), fg: $('fg'), cam: $('cam'), toast: $('toast'), overlay: $('overlay'),
    subs: $('subs'), fade: $('fade'), shake: $('shake'), cursor: $('cursor'),
    wMain: $('wMain'), wDlg: $('wDlg'),
  };
  const last = {};
  const put = (key, el, html) => {
    if (last[key] !== html) { el.innerHTML = html; last[key] = html; }
  };

  const FILM_DIR = 'build/film/';
  const cache = {};
  function preload() {
    const names = Object.keys(TL.film ? TL.film.frames : {});
    return Promise.all(names.map(n => {
      const img = new Image();
      img.src = FILM_DIR + n + '.png';
      cache[n] = img;
      return img.decode().catch(() => {});
    }));
  }

  function setWindow(el, w, pending) {
    if (!w) { el.style.display = 'none'; return; }
    el.style.display = 'block';
    const img = el.querySelector('img');
    const src = FILM_DIR + w.src + '.png';
    if (img.getAttribute('src') !== src) {
      img.setAttribute('src', src);
      pending.push(img.decode().catch(() => {}));
    }
    const meta = TL.film.frames[w.src];
    img.style.width = meta[0] / 2 + 'px';
    img.style.height = meta[1] / 2 + 'px';
    el.querySelector('.wtitle').textContent = w.title || '';
    el.style.transform = `translate(${w.x}px, ${w.y}px) scale(${w.s})`;
    el.style.opacity = w.opacity ?? 1;
    el.style.filter = w.filter || '';
  }

  function subtitles(t, frame) {
    if (frame.noSubs) return '';
    const l = TL.lines.find(l => l.sub && t >= l.t0 - 0.05 && t <= l.t1 + 0.4);
    if (!l) return '';
    const k = Math.min(1, (t - l.t0 + 0.05) / 0.15);
    return `<div class="sub ${l.style === 'whisper' ? 'whisper' : ''}" style="opacity:${k};transform:translateY(${(1 - k) * 10}px)">` +
      `<span class="who" style="color:var(--${l.who})">${l.name}${l.style === 'whisper' ? ' (шёпотом)' : ''}</span>${esc(l.text)}</div>`;
  }

  window.renderAt = async function (t) {
    const sc = TL.scenes.find(s => t >= s.start && t < s.end) || TL.scenes[TL.scenes.length - 1];
    const f = { world: null, view: [960, 540, 1], screen: null, win: null, dlg: null, cursor: null, cam: null, fg: null,
                toast: null, overlay: '', shake: [0, 0, 0], fade: 0, noSubs: false };
    SCENES[sc.id](t, f, sc);

    // мир (SVG): камера — это viewBox, поэтому векторы остаются чёткими при любом зуме
    if (f.world) {
      L.world.style.display = 'block';
      const [cx, cy, z] = f.view;
      const w = 1920 / z, h = 1080 / z;
      L.world.setAttribute('viewBox', `${cx - w / 2} ${cy - h / 2} ${w} ${h}`);
      put('world', L.world, f.world);
      L.world.style.filter = f.worldFilter || '';
    } else {
      L.world.style.display = 'none';
    }
    put('screen', L.screen, f.screen || '');
    L.screen.style.transform = f.screenTransform || '';
    L.screen.style.transformOrigin = f.screenOrigin || '50% 50%';
    L.screen.style.filter = f.screenFilter || '';

    const pending = [];
    setWindow(L.wMain, f.win, pending);
    setWindow(L.wDlg, f.dlg, pending);

    put('fg', L.fg, f.fg || '');
    if (f.cursor) {
      L.cursor.style.display = 'block';
      L.cursor.style.transform = `translate(${f.cursor[0]}px, ${f.cursor[1]}px)`;
      L.cursor.classList.toggle('press', !!f.cursor[2]);
    } else {
      L.cursor.style.display = 'none';
    }
    if (f.cam) {
      L.cam.style.display = 'block';
      put('cam', L.cam, facecam(t, f.cam.expr, f.cam.mood));
      L.cam.style.transform = f.cam.transform || '';
      L.cam.style.opacity = f.cam.opacity ?? 1;
    } else {
      L.cam.style.display = 'none';
    }
    put('toast', L.toast, f.toast || '');
    put('overlay', L.overlay, f.overlay || '');
    put('subs', L.subs, subtitles(t, f));
    const [dx, dy, rot] = f.shake;
    L.shake.style.transform = dx || dy || rot ? `translate(${dx}px, ${dy}px) rotate(${rot}deg)` : '';
    L.fade.style.opacity = f.fade;
    await Promise.all(pending);
  };

  window.ready = (async () => {
    await document.fonts.ready;
    await preload();
    await document.fonts.ready;
    return true;
  })();

  const q = new URLSearchParams(location.search);
  if (q.has('t')) window.ready.then(() => renderAt(parseFloat(q.get('t'))));
  if (q.has('play')) {
    window.ready.then(() => {
      const t0 = performance.now() - (parseFloat(q.get('play')) || 0) * 1000;
      const loop = () => { renderAt((performance.now() - t0) / 1000); requestAnimationFrame(loop); };
      loop();
    });
  }
})();
