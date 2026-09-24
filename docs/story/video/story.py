"""«Злой csproj»: монтажный лист ролика по docs/story/angry-csproj.md.

Здесь — весь хронометраж: реплики (озвучены RHVoice), паузы, набор текста в терминале и редакторе,
звуковые события и метки, по которым сцены в index.html двигают камеру и персонажей.
Запуск: python story.py → build/timeline.js (для index.html) и build/timeline.json (для mix.py).
"""

import hashlib
import json
import os
import random
import subprocess
import sys
import wave

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
BUILD = os.path.join(HERE, "build")
TTS_DIR = os.path.join(BUILD, "tts")
FPS = 30

# Голоса RHVoice (пакеты rhvoice + rhvoice-russian из Ubuntu). pitch/rate — проценты.
# Только голоса без запрета коммерческого использования: aleksandr-hq — CC BY-SA 4.0,
# aleksandr, irina, elena — LGPL-2.1+ (см. /usr/share/doc/rhvoice-russian/copyright). Yuriy, Mikhail,
# Pavel и другие новые голоса — CC BY-NC-ND 4.0, для ролика проекта не годятся.
VOICES = {
    "lesha": dict(name="ЛЁША", voice="aleksandr-hq", rate=112, pitch=100, pan=0.18),
    "gena": dict(name="ГЕНА", voice="aleksandr", rate=94, pitch=80, pan=-0.05),
    "marina": dict(name="МАРИНА ПЕТРОВНА", voice="irina", rate=100, pitch=93, pan=-0.2),
    "term": dict(name="ГОЛОС ТЕРМИНАЛА", voice="elena", rate=96, pitch=90, pan=0.0),
}


def tts(who, say, style=None):
    """Синтез одной реплики (кэш по тексту и голосу). Возвращает путь к wav 24 кГц моно."""
    v = VOICES[who]
    key = hashlib.sha1(f"{v['voice']}|{v['rate']}|{v['pitch']}|{style}|{say}".encode()).hexdigest()[:16]
    path = os.path.join(TTS_DIR, f"{who}-{key}.wav")
    if not os.path.exists(path):
        os.makedirs(TTS_DIR, exist_ok=True)
        subprocess.run(["RHVoice-test", "-p", v["voice"], "-r", str(v["rate"]), "-t", str(v["pitch"]), "-o", path],
                       input=say.encode(), check=True, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    return path


def read_wav(path):
    with wave.open(path) as w:
        sr = w.getframerate()
        x = np.frombuffer(w.readframes(w.getnframes()), dtype=np.int16).astype(np.float32) / 32768
    return sr, x


def trim(x, sr, thresh=0.01):
    """Отрезать тишину по краям (RHVoice оставляет ~0.2 с), оставив 30 мс."""
    idx = np.where(np.abs(x) > thresh)[0]
    if len(idx) == 0:
        return x
    pad = int(0.03 * sr)
    return x[max(0, idx[0] - pad): min(len(x), idx[-1] + pad)]


class Story:
    def __init__(self):
        self.t = 0.0
        self.marks = {}
        self.lines = []
        self.sfx = []
        self.typing = {}
        self.scenes = []
        self.rng = random.Random(47)

    # --- время ---------------------------------------------------------------------------------
    def mark(self, name, dt=0.0):
        self.marks[name] = round(self.t + dt, 3)
        return self.marks[name]

    def wait(self, s):
        self.t += s

    def scene(self, sid):
        if self.scenes:
            self.scenes[-1]["end"] = round(self.t, 3)
        self.scenes.append({"id": sid, "start": round(self.t, 3)})
        self.mark(sid + ".start")

    def fx(self, name, dt=0.0, gain=1.0, **kw):
        self.sfx.append(dict(name=name, t=round(self.t + dt, 3), gain=gain, **kw))

    # --- реплики -------------------------------------------------------------------------------
    def say(self, who, text, say=None, gap=0.25, style=None, sub=True, lid=None):
        path = tts(who, say or text, style)
        sr, x = read_wav(path)
        x = trim(x, sr)
        dur = len(x) / sr
        # огибающая для движения губ: RMS по кадрам ролика
        hop = sr // FPS
        env = [float(np.sqrt(np.mean(x[i:i + hop] ** 2))) for i in range(0, len(x), hop)]
        peak = max(env) or 1.0
        env = [round(min(1.0, e / peak * 1.4), 2) for e in env]
        line = dict(id=lid or f"l{len(self.lines)}", who=who, name=VOICES[who]["name"], text=text,
                    t0=round(self.t, 3), t1=round(self.t + dur, 3), wav=os.path.relpath(path, HERE),
                    style=style, sub=sub, env=env)
        self.lines.append(line)
        if lid:
            self.marks[lid] = line["t0"]
            self.marks[lid + ".end"] = line["t1"]
        self.t += dur + gap
        return line

    # --- набор текста --------------------------------------------------------------------------
    def type(self, tid, text, cps=24.0, jitter=0.35, sound="key", gain=(0.8, 0.8), enter=True, lead=0.15):
        """Посимвольный набор: время каждого символа, звук каждой клавиши, Enter в конце."""
        self.t += lead
        times = []
        n = len(text)
        for i, ch in enumerate(text):
            times.append(round(self.t, 3))
            g = gain[0] + (gain[1] - gain[0]) * (i / max(1, n - 1))
            if not ch.isspace() or sound != "key":
                self.fx(sound, gain=g, i=i)
            else:
                self.fx("space", gain=g)
            step = 1.0 / cps * (1 + self.rng.uniform(-jitter, jitter))
            if ch in " -./" and sound == "key":
                step *= 1.3
            self.t += step
        self.typing[tid] = dict(t0=times[0] if times else self.t, times=times, text=text)
        if enter:
            self.t += 0.12
            self.fx("enter" if sound == "key" else sound, gain=max(gain) * (1.1 if sound == "key" else 1.0))
            self.marks[tid + ".enter"] = round(self.t, 3)
            self.t += 0.1
        else:
            self.marks[tid + ".done"] = round(self.t, 3)
        return times

    def click(self, name, dt=0.0, gain=0.9):
        self.fx("click", dt=dt, gain=gain)
        return self.mark(name, dt)


def build():
    S = Story()

    # ── 0. Заставка ────────────────────────────────────────────────────────────────────────────
    S.scene("s0")
    S.wait(0.5)
    S.type("s0.pre", "NetForms представляет", cps=19, gain=(0.35, 0.35), enter=False)
    S.wait(0.45)
    S.mark("s0.title")
    S.fx("boom")
    S.wait(2.6)

    # ── 1. Офис, понедельник, утро ─────────────────────────────────────────────────────────────
    S.scene("s1")
    S.wait(0.35)
    S.mark("s1.titr")
    S.fx("typewriter", dur=1.6)
    S.wait(3.4)
    S.mark("s1.pull")
    S.fx("cart", dur=4.4)
    S.fx("steps", dur=4.3, gain=0.6)
    S.wait(4.4)
    S.mark("s1.cartStop")
    S.fx("thunk", dt=0.55)
    S.wait(0.95)
    S.say("gena", "Почта есть, браузер есть, таблицы есть. Всё работает.", lid="s1.g1")
    S.say("marina", "А «Склад»?", say="А склад?", lid="s1.m1")
    S.mark("s1.pause")
    S.wait(1.25)
    S.mark("s1.turn")
    S.wait(0.9)
    S.say("gena", "А «Склад» у нас кто?", say="А склад у нас кто?", lid="s1.g2")
    S.mark("s1.monitor")
    S.wait(1.9)
    S.say("lesha", "Это WinForms. Сорок три формы. Там один DataGridView на весь экран, Марина Петровна им живёт.",
          say="Это вин формс. Сорок три формы. Там один дата грид вью на весь экран, Марина Петровна им живёт.",
          lid="s1.l1")
    S.say("marina", "Живу.", lid="s1.m2", gap=0.55)
    S.say("lesha", "Ну… это же .NET. .NET теперь кроссплатформенный. Соберу.",
          say="Ну... это же дотнет. Дотнет теперь кроссплатформенный. Соберу.", lid="s1.l2", gap=0.2)
    S.say("gena", "Ну собери.", lid="s1.g3")
    S.fx("steps", dur=2.4, gain=0.5)
    S.fx("cart", dt=0.2, dur=2.4)
    S.wait(1.6)
    S.fx("whoosh")

    # ── 2. Рабочее место Лёши, день ────────────────────────────────────────────────────────────
    S.scene("s2")
    S.fx("crack", dt=0.7)
    S.wait(1.7)
    S.mark("s2.term")
    S.type("s2.c1", "git clone https://git.company.local/sklad.git && cd sklad", cps=34, lead=0.3)
    S.wait(0.45)
    S.type("s2.c2", "dotnet build", cps=22)
    S.wait(0.8)
    S.mark("s2.err1")
    S.fx("error")
    S.wait(0.5)
    S.say("term", "Чтобы собрать проект для Windows на этой операционной системе…",
          say="Чтобы собрать проект для виндоус на этой операционной системе...", lid="s2.t1", style="robot")
    S.say("lesha", "А, флажок. Они сами подсказывают.", lid="s2.l1", gap=0.1)
    S.type("s2.c3", "dotnet build -p:EnableWindowsTargeting=true", cps=34)
    S.wait(1.1)
    S.mark("s2.ok")
    S.fx("success")
    S.wait(0.7)
    S.mark("s2.lean")
    S.fx("creak", dt=0.1)
    S.fx("sip", dt=1.15)
    S.wait(2.3)
    S.mark("s2.term2")
    S.type("s2.c4", "dotnet run -p:EnableWindowsTargeting=true", cps=34, lead=0.2)
    S.wait(0.8)
    S.mark("s2.err2")
    S.fx("error")
    S.wait(0.9)
    S.mark("s2.freeze")
    S.fx("scratch")
    S.wait(1.5)
    S.mark("s2.term3")
    S.say("term", "Фреймворк Microsoft точка WindowsDesktop точка App. Не найдено ни одного фреймворка.",
          say="Фреймворк майкрософт точка виндоус десктоп точка апп. Не найдено ни одного фреймворка.",
          lid="s2.t2", style="robot")
    S.say("lesha", "Ну так поставь.", lid="s2.l2", gap=0.1)
    S.click("s2.click", dt=0.55)
    S.wait(0.75)
    S.mark("s2.page")
    S.wait(1.9)
    S.say("lesha", "Для Linux нет.", say="Для линукса нет.", lid="s2.l3", style="whisper")
    S.wait(1.1)

    # ── 3. Монтаж «Пять стадий» ────────────────────────────────────────────────────────────────
    S.scene("s3")
    S.mark("s3.denial")
    for i in range(3):
        S.type(f"s3.r{i}", "dotnet run", cps=26, lead=0.12)
        S.wait(0.1)
        S.mark(f"s3.r{i}.out")
        S.fx("error", gain=0.5)
        S.wait(0.45)
    S.wait(0.35)
    S.mark("s3.anger")
    for i in range(14):
        S.fx("click", dt=0.15 + i * 0.22, gain=0.55)
    S.wait(3.9)
    S.mark("s3.bargain")
    S.wait(0.3)
    for i, dur in enumerate([0.8, 0.8, 2.4]):
        S.mark(f"s3.b{i}")
        S.fx("marker", dur=dur)
        S.wait(dur + 0.6)
    S.wait(0.5)
    S.mark("s3.depr")
    S.wait(0.25)
    for i, dur in enumerate([2.0, 1.4]):
        S.mark(f"s3.d{i}")
        S.fx("marker", dur=dur)
        S.wait(dur + 0.35)
    S.wait(0.2)
    S.mark("s3.wipe")
    S.fx("wipe")
    S.wait(1.7)
    S.mark("s3.accept")
    S.wait(3.3)

    # ── 4. Ночь ────────────────────────────────────────────────────────────────────────────────
    S.scene("s4")
    S.wait(1.1)
    S.say("lesha", "Значит, -windows.", say="Значит, минус виндоус.", lid="s4.z1", gap=0.3, style="teeth")
    S.say("lesha", "Значит, UseWindowsForms.", say="Значит, юз виндоус формс.", lid="s4.z2", style="teeth")
    S.wait(0.7)
    S.mark("s4.chat")
    S.fx("notify")
    S.wait(1.9)
    S.click("s4.link")
    S.wait(0.35)
    S.mark("s4.page")
    S.wait(0.8)
    S.say("lesha", "Те же пространства имён.", lid="s4.r1", gap=0.2, sub=False)
    S.say("lesha", "Тот же Application.Run.", say="Тот же эппликейшн ран.", lid="s4.r2", gap=0.2, sub=False)
    S.say("lesha", "Тот же InitializeComponent.", say="Тот же инишиалайз компонент.", lid="s4.r3", gap=0.2, sub=False)
    S.say("lesha", "Ваш код компилируется без изменений…", say="Ваш код компилируется без изменений...",
          lid="s4.r4", sub=False)
    S.wait(1.1)
    S.say("lesha", "То есть… менять надо только это?", say="То есть... менять надо только это?", lid="s4.l1")
    S.wait(0.2)
    S.mark("s4.edit")
    S.wait(0.7)
    # «-windows» — один удар по Backspace
    S.mark("s4.bs")
    S.fx("slam", gain=0.55)
    S.wait(0.55)
    # 8 → 10
    S.type("s4.ten", "⌫10", cps=6, jitter=0.1, sound="slam", gain=(0.6, 0.66), enter=False, lead=0.0)
    S.wait(0.3)
    # строка UseWindowsForms: удар, удар, удар
    for i in range(3):
        S.mark(f"s4.del{i}")
        S.fx("slam", gain=0.68 + 0.04 * i)
        S.wait(0.42)
    S.wait(0.2)
    S.type("s4.add", S_ADD, cps=22, jitter=0.2, sound="slam", gain=(0.72, 1.0), enter=False, lead=0.0)
    S.wait(0.35)
    S.mark("s4.save")
    S.fx("safe")
    S.wait(1.7)
    S.say("lesha", "Ну давай. Скажи мне ещё что-нибудь.", lid="s4.l2", gap=0.3)
    S.mark("s4.run")
    S.type("s4.cr", "dotnet run", cps=9, jitter=0.15, lead=0.35)
    S.mark("s4.silence")
    S.wait(2.9)
    S.mark("s4.window")
    S.fx("open")
    S.wait(1.7)
    S.say("lesha", "Сорок три формы.", lid="s4.l3", gap=0.15)
    S.mark("s4.tabs")
    for i in range(6):
        S.click(f"s4.tab{i}", dt=0.45 + i * 0.52, gain=0.8)
    S.wait(3.6)
    S.say("lesha", "Я не поменял ни одной строчки кода.", lid="s4.l4")
    S.wait(1.5)

    # ── 5. Утро ────────────────────────────────────────────────────────────────────────────────
    S.scene("s5")
    S.mark("s5.insert")          # врезка: экран ноутбука, «ssssss…»
    S.wait(1.8)
    S.mark("s5.genaIn")
    S.fx("steps", dur=2.5)
    S.wait(2.8)
    S.mark("s5.look")            # врезка: второй монитор, «Склад» на Linux
    S.wait(1.6)
    S.say("gena", "Хм.", say="Хм.", lid="s5.g1", gap=0.4)
    S.mark("s5.roll")
    S.fx("cart", dt=1.0, dur=2.6)
    S.fx("steps", dt=1.0, dur=2.4, gain=0.6)
    S.fx("thunk", dt=3.9, gain=0.5)
    S.wait(4.2)
    S.say("gena", "Марина Петровна, проверьте.", lid="s5.g2", gap=0.3)
    S.mark("s5.check")
    S.wait(0.9)
    S.click("s5.prihod")
    S.wait(0.45)
    S.mark("s5.dialog")
    S.wait(0.6)
    S.click("s5.search", gain=0.6)
    S.type("s5.find", "бумага", cps=6.5, jitter=0.3, gain=(0.55, 0.55), enter=False, lead=0.3)
    S.wait(0.35)
    S.click("s5.row", gain=0.6)
    S.wait(0.55)
    S.click("s5.qty", gain=0.6)
    S.type("s5.num", "40", cps=5, gain=(0.55, 0.55), enter=False, lead=0.3)
    S.wait(0.45)
    S.mark("s5.f2")
    S.fx("enter", gain=0.9)
    S.wait(1.6)
    S.mark("s5.two")
    S.say("marina", "А что изменилось?", lid="s5.m1", gap=0.3)
    S.say("gena", "Ничего.", lid="s5.g3", gap=0.35)
    S.say("marina", "Тогда хорошо.", lid="s5.m2", gap=0.3)
    S.mark("s5.coffee")
    S.fx("steps", dt=0.3, dur=2.0)
    S.fx("mug", dt=2.75)
    S.wait(4.0)

    # ── 6. Эпилог ──────────────────────────────────────────────────────────────────────────────
    S.scene("s6")
    S.wait(0.6)
    S.type("s6.c1", "git diff --stat", cps=16)
    S.wait(0.35)
    S.mark("s6.o1")
    S.wait(0.5)
    S.say("term", "Один файл изменён.", lid="s6.t1", style="robot", gap=0.4)
    S.mark("s6.titr1")
    S.fx("boom", gain=0.5)
    S.wait(3.6)
    S.mark("s6.calm")
    S.type("s6.c2", "dotnet tool install -g NetForms.Convert --prerelease", cps=24, gain=(0.4, 0.4), lead=0.4)
    S.wait(0.7)
    S.type("s6.c3", "netforms-convert Sklad.csproj --apply", cps=24, gain=(0.4, 0.4))
    S.wait(0.25)
    S.mark("s6.o3")
    S.wait(3.0)
    S.mark("s6.titr2")
    S.wait(2.7)
    S.mark("s6.logo")
    S.fx("logo")
    S.wait(3.4)
    S.mark("s6.final")
    S.wait(4.2)
    S.mark("s6.fade")
    S.wait(1.2)
    S.scenes[-1]["end"] = round(S.t, 3)
    S.mark("end")

    return S


# Что Лёша вбивает в Sklad.csproj (сцена 4): ровно диф из сценария, посимвольно.
S_ADD = ('<ItemGroup>\n<PackageReference Include="NetForms" Version="0.1.0-preview.1" />\n'
         '<Using Include="System.Drawing" />\n<Using Include="System.Windows.Forms" />\n</ItemGroup>\n\n')


def film_meta():
    """Кадры «Склада» (build/film, их снимает sklad/Film.cs): размеры PNG и геометрия контролов."""
    import struct
    d = os.path.join(BUILD, "film")
    if not os.path.isdir(d):
        return None
    frames = {}
    for n in sorted(os.listdir(d)):
        if n.endswith(".png"):
            with open(os.path.join(d, n), "rb") as f:
                head = f.read(24)
            frames[n[:-4]] = list(struct.unpack(">II", head[16:24]))
    with open(os.path.join(d, "film.json")) as f:
        meta = json.load(f)
    return dict(frames=frames, meta=meta)


def main():
    S = build()
    data = dict(fps=FPS, duration=round(S.t, 3), scenes=S.scenes, marks=S.marks, lines=S.lines,
                sfx=S.sfx, typing=S.typing, film=film_meta())
    os.makedirs(BUILD, exist_ok=True)
    with open(os.path.join(BUILD, "timeline.json"), "w") as f:
        json.dump(data, f, ensure_ascii=False)
    with open(os.path.join(BUILD, "timeline.js"), "w") as f:
        f.write("window.TL = " + json.dumps(data, ensure_ascii=False) + ";\n")
    print(f"длительность {S.t:.1f} с; реплик {len(S.lines)}, звуков {len(S.sfx)}")
    for sc in S.scenes:
        print(f"  {sc['id']}: {sc['start']:7.2f} → {sc['end']:7.2f}  ({sc['end'] - sc['start']:.1f} с)")
    if "-v" in sys.argv:
        for l in S.lines:
            print(f"  {l['t0']:7.2f} {l['t1'] - l['t0']:5.2f} {l['who']:6} {l['text']}")


if __name__ == "__main__":
    main()
