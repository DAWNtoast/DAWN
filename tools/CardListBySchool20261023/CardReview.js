/* Offline, per-card review notes. Keep the storage namespace stable across catalogue exports. */
(() => {
  'use strict';
  const FORMAT = 'shattered-sail-card-review';
  const VERSION = 1;
  const PREFIX = 'shattered-sail:card-review:v1:';
  const RECOVERY_PREFIX = 'shattered-sail:card-review-recovery:v1:';
  const byId = new Map([...document.querySelectorAll('.card-block[data-card-id]')].map(card => [card.dataset.cardId, card]));
  const records = new Map(), damaged = new Map(), seenRaw = new Map(), pendingRemote = new Map(), unsaved = new Set();
  const $ = id => document.getElementById(id);
  let storage = null;
  let notice = '';
  let noticeError = false;
  try { storage = window.localStorage; } catch { /* Private or restricted file browsing may disable storage. */ }

  function validRecord(value, expectedId) {
    if (!value || typeof value !== 'object' || Array.isArray(value) ||
        typeof value.id !== 'string' || !/^card_[a-z0-9_]+$/.test(value.id) ||
        (expectedId && value.id !== expectedId) || typeof value.reviewed !== 'boolean' ||
        typeof value.suggestion !== 'string' || typeof value.number !== 'string' || typeof value.name !== 'string' ||
        !(value.updatedAt === null || (typeof value.updatedAt === 'string' && Number.isFinite(Date.parse(value.updatedAt))))) {
      throw new Error('审核记录字段不完整或格式错误');
    }
    return { id:value.id, number:value.number, name:value.name, reviewed:value.reviewed,
      suggestion:value.suggestion, updatedAt:value.updatedAt };
  }
  function blank(id) {
    const card = byId.get(id);
    return { id, number:card?.dataset.cardNumber || '', name:card?.dataset.cardName || '',
      reviewed:false, suggestion:'', updatedAt:null };
  }
  function readStored(key, id) {
    const raw = storage.getItem(key);
    seenRaw.set(key, raw);
    if (raw === null) return null;
    try { return validRecord(JSON.parse(raw), id); }
    catch { damaged.set(key, raw); return null; }
  }
  if (storage) {
    try {
      const keys = Array.from({length:storage.length}, (_, i) => storage.key(i));
      for (const prefix of [PREFIX, RECOVERY_PREFIX]) for (const key of keys) {
        if (!key || !key.startsWith(prefix)) continue;
        const record = readStored(key, key.slice(prefix.length));
        if (record) records.set(record.id, record);
      }
    } catch { storage = null; }
  }

  function mergeRecords(local, incoming) {
    const result = {...local};
    let conflict = false;
    result.reviewed = local.reviewed || incoming.reviewed;
    if (!local.suggestion.trim()) result.suggestion = incoming.suggestion;
    else if (incoming.suggestion.trim() && local.suggestion !== incoming.suggestion && !local.suggestion.includes('\n\n【导入建议】\n' + incoming.suggestion)) {
      result.suggestion = local.suggestion + '\n\n【导入建议】\n' + incoming.suggestion;
      conflict = true;
    }
    result.number = byId.get(local.id)?.dataset.cardNumber || local.number || incoming.number;
    result.name = byId.get(local.id)?.dataset.cardName || local.name || incoming.name;
    result.updatedAt = new Date().toISOString();
    return {record:result, conflict};
  }
  function paint(id) {
    const card = byId.get(id);
    if (!card) return;
    const record = records.get(id) || blank(id);
    const checkbox = card.querySelector('.review-checkbox'), notes = card.querySelector('.review-notes');
    if (checkbox) checkbox.checked = record.reviewed;
    if (notes && notes.value !== record.suggestion) notes.value = record.suggestion;
    card.classList.toggle('reviewed', record.reviewed);
    card.classList.toggle('has-review-notes', !!record.suggestion.trim());
    const printed = card.querySelector('.review-card-print');
    if (printed) printed.textContent = record.suggestion || '暂无修改建议';
  }
  function refreshStatus() {
    let reviewed = 0, notes = 0;
    for (const id of byId.keys()) {
      const record = records.get(id);
      if (record?.reviewed) reviewed++;
      if (record?.suggestion.trim()) notes++;
    }
    if ($('review-progress')) $('review-progress').textContent = `已检查 ${reviewed} / ${byId.size} 张 · ${notes} 张有修改建议`;
    const status = $('review-storage-status');
    if (!status) return;
    const warnings = [];
    if (!storage || unsaved.size) warnings.push('部分修改未能保存到浏览器，请先导出审核记录，暂勿关闭页面。');
    else warnings.push('已自动保存到当前浏览器；刷新或重新打开此页可继续。');
    if (damaged.size) warnings.push(`发现 ${damaged.size} 条异常旧记录，原文已保留；新输入另行保存，导出文件包含原文备份。`);
    status.textContent = (notice ? notice + ' ' : '') + warnings.join(' ');
    status.classList.toggle('error', !storage || unsaved.size > 0 || noticeError);
  }
  function changed() { window.dispatchEvent(new Event('card-review-change')); }

  function persist(id, proposed) {
    let record = {...proposed, updatedAt:new Date().toISOString()};
    if (pendingRemote.has(id)) {
      record = mergeRecords(record, pendingRemote.get(id)).record;
      pendingRemote.delete(id);
      notice = '已合并另一窗口的建议，原有输入已保留。';
    }
    records.set(id, record);
    if (!storage) { unsaved.add(id); paint(id); refreshStatus(); return; }
    try {
      const primary = PREFIX + id;
      // Preserve malformed primary bytes. Continue in a separate recovery key.
      const primaryRaw = storage.getItem(primary);
      if (primaryRaw !== null) {
        try { validRecord(JSON.parse(primaryRaw), id); }
        catch { damaged.set(primary, primaryRaw); }
      }
      const key = damaged.has(primary) ? RECOVERY_PREFIX + id : primary;
      const raw = storage.getItem(key);
      if (raw !== null) {
        let incoming;
        try { incoming = validRecord(JSON.parse(raw), id); }
        catch { damaged.set(key, raw); throw new Error('恢复记录也异常，原文已保留'); }
        if (seenRaw.get(key) !== raw) {
          record = mergeRecords(record, incoming).record;
          records.set(id, record);
          notice = '已合并另一窗口的建议，原有输入已保留。';
        }
      }
      const encoded = JSON.stringify(record);
      storage.setItem(key, encoded);
      seenRaw.set(key, encoded);
      unsaved.delete(id);
    } catch { unsaved.add(id); }
    paint(id);
    refreshStatus();
  }
  function saveCard(card) {
    notice = ''; noticeError = false;
    const id = card.dataset.cardId;
    persist(id, {...(records.get(id) || blank(id)), reviewed:card.querySelector('.review-checkbox').checked,
      suggestion:card.querySelector('.review-notes').value});
  }
  window.CardReview = {
    matches(card) {
      const record = records.get(card.dataset.cardId) || blank(card.dataset.cardId);
      switch ($('review-filter')?.value) {
        case 'pending': return !record.reviewed;
        case 'reviewed': return record.reviewed;
        case 'notes': return !!record.suggestion.trim();
        default: return true;
      }
    }
  };
  for (const [id, card] of byId) {
    paint(id);
    card.querySelector('.review-checkbox')?.addEventListener('change', () => { saveCard(card); changed(); });
    const notes = card.querySelector('.review-notes');
    notes?.addEventListener('input', () => saveCard(card));
    notes?.addEventListener('blur', () => {
      if (pendingRemote.has(id)) saveCard(card);
      changed();
    });
  }

  function exportReview() {
    const all = new Map(records);
    for (const id of byId.keys()) if (!all.has(id)) all.set(id, blank(id));
    const cards = [...all.values()].map(record => ({...record,
      number:byId.get(record.id)?.dataset.cardNumber || record.number,
      name:byId.get(record.id)?.dataset.cardName || record.name}));
    const report = {format:FORMAT, version:VERSION, catalogDate:document.body.dataset.catalogDate || '',
      exportedAt:new Date().toISOString(), summary:{total:byId.size,
        reviewed:cards.filter(c => byId.has(c.id) && c.reviewed).length,
        withSuggestions:cards.filter(c => byId.has(c.id) && c.suggestion.trim()).length}, cards};
    if (damaged.size) report.storageRecovery = [...damaged].map(([key, raw]) => ({key, raw}));
    const url = URL.createObjectURL(new Blob([JSON.stringify(report, null, 2)], {type:'application/json;charset=utf-8'}));
    const link = document.createElement('a');
    link.href = url;
    link.download = '卡牌审核记录-' + new Date().toLocaleDateString('sv-SE') + '.json';
    document.body.appendChild(link); link.click(); link.remove();
    setTimeout(() => URL.revokeObjectURL(url), 1000);
    notice = '审核记录已导出，可把 JSON 文件交给我整理修改。'; noticeError = false; refreshStatus();
  }
  $('export-reviews')?.addEventListener('click', exportReview);
  $('import-reviews')?.addEventListener('click', () => $('import-reviews-file').click());
  $('import-reviews-file')?.addEventListener('change', async event => {
    const input = event.currentTarget, file = input.files?.[0];
    if (!file) return;
    try {
      if (file.size > 10 * 1024 * 1024) throw new Error('文件超过 10 MB，请选择审核记录 JSON');
      const data = JSON.parse((await file.text()).replace(/^\uFEFF/, ''));
      if (data?.format !== FORMAT || data.version !== VERSION || !Array.isArray(data.cards)) throw new Error('不是支持的卡牌审核记录文件');
      const incoming = data.cards.map(row => validRecord(row));
      if (new Set(incoming.map(row => row.id)).size !== incoming.length) throw new Error('文件中存在重复卡牌 ID');
      if (data.storageRecovery !== undefined && (!Array.isArray(data.storageRecovery) || data.storageRecovery.some(row =>
        !row || typeof row.key !== 'string' || typeof row.raw !== 'string'))) throw new Error('异常原文备份格式不正确');
      // Finish validating every row before changing any existing record.
      let conflicts = 0, count = 0;
      for (const row of incoming) {
        if (!row.reviewed && !row.suggestion && !row.updatedAt && !records.has(row.id)) continue;
        const merged = mergeRecords(records.get(row.id) || blank(row.id), row);
        if (merged.conflict) conflicts++;
        persist(row.id, merged.record); count++;
      }
      if (data.storageRecovery) for (const row of data.storageRecovery) {
        // Retain imported diagnostic bytes in memory and subsequent exports, never execute them.
        let key = 'imported:' + row.key;
        while (damaged.has(key) && damaged.get(key) !== row.raw) key = 'imported:' + key;
        damaged.set(key, row.raw);
      }
      notice = `已导入 ${count} 条审核记录` + (conflicts ? `；${conflicts} 条不同建议已追加保留` : '') + '。';
      noticeError = false; refreshStatus(); changed();
    } catch (error) {
      notice = '导入失败，原有记录未更改：' + (error.message || '文件格式错误');
      noticeError = true; refreshStatus();
    } finally { input.value = ''; }
  });
  window.addEventListener('storage', event => {
    if (!event.key) { notice = '其他窗口清除了浏览器记录，当前输入仍在本页，请导出备份。'; noticeError = true; refreshStatus(); return; }
    const prefix = event.key.startsWith(PREFIX) ? PREFIX : event.key.startsWith(RECOVERY_PREFIX) ? RECOVERY_PREFIX : null;
    if (!prefix || event.newValue === null) return;
    const id = event.key.slice(prefix.length);
    try {
      const incoming = validRecord(JSON.parse(event.newValue), id);
      seenRaw.set(event.key, event.newValue);
      const card = byId.get(id);
      if (card && document.activeElement === card.querySelector('.review-notes')) {
        pendingRemote.set(id, incoming);
        notice = '另一窗口更新了正在编辑的卡牌，离开输入框时会合并保留。';
      } else {
        records.set(id, unsaved.has(id) ? mergeRecords(records.get(id) || blank(id), incoming).record : incoming);
        paint(id); changed();
      }
      refreshStatus();
    } catch { damaged.set(event.key, event.newValue); refreshStatus(); }
  });
  window.addEventListener('beforeprint', () => { for (const id of byId.keys()) paint(id); });
  refreshStatus(); changed();
})();