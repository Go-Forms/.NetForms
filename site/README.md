# site/

The NetForms website: `index.html` (English), `ru/index.html` (Russian), shared `assets/`. Static files,
no build step; `.github/workflows/pages.yml` publishes the folder to GitHub Pages on every change in `main`.

Preview locally: `npx http-server site` (or open `index.html`).

The pictures in `assets/` are copies of `docs/images/` (real renders and the designer's UI-test screenshots)
and of `eng/branding/`. The numbers in the "Status" section come from `docs/compatibility.md` — update both
pages together with it on each release.
