/* Original assets and game-rendered card faces. Review storage is owned by CardReview.js. */
(() => {
  'use strict';
  const panels = [...document.querySelectorAll('.card-visuals')];
  const data = new WeakMap();
  const rankName = level => level === 0 ? '基础 +0' : `强化 +${level}`;
  function render(panel, refreshLevels) {
    const views = data.get(panel);
    const stage = panel.querySelector('.preview-stage');
    const levels = panel.querySelector('.preview-level');
    const candidates = views.filter(view => view.stage === stage.value);
    if (refreshLevels) {
      const previous = Number(levels.value);
      const selected = candidates.find(view => view.level === previous) || candidates[0];
      levels.replaceChildren(...candidates.map(view => {
        const option = document.createElement('option');
        option.value = String(view.level);
        option.textContent = rankName(view.level);
        return option;
      }));
      levels.value = String(selected.level);
    }
    const selected = candidates.find(view => view.level === Number(levels.value)) || candidates[0];
    if (!selected) throw new Error('Missing game card view');
    const name = panel.closest('.card-block').dataset.cardName;
    const label = `${selected.stage} · ${rankName(selected.level)}`;
    const original = panel.querySelector('.original-art-image');
    const originalLink = panel.querySelector('.original-art-link');
    originalLink.href = selected.originalImage;
    originalLink.dataset.imageTitle = `${name} · ${selected.stage} · 原始卡图`;
    if (original.getAttribute('src') !== selected.originalImage) original.src = selected.originalImage;
    original.width = selected.originalWidth;
    original.height = selected.originalHeight;
    original.alt = `${name} ${selected.stage} 原始卡图`;
    panel.querySelector('.original-art-size').textContent = `${selected.originalWidth} × ${selected.originalHeight} 像素`;
    panel.querySelector('.original-art-caption').textContent = `${selected.stage} · 原始文件，点击查看 1:1 像素`;
    const game = panel.querySelector('.game-card-image');
    const gameLink = panel.querySelector('.game-card-link');
    gameLink.href = selected.image;
    gameLink.dataset.imageTitle = `${name} · ${label} · 游戏卡面`;
    if (game.getAttribute('src') !== selected.image) game.src = selected.image;
    game.width = selected.width;
    game.height = selected.height;
    game.style.setProperty('--game-design-width', `${selected.designWidth}px`);
    game.alt = `${name} ${label} 游戏卡面`;
    panel.querySelector('.game-view-state').textContent = label;
  }
  for (const panel of panels) {
    const views = JSON.parse(panel.dataset.views);
    if (!Array.isArray(views) || !views.length) throw new Error('Missing card comparison data');
    data.set(panel, views);
    panel.querySelector('.preview-stage').addEventListener('change', () => render(panel, true));
    panel.querySelector('.preview-level').addEventListener('change', () => render(panel, false));
  }
  document.getElementById('stage-filter')?.addEventListener('change', event => {
    const selected = event.target.value;
    if (selected === 'all') return;
    for (const panel of panels) {
      if (!data.get(panel).some(view => view.stage === selected)) continue;
      panel.querySelector('.preview-stage').value = selected;
      render(panel, true);
    }
  });

  const dialog = document.getElementById('card-image-dialog');
  const image = document.getElementById('card-image-dialog-image');
  const canvas = document.getElementById('image-view-canvas');
  const fit = document.getElementById('image-view-fit');
  const pixels = document.getElementById('image-view-pixels');
  let returnFocus = null;
  function setMode(pixelMode) {
    canvas.classList.toggle('is-pixels', pixelMode);
    fit.setAttribute('aria-pressed', String(!pixelMode));
    pixels.setAttribute('aria-pressed', String(pixelMode));
    canvas.scrollTop = canvas.scrollLeft = 0;
  }
  fit.addEventListener('click', () => setMode(false));
  pixels.addEventListener('click', () => setMode(true));
  image.addEventListener('load', () => {
    document.getElementById('image-view-size').textContent = `${image.naturalWidth} × ${image.naturalHeight} 像素`;
  });
  document.addEventListener('click', event => {
    const link = event.target.closest('.preview-image-link');
    if (!link || event.ctrlKey || event.metaKey || event.shiftKey || event.altKey || event.button !== 0 || typeof dialog.showModal !== 'function') return;
    event.preventDefault();
    returnFocus = link;
    const title = link.dataset.imageTitle;
    document.getElementById('card-image-dialog-title').textContent = title;
    document.getElementById('image-view-original-link').href = link.href;
    document.getElementById('image-view-size').textContent = '';
    image.alt = title;
    image.src = link.href;
    setMode(false);
    dialog.showModal();
    document.body.classList.add('image-view-open');
  });
  document.getElementById('image-view-close').addEventListener('click', () => dialog.close());
  dialog.addEventListener('click', event => { if (event.target === dialog) dialog.close(); });
  dialog.addEventListener('close', () => {
    document.body.classList.remove('image-view-open');
    image.removeAttribute('src');
    returnFocus?.focus({preventScroll:true});
  });
})();
