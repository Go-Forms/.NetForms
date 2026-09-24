// Renders logo.svg to the PNG icons (NuGet 256, VS Code 128, site favicon 32) with Playwright's Chromium:
//   node eng/branding/render-icons.js eng/branding/logo.svg eng/branding/icon.png:256 eng/branding/icon-128.png:128 eng/branding/favicon-32.png:32
// (with a global playwright: NODE_PATH=$(npm root -g) node ...)
const { chromium } = require('playwright');
const fs = require('fs');
(async () => {
  const [svg, ...outs] = process.argv.slice(2);
  const browser = await chromium.launch();
  const markup = fs.readFileSync(svg, 'utf8');
  for (const o of outs) {
    const [out, size] = o.split(':');
    const page = await browser.newPage({ viewport: { width: +size, height: +size } });
    await page.setContent(`<html><body style="margin:0">${markup.replace('width="256" height="256"', `width="${size}" height="${size}"`)}</body></html>`);
    await page.screenshot({ path: out, omitBackground: true });
    await page.close();
  }
  await browser.close();
})();
