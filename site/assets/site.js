// Copy buttons: each copies the text of the <pre> right before it. When the clipboard is refused,
// the text is selected instead so Ctrl+C works.
document.querySelectorAll('button.copy').forEach((button) => {
  const label = button.textContent;
  button.addEventListener('click', async () => {
    const pre = button.previousElementSibling;
    const text = pre.textContent.split('\n').filter((l) => !l.startsWith('#')).join('\n').trim();
    try {
      await navigator.clipboard.writeText(text);
      button.textContent = button.dataset.done || 'Copied';
    } catch {
      const range = document.createRange();
      range.selectNodeContents(pre);
      const selection = window.getSelection();
      selection.removeAllRanges();
      selection.addRange(range);
      button.textContent = button.dataset.select || 'Selected — press Ctrl+C';
    }
    setTimeout(() => { button.textContent = label; }, 2000);
  });
});

// The EN/RU switch: remember the choice, so index.html stops sending a Russian-language browser to ru/.
document.querySelectorAll('[data-lang-choice]').forEach((link) => {
  link.addEventListener('click', () => {
    try { localStorage.setItem('netforms-lang', link.dataset.langChoice); } catch { /* storage blocked: no memory, no harm */ }
  });
});

// Documentation pages on a phone: the contents list starts folded, so the page itself is on the first screen.
if (window.matchMedia('(max-width: 760px)').matches) {
  document.querySelectorAll('.docs-nav details[open]').forEach((d) => d.removeAttribute('open'));
}
