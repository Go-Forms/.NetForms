#!/usr/bin/env bash
# «Злой csproj» — сборка ролика с нуля: кадры «Склада» → голоса и хронометраж → звук → кадры → MP4.
# Нужны: .NET 10 SDK, Python 3 с numpy и scipy, Node 22, Chromium (Playwright), ffmpeg с libx264,
# RHVoice с русскими голосами (Ubuntu: apt install rhvoice rhvoice-russian), шрифт Noto Sans (fonts-noto-core).
set -euo pipefail
cd "$(dirname "$0")"
PY=${PYTHON:-python3}
FFMPEG=${FFMPEG:-ffmpeg}
OUT=${OUT:-../angry-csproj.mp4}

# 1. «Склад» — настоящее WinForms-приложение на NetForms; Film.cs снимает его кадры без окна.
dotnet run --project sklad -c Release -- film build/film
# 2. Реплики (RHVoice), паузы, набор текста → build/timeline.{json,js}
$PY story.py
# 3. Звук: голоса, шумы и музыка → build/soundtrack.wav
$PY mix.py
# 4. Кадры: index.html в Chromium, кадр за кадром → build/frames/*.jpg
[ -d node_modules ] || npm install --no-audit --no-fund
rm -rf build/frames
node render.mjs frames
# 5. Видео
"$FFMPEG" -y -loglevel error -framerate 30 -i build/frames/f%05d.jpg -i build/soundtrack.wav \
  -c:v libx264 -preset slow -crf 21 -tune animation -pix_fmt yuv420p -r 30 \
  -c:a aac -b:a 160k -movflags +faststart -shortest "$OUT"
echo "готово: $OUT"
