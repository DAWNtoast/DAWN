(() => {
  'use strict';
  const $ = id => document.getElementById(id);
  const people = [...document.querySelectorAll('.crew-block')];
  let port = '';
  function filter() {
    const query = $('search').value.trim().toLocaleLowerCase();
    const profession = $('profession-filter').value;
    let visible = 0, reviewed = 0;
    people.forEach(person => {
      const isReviewed = person.classList.contains('reviewed');
      reviewed += Number(isReviewed);
      person.querySelector('.review-badge').textContent = isReviewed ? '✓ 已审核' : '待审核';
      const match = (!port || person.dataset.port === port) && (!profession || person.dataset.profession === profession) &&
        (!query || person.dataset.search.toLocaleLowerCase().includes(query)) && (!window.CrewReview || window.CrewReview.matches(person));
      person.hidden = !match; visible += Number(match);
    });
    $('visible-count').textContent = `显示 ${visible} / ${people.length} 位`;
    $('progress-bar').style.width = `${reviewed / people.length * 100}%`;
    $('empty-state').hidden = visible > 0;
    document.querySelectorAll('.port-button').forEach(button => {
      const group = people.filter(p => !button.dataset.port || p.dataset.port === button.dataset.port);
      button.querySelector('small').textContent = `${group.filter(p => p.classList.contains('reviewed')).length} / ${group.length}`;
      button.classList.toggle('active', button.dataset.port === port);
      button.setAttribute('aria-pressed', String(button.dataset.port === port));
    });
  }
  document.querySelectorAll('.port-button').forEach(button => button.addEventListener('click', () => {
    port = button.dataset.port; filter(); $('top').scrollIntoView({behavior:'instant',block:'start'});
  }));
  $('search').addEventListener('input',filter);
  $('profession-filter').addEventListener('change',filter);
  $('review-filter').addEventListener('change',filter);
  window.addEventListener('crew-review-change',filter);
  $('reset-filters').addEventListener('click', () => {
    port=''; $('search').value=''; $('profession-filter').value=''; $('review-filter').value='all'; filter();
  });
  const dialog = $('lightbox'), picture = $('lightbox-image'), stage = dialog.querySelector('.lightbox-stage');
  let gallery = [], current = 0, opener = null;
  function fit(native) {stage.classList.toggle('native',native); $('image-native').setAttribute('aria-pressed',String(native)); $('image-fit').setAttribute('aria-pressed',String(!native));}
  function show(index) {
    current = index; const item = gallery[current];
    picture.src = item.dataset.src; picture.alt = item.dataset.title;
    const person = item.closest('.crew-block');
    $('lightbox-title').textContent = `${person?.dataset.crewName || ''} · ${item.dataset.title}  (${current+1}/${gallery.length})`;
    $('image-original').href = item.dataset.src;
    $('image-prev').disabled = current===0; $('image-next').disabled = current===gallery.length-1;
    fit(false); stage.scrollTop=0; stage.scrollLeft=0;
  }
  document.querySelectorAll('.image-open').forEach(button => button.addEventListener('click', () => {
    opener = button; gallery = [...button.closest('.crew-block').querySelectorAll('.image-open')]; show(gallery.indexOf(button));
    dialog.showModal(); document.body.style.overflow='hidden'; $('image-close').focus();
  }));
  function close() { dialog.close(); }
  dialog.addEventListener('close', () => {document.body.style.overflow=''; opener?.focus({preventScroll:true});});
  $('image-close').addEventListener('click',close);
  $('image-fit').addEventListener('click',()=>fit(false));
  $('image-native').addEventListener('click',()=>fit(true));
  $('image-prev').addEventListener('click',()=>{if(current>0)show(current-1);});
  $('image-next').addEventListener('click',()=>{if(current<gallery.length-1)show(current+1);});
  dialog.addEventListener('click',event=>{if(event.target===dialog)close();});
  dialog.addEventListener('keydown',event=>{if(event.key==='ArrowLeft'&&current>0){event.preventDefault();show(current-1);}if(event.key==='ArrowRight'&&current<gallery.length-1){event.preventDefault();show(current+1);}});
  filter();
})();
