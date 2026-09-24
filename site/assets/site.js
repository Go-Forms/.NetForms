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
