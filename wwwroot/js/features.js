/* Orbis Web Manager v2.2 features: saves, updates, GoldHEN, stats, batch covers, search, PWA…
   Loaded after app.js and reuses its helpers (api, postJson, esc, showToast, formatBytes, openModal…). */

const fx = { updates: {}, users: [], saves: null, selectCaptures: false, selected: new Set(), ghConfigChanges: {}, plugins: null, nowTimer: null };

function onFeatureTab(tab) {
  if (tab === 'saves') loadSaves();
  if (tab === 'stats') { loadHistory(); loadStorage(false); }
  if (tab === 'goldhen') loadGoldhenSub(document.querySelector('#pane-goldhen .subtab.active').dataset.sub);
  if (tab === 'dashboard') { loadActivity(); loadNowPlaying(); }
  if (tab === 'settings') loadSettingsExtra();
  if (tab !== 'media' && fx.selectCaptures) toggleCaptureSelect(false);
}

function confirmBox(msg) { return window.confirm(msg); }
const fmtMin = (m) => fmtPlaytime(m || 0);

async function downloadPost(url, body, fallbackName) {
  const res = await fetch(url, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body) });
  if (!res.ok) {
    let msg = `Erro ${res.status}`;
    try { msg = (await res.json()).error || msg; } catch (e) { }
    throw new Error(msg);
  }
  const blob = await res.blob();
  const cd = res.headers.get('content-disposition') || '';
  const m = /filename="([^"]+)"/.exec(cd);
  const a = document.createElement('a');
  a.href = URL.createObjectURL(blob);
  a.download = m ? m[1] : fallbackName;
  document.body.appendChild(a);
  a.click();
  setTimeout(() => { URL.revokeObjectURL(a.href); a.remove(); }, 2000);
}

// ======================================================= DASHBOARD: activity, now playing, app update
const ACT_ICONS = { save: '#i-save', cover: '#i-image', pkg: '#i-package', notify: '#i-bell', payload: '#i-bolt', goldhen: '#i-sliders', cheat: '#i-bolt', download: '#i-download', uninstall: '#i-trash', limit: '#i-clock', auth: '#i-lock', capture: '#i-image' };

async function loadActivity() {
  const box = document.getElementById('activity-list');
  try {
    const list = await api('/api/activity', { timeout: 10000 });
    if (!list.length) { box.innerHTML = '<li class="text-muted text-sm">Nada ainda. Trocas de capa, envios de PKG, backups e outras ações aparecem aqui.</li>'; return; }
    box.innerHTML = list.slice(0, 12).map(a => {
      const d = new Date(a.at);
      const when = isNaN(d) ? '' : d.toLocaleString('pt-BR', { day: '2-digit', month: '2-digit', hour: '2-digit', minute: '2-digit' });
      return `<li><svg aria-hidden="true"><use href="${ACT_ICONS[a.kind] || '#i-bell'}"/></svg><span class="grow">${esc(a.text)}</span><time>${esc(when)}</time></li>`;
    }).join('');
  } catch (e) { /* server busy */ }
}

async function loadNowPlaying() {
  clearTimeout(fx.nowTimer);
  const card = document.getElementById('now-playing');
  try {
    const n = await api('/api/nowplaying', { timeout: 15000 });
    if (n.playing) {
      card.hidden = false;
      const img = document.getElementById('np-icon');
      img.dataset.icon = n.id;
      if (img.getAttribute('src') !== n.iconUrl) img.src = n.iconUrl;
      document.getElementById('np-name').textContent = n.name;
      document.getElementById('np-sub').textContent = `desde ${n.since} · ${fmtMin(n.minutes)} nesta sessão`;
    } else card.hidden = true;
  } catch (e) { card.hidden = true; }
  if (state.currentTab === 'dashboard') fx.nowTimer = setTimeout(loadNowPlaying, 30000);
}

async function checkAppUpdate() {
  try {
    const u = await api('/api/app/update', { timeout: 20000 });
    fx.appUpdate = u;
    if (u.hasUpdate) {
      document.getElementById('app-update-box').hidden = false;
      document.getElementById('app-update-ver').textContent = 'v' + u.latest;
      document.getElementById('app-update-link').href = u.url;
    }
  } catch (e) { }
}

// ======================================================= GAME UPDATES (PSN)
async function loadUpdates(refresh) {
  const box = document.getElementById('updates-box');
  const btn = document.getElementById('btn-check-updates');
  try {
    let u = await api('/api/updates' + (refresh ? '?refresh=1' : ''), { timeout: 30000 });
    if (refresh || u.checking) {
      btn.disabled = true;
      box.hidden = false;
      box.innerHTML = '<span class="spinner small"></span> Consultando a PSN para cada jogo…';
      while (u.checking) {
        await new Promise(r => setTimeout(r, 2000));
        u = await api('/api/updates', { timeout: 30000 });
      }
      btn.disabled = false;
    }
    fx.updates = {};
    u.games.forEach(g => { if (g.hasUpdate) fx.updates[g.id] = g; });
    const avail = u.games.filter(g => g.hasUpdate);
    if (!u.everChecked) { box.hidden = true; return; }
    box.hidden = false;
    if (!avail.length) {
      box.innerHTML = `<strong>Todos os jogos estão na versão mais recente.</strong> <span class="text-muted">Verificado ${esc(u.lastRun || 'antes')}.</span>`;
    } else {
      const blocked = avail.filter(g => g.needsNewerFw).length;
      box.innerHTML = `<div class="updates-head"><strong>${avail.length} ${avail.length === 1 ? 'jogo tem' : 'jogos têm'} atualização na PSN</strong><span class="text-muted">Baixe pela tela do jogo no PS4 (Options › Verificar atualização).${blocked ? ` ${blocked} pede${blocked > 1 ? 'm' : ''} um firmware maior que o configurado (${esc((state.config && state.config.ps4_fw) || '?')}).` : ''}</span></div>
        <div class="updates-list">${avail.map(g => `
          <button class="update-row" data-action="open-game" data-id="${esc(g.id)}">
            <img src="${esc(g.iconUrl)}" alt="" data-icon="${esc(g.id)}" loading="lazy">
            <span class="grow"><strong>${esc(g.name)}</strong><small>v${esc(g.installed)} → v${esc(g.latest)} · ${formatBytes(g.size)}</small>
              ${g.needsNewerFw ? `<small class="text-warning">Pede firmware ${esc(g.requiresFw)} ou maior</small>` : ''}</span>
          </button>`).join('')}</div>`;
    }
    applyGamesView();
  } catch (e) {
    btn.disabled = false;
    if (refresh) showToast('Não foi possível verificar: ' + e.message, 'error');
  }
}

// add "UPDATE" badges to the library cards
const _renderGames = renderGames;
renderGames = function (games) {
  _renderGames(games);
  Object.keys(fx.updates).forEach(id => {
    const card = document.querySelector(`#games-container .game-card[data-id="${CSS.escape(id)}"] .game-cover-box`);
    if (card && !card.querySelector('.game-badge-update')) card.insertAdjacentHTML('beforeend', '<span class="game-badge-update" title="Atualização disponível na PSN">UPDATE</span>');
  });
};

// ======================================================= GAME DETAILS: update, DLC, patches, uninstall
const _openGameDetails = openGameDetails;
openGameDetails = async function (id) {
  const extra = document.getElementById('gd-extra');
  extra.innerHTML = '';
  const p = _openGameDetails(id);
  const upd = fx.updates[id];
  extra.innerHTML = `
    ${upd ? `<div class="info-box"><strong>Atualização disponível:</strong> v${esc(upd.installed)} → v${esc(upd.latest)} (${formatBytes(upd.size)}). No PS4, aperte Options no jogo › Verificar atualização.</div>` : ''}
    <div class="gd-section"><h4>DLCs instaladas</h4><div id="gd-dlc" class="text-sm text-muted"><span class="spinner small"></span> Lendo…</div></div>
    <div class="gd-section gd-danger">
      <h4>Desinstalar</h4>
      <p class="text-sm text-muted">Usa o Remote Package Installer (precisa estar aberto no PS4). Os saves continuam no console.</p>
      <div class="row">
        <button class="btn btn-outline btn-sm" data-fx="uninstall" data-what="patch" data-id="${esc(id)}"><svg><use href="#i-trash"/></svg>Só a atualização</button>
        <button class="btn btn-danger btn-sm" data-fx="uninstall" data-what="game" data-id="${esc(id)}"><svg><use href="#i-trash"/></svg>Desinstalar jogo</button>
      </div>
    </div>`;
  await p;
  try {
    const u = await api(`/api/game/dlc?id=${encodeURIComponent(id)}`, { timeout: 60000 });
    const box = document.getElementById('gd-dlc');
    if (!box || state.detailId !== id) return;
    box.innerHTML = u.dlcs && u.dlcs.length
      ? `<ul class="dlc-list">${u.dlcs.map(d => `<li><span class="grow">${esc(d.name)}</span><span class="mono">${formatBytes(d.size)}</span></li>`).join('')}</ul>`
      : 'Nenhuma DLC instalada.';
  } catch (e) {
    const box = document.getElementById('gd-dlc');
    if (box) box.textContent = 'Não foi possível ler as DLCs.';
  }
};

async function uninstall(id, what) {
  const g = state.games.find(x => x.id === id);
  const name = g ? g.name : id;
  const msg = what === 'patch'
    ? `Desinstalar a atualização de "${name}"? O jogo volta para a versão do disco/base.`
    : `Desinstalar "${name}" do PS4? Os dados do jogo serão apagados do HD (os saves ficam). Isso não pode ser desfeito.`;
  if (!confirmBox(msg)) return;
  try {
    await postJson(`/api/game/uninstall?id=${encodeURIComponent(id)}&what=${what}`, {});
    showToast(what === 'patch' ? 'Atualização desinstalada.' : `"${name}" foi desinstalado.`, 'success');
    if (what !== 'patch') { closeModal('modal-game'); setTimeout(() => fetchGames('refresh'), 1500); }
  } catch (e) { showToast(e.message, 'error', 9000); }
}

// ======================================================= DOWNLOADS: pause / resume / cancel
const _downloadCard = downloadCard;
downloadCard = function (t) {
  const html = _downloadCard(t);
  if (t.state !== 'downloading' && t.state !== 'paused') return html;
  const ctl = `<div class="dl-ctl">
    ${t.state === 'paused'
      ? `<button class="btn btn-ghost btn-sm" data-fx="dl" data-act="resume" data-task="${esc(t.taskId)}" title="Retomar"><svg><use href="#i-play"/></svg>Retomar</button>`
      : `<button class="btn btn-ghost btn-sm" data-fx="dl" data-act="pause" data-task="${esc(t.taskId)}" title="Pausar"><svg><use href="#i-pause"/></svg>Pausar</button>`}
    <button class="btn btn-ghost btn-sm" data-fx="dl" data-act="cancel" data-task="${esc(t.taskId)}" data-name="${esc(t.name)}" title="Cancelar"><svg><use href="#i-x"/></svg>Cancelar</button>
  </div>`;
  return html.replace(/<\/article>\s*$/, ctl + '</article>');
};

async function downloadControl(task, act, name) {
  if (act === 'cancel' && !confirmBox(`Cancelar o download de "${name}"? O que já foi baixado será descartado.`)) return;
  try {
    await postJson(`/api/downloads/control?task=${encodeURIComponent(task)}&action=${act}`, {});
    showToast(act === 'pause' ? 'Download pausado.' : act === 'resume' ? 'Download retomado.' : 'Download cancelado.', 'success');
    setTimeout(() => loadDownloads(), 1500);
  } catch (e) {
    showToast(e.message + (/12800|Remote Package/i.test(e.message) ? '' : ' (abra o Remote Package Installer no PS4)'), 'error', 9000);
  }
}

// ======================================================= PKGs: analysis + PC folder details
async function analyzePkgs() {
  const btn = document.getElementById('btn-analyze-pkgs');
  const box = document.getElementById('pkg-analyze-box');
  btn.disabled = true;
  box.innerHTML = '<span class="spinner small"></span> Lendo o cabeçalho de cada PKG no PS4…';
  try {
    const list = await api('/api/pkgs/analyze', { timeout: 300000 });
    const removable = list.filter(p => p.canDelete);
    const freed = removable.reduce((s, p) => s + p.size, 0);
    box.innerHTML = removable.length
      ? `<span><strong>${removable.length} PKG${removable.length > 1 ? 's' : ''} já instalado${removable.length > 1 ? 's' : ''}</strong> ocupando ${formatBytes(freed)}. Dá para apagar sem perder nada.</span>
         <button class="btn btn-danger btn-sm" id="btn-clean-pkgs"><svg><use href="#i-trash"/></svg>Apagar os ${removable.length}</button>`
      : '<span><strong>Nada para limpar.</strong> Nenhum PKG em /data/pkg já está instalado.</span>';
    const c = document.getElementById('pkgs-container');
    c.innerHTML = list.length ? list.map(p => pkgCardHtml(p, true)).join('') : '<div class="empty-state">Nenhum .pkg em /data/pkg/.</div>';
    const clean = document.getElementById('btn-clean-pkgs');
    if (clean) clean.onclick = async () => {
      if (!confirmBox(`Apagar ${removable.length} PKG(s) já instalados de /data/pkg/ e liberar ${formatBytes(freed)}?`)) return;
      let ok = 0;
      for (const p of removable) {
        try { await postJson(`/api/files/delete?path=${encodeURIComponent(p.path)}&isDir=false`, {}); ok++; } catch (e) { }
      }
      showToast(`${ok} PKG(s) apagados.`, 'success');
      fetchSummary(true);
      analyzePkgs();
    };
  } catch (e) {
    box.innerHTML = `<span class="text-danger">${esc(e.message)}</span>`;
  } finally { btn.disabled = false; }
}

const PKG_STATUS = {
  new: ['badge success', 'Não instalado'], upgrade: ['badge cheat', 'Atualiza'], installed: ['badge outline', 'Já instalado'],
  older: ['badge warning', 'Mais antigo'], dlc: ['badge cheat', 'DLC'], 'dlc-nogame': ['badge warning', 'DLC sem o jogo'], unknown: ['badge outline', '?']
};

function pkgCardHtml(p, onConsole, lan) {
  const st = PKG_STATUS[p.status] || PKG_STATUS.unknown;
  const icon = !onConsole && p.hasIcon ? `<img class="pkg-icon" src="/api/pc/pkg/icon?name=${encodeURIComponent(p.name)}" alt="" loading="lazy">` : `<span class="pkg-icon ph"><svg><use href="#i-package"/></svg></span>`;
  return `
    <div class="pkg-card pkg-rich">
      <div class="pkg-row">${icon}
        <div class="grow">
          <div class="pkg-title" title="${esc(p.name)}">${esc(p.title || p.name)}</div>
          <div class="pkg-sub">${[p.kind, p.titleId, p.version ? 'v' + p.version : ''].filter(Boolean).map(esc).join(' · ')}</div>
        </div>
      </div>
      <div class="pkg-meta"><span><span class="${st[0]}">${st[1]}</span> ${esc(p.note || '')}</span><span>${formatBytes(p.size)}</span></div>
      <div class="pkg-actions">
        ${onConsole
          ? `<a href="/api/files/download?path=${encodeURIComponent(p.path)}" class="btn btn-sm btn-outline flex-1 text-center">Baixar</a>
             <button class="btn btn-sm btn-danger" data-action="delete" data-path="${esc(p.path)}" data-dir="false">Excluir</button>`
          : `<button class="btn btn-sm btn-primary flex-1" data-action="pc-install" data-name="${esc(p.name)}" ${lan ? '' : 'disabled'}>${p.status === 'installed' ? 'Reinstalar' : 'Instalar no PS4'}</button>`}
      </div>
    </div>`;
}

loadPcPkgs = async function () {
  const info = document.getElementById('pcpkgs-info');
  const container = document.getElementById('pcpkgs-container');
  try {
    const d = await api('/api/pc/pkgs', { timeout: 120000 });
    if (!d.folder) {
      info.innerHTML = `Configure a <strong>pasta de PKGs do PC</strong> em <a href="#" class="link" data-tab-trigger="settings">Configurações</a> para listar os arquivos aqui.`;
      container.innerHTML = '';
      return;
    }
    if (!d.exists) { info.innerHTML = `A pasta <code>${esc(d.folder)}</code> não existe neste PC.`; container.innerHTML = ''; return; }
    info.innerHTML = d.lanAccess
      ? `Pasta: <code>${esc(d.folder)}</code> · o PS4 vai baixar de <code>${esc(d.serverUrl)}</code>. Deixe o <strong>Remote Package Installer</strong> aberto no PS4.`
      : `<span style="color:var(--warning)">O acesso pela rede está desativado: o PS4 não conseguirá baixar. Execute <code>liberar-rede.bat</code> uma vez.</span>`;
    container.innerHTML = d.files.length ? d.files.map(f => pkgCardHtml(f, false, d.lanAccess)).join('') : '<div class="empty-state">Nenhum .pkg nesta pasta.</div>';
  } catch (err) {
    info.innerHTML = `<span class="text-danger">${esc(err.message)}</span>`;
  }
};

// ======================================================= CAPTURES: multi-select + zip
function captureRealPath(card) {
  try { return new URL(card.dataset.full, location.origin).searchParams.get('path'); } catch (e) { return null; }
}

function toggleCaptureSelect(on) {
  fx.selectCaptures = on;
  fx.selected.clear();
  document.getElementById('pane-media').classList.toggle('selecting', on);
  document.querySelectorAll('.capture-card.selected').forEach(c => c.classList.remove('selected'));
  document.querySelector('#btn-select-captures span').textContent = on ? 'Cancelar seleção' : 'Selecionar';
  updateZipBtn();
}

function updateZipBtn() {
  const b = document.getElementById('btn-zip-captures');
  b.hidden = !fx.selectCaptures;
  b.disabled = fx.selected.size === 0;
  document.getElementById('zip-count').textContent = fx.selected.size;
}

document.addEventListener('click', (e) => {
  if (!fx.selectCaptures) return;
  const card = e.target.closest('.capture-card');
  if (!card) return;
  e.preventDefault();
  e.stopPropagation();
  const p = captureRealPath(card);
  if (!p) return;
  if (fx.selected.has(p)) { fx.selected.delete(p); card.classList.remove('selected'); }
  else { fx.selected.add(p); card.classList.add('selected'); }
  updateZipBtn();
}, true);

async function zipCaptures() {
  const b = document.getElementById('btn-zip-captures');
  b.disabled = true;
  showToast(`Compactando ${fx.selected.size} capturas…`, 'info');
  try {
    await downloadPost('/api/captures/zip', { paths: Array.from(fx.selected) }, 'capturas_ps4.zip');
    toggleCaptureSelect(false);
  } catch (e) { showToast(e.message, 'error'); }
  b.disabled = false;
}

// ======================================================= SAVES
async function ensureUsers() {
  if (state.summary && state.summary.users && state.summary.users.length) return state.summary.users;
  await fetchSummary(true);
  return (state.summary && state.summary.users) || [];
}

async function loadSaves() {
  const sel = document.getElementById('saves-user');
  const status = document.getElementById('saves-status');
  const box = document.getElementById('saves-container');
  const users = await ensureUsers();
  if (!users.length) { box.innerHTML = '<div class="empty-state">Não foi possível ler os usuários do PS4. Verifique a conexão.</div>'; return; }
  if (!sel.options.length) {
    sel.innerHTML = users.map(u => `<option value="${esc(u.id)}" ${u.isCurrent ? 'selected' : ''}>${esc(u.name)}</option>`).join('');
  }
  const user = sel.value;
  box.innerHTML = '<div class="loading-state"><div class="spinner"></div><span>Lendo os saves do PS4…</span></div>';
  try {
    const d = await api(`/api/saves?user=${encodeURIComponent(user)}`, { timeout: 120000 });
    fx.saves = d;
    status.textContent = `${d.games.length} jogos com save · backups em ${d.folder}`;
    box.innerHTML = d.games.length ? d.games.map(g => `
      <div class="save-item" data-id="${esc(g.id)}">
        <img src="${esc(g.iconUrl)}" alt="" data-icon="${esc(g.id)}" loading="lazy">
        <div class="grow">
          <strong>${esc(g.name)}</strong>
          <small>${esc(g.id)}${g.installed ? '' : ' · jogo não instalado'} · ${g.backups ? `${g.backups} backup${g.backups > 1 ? 's' : ''}, último ${esc(g.lastBackup)}` : 'sem backup ainda'}</small>
          <div class="save-backups" id="sb-${esc(g.id)}" hidden></div>
        </div>
        <div class="row">
          ${g.backups ? `<button class="btn btn-ghost btn-sm" data-fx="save-list" data-id="${esc(g.id)}">Backups</button>` : ''}
          <button class="btn btn-outline btn-sm" data-fx="save-backup" data-id="${esc(g.id)}"><svg><use href="#i-save"/></svg>Backup</button>
        </div>
      </div>`).join('') : '<div class="empty-state">Nenhum save encontrado para este usuário.</div>';
  } catch (e) {
    box.innerHTML = `<div class="empty-state text-danger">${esc(e.message)}</div>`;
  }
}

async function saveBackup(id, btn) {
  const user = document.getElementById('saves-user').value;
  if (btn) { btn.disabled = true; btn.innerHTML = '<span class="spinner small"></span> Copiando…'; }
  try {
    const r = await postJson(`/api/saves/backup?user=${encodeURIComponent(user)}&id=${encodeURIComponent(id)}`, {}, { timeout: 600000 });
    if (id === '*') showToast(`Backup concluído: ${r.ok} jogos${r.failed ? `, ${r.failed} com erro` : ''}.`, r.failed ? 'error' : 'success', 7000);
    else showToast(`Backup feito (${r.backup.files} arquivos, ${formatBytes(r.backup.size)}).`, 'success');
  } catch (e) { showToast('Falha no backup: ' + e.message, 'error', 8000); }
  loadSaves();
}

async function saveListBackups(id) {
  const user = document.getElementById('saves-user').value;
  const box = document.getElementById('sb-' + id);
  if (!box) return;
  if (!box.hidden) { box.hidden = true; return; }
  box.hidden = false;
  box.innerHTML = '<span class="spinner small"></span>';
  try {
    const list = await api(`/api/saves/backups?user=${encodeURIComponent(user)}&id=${encodeURIComponent(id)}`);
    box.innerHTML = list.map(b => `
      <div class="backup-row">
        <span class="grow">${esc(b.when)} · ${b.files} arquivos · ${formatBytes(b.size)}${b.note ? ` · <em>${esc(b.note)}</em>` : ''}</span>
        <button class="btn btn-outline btn-sm" data-fx="save-restore" data-id="${esc(id)}" data-stamp="${esc(b.stamp)}">Restaurar</button>
        <a class="btn btn-ghost btn-sm" href="/api/saves/zip?user=${encodeURIComponent(user)}&id=${encodeURIComponent(id)}&stamp=${encodeURIComponent(b.stamp)}">.zip</a>
        <button class="btn btn-ghost btn-sm" data-fx="save-delete" data-id="${esc(id)}" data-stamp="${esc(b.stamp)}" aria-label="Apagar backup"><svg><use href="#i-trash"/></svg></button>
      </div>`).join('') || '<span class="text-muted">Nenhum backup.</span>';
  } catch (e) { box.innerHTML = `<span class="text-danger">${esc(e.message)}</span>`; }
}

async function saveRestore(id, stamp) {
  const g = (fx.saves && fx.saves.games.find(x => x.id === id)) || { name: id };
  if (!confirmBox(`Restaurar o save de "${g.name}" do backup de ${stamp.replace(/^(\d{4})(\d{2})(\d{2})_(\d{2})(\d{2}).*/, '$3/$2/$1 $4:$5')}?\n\nFeche o jogo no PS4 antes. O save atual será guardado automaticamente antes de ser substituído.`)) return;
  const user = document.getElementById('saves-user').value;
  try {
    const r = await postJson(`/api/saves/restore?user=${encodeURIComponent(user)}&id=${encodeURIComponent(id)}&stamp=${encodeURIComponent(stamp)}`, {}, { timeout: 600000 });
    showToast(`Save restaurado (${r.files} arquivos).`, 'success', 6000);
  } catch (e) { showToast('Falha ao restaurar: ' + e.message, 'error', 9000); }
  loadSaves();
}

async function saveDelete(id, stamp) {
  if (!confirmBox('Apagar este backup do PC? Isso não pode ser desfeito.')) return;
  const user = document.getElementById('saves-user').value;
  try {
    await postJson(`/api/saves/delete?user=${encodeURIComponent(user)}&id=${encodeURIComponent(id)}&stamp=${encodeURIComponent(stamp)}`, {});
    const box = document.getElementById('sb-' + id);
    if (box) { box.hidden = true; saveListBackups(id); }
  } catch (e) { showToast(e.message, 'error'); }
}

// ======================================================= STATS: history, storage, trophies
async function loadHistory() {
  const span = parseInt(document.getElementById('history-span').value, 10) || 14;
  const chart = document.getElementById('history-chart');
  const top = document.getElementById('history-top');
  try {
    const h = await api(`/api/history?days=${span}`);
    const max = Math.max(60, ...h.days.map(d => d.minutes));
    const total = h.days.reduce((s, d) => s + d.minutes, 0);
    document.getElementById('history-sub').textContent = h.trackingSince
      ? `${fmtMin(total)} jogados no período · o Orbis registra desde ${h.trackingSince.split('-').reverse().join('/')} (dias com o Orbis fechado entram no dia seguinte).`
      : 'O registro começa hoje: o GoldHEN só guarda o total de cada jogo, então o Orbis anota o total todo dia que fica aberto.';
    chart.innerHTML = h.days.map(d => {
      const pct = Math.round(d.minutes * 100 / max);
      const label = d.date.slice(8, 10) + '/' + d.date.slice(5, 7);
      const tip = d.games.length ? d.games.map(g => `${g.name}: ${fmtMin(g.minutes)}`).join('\n') : (d.recorded ? 'Sem jogo' : 'Sem registro');
      return `<div class="hc-col" title="${esc(label + ' · ' + fmtMin(d.minutes) + '\n' + tip)}">
        <span class="hc-val">${d.minutes ? fmtMin(d.minutes) : ''}</span>
        <span class="hc-bar ${d.recorded ? '' : 'empty'}" style="height:${Math.max(d.minutes ? 4 : 2, pct)}%"></span>
        <span class="hc-label">${label}</span></div>`;
    }).join('');
    top.innerHTML = h.top.length ? `<h4>Mais jogados no período</h4>` + h.top.slice(0, 6).map(g => `
      <div class="top-row" data-action="open-game" data-id="${esc(g.id)}" role="button" tabindex="0">
        <img src="${esc(g.iconUrl)}" alt="" data-icon="${esc(g.id)}" loading="lazy">
        <span class="grow">${esc(g.name)}</span><strong>${fmtMin(g.minutes)}</strong></div>`).join('') : '';
  } catch (e) { chart.innerHTML = `<div class="text-danger">${esc(e.message)}</div>`; }
}

let storageTimer = null;
async function loadStorage(refresh) {
  clearTimeout(storageTimer);
  const box = document.getElementById('storage-container');
  const status = document.getElementById('storage-status');
  try {
    const s = await api('/api/storage' + (refresh ? '?refresh=1' : ''), { timeout: 30000 });
    if (s.scanning) {
      status.innerHTML = `<span class="spinner small"></span> Medindo… ${s.done} de ${s.total} jogos`;
      if (state.currentTab === 'stats') storageTimer = setTimeout(() => loadStorage(false), 2500);
    } else status.textContent = s.games.length ? `Total: ${formatBytes(s.bytes)} em ${s.games.length} jogos` : '';
    if (!s.games.length) { box.innerHTML = s.scanning ? '' : '<div class="empty-state">Clique em <strong>Calcular</strong> para medir quanto cada jogo ocupa (leva alguns minutos na primeira vez).</div>'; return; }
    const max = s.games[0].total || 1;
    box.innerHTML = s.games.map(g => `
      <div class="storage-row" data-action="open-game" data-id="${esc(g.id)}" role="button" tabindex="0">
        <img src="${esc(g.iconUrl)}" alt="" data-icon="${esc(g.id)}" loading="lazy">
        <div class="grow">
          <div class="storage-top"><strong>${esc(g.name)}</strong><span class="mono">${formatBytes(g.total)}</span></div>
          <div class="storage-bar" style="width:${Math.max(2, g.total * 100 / max)}%">
            <span class="sb-app" style="flex:${g.app}"></span><span class="sb-patch" style="flex:${g.patch}"></span><span class="sb-dlc" style="flex:${g.dlc}"></span>
          </div>
          <small>Jogo ${formatBytes(g.app)} · Atualização ${formatBytes(g.patch)}${g.dlcs.length ? ` · ${g.dlcs.length} DLC (${formatBytes(g.dlc)})` : ''} · jogado ${fmtMin(g.minutes)}</small>
        </div>
      </div>`).join('');
  } catch (e) { status.innerHTML = `<span class="text-danger">${esc(e.message)}</span>`; }
}

async function loadTrophies() {
  const box = document.getElementById('trophies-container');
  const users = await ensureUsers();
  const cur = users.find(u => u.isCurrent) || users[0];
  if (!cur) { box.innerHTML = '<div class="empty-state">Usuário não encontrado.</div>'; return; }
  box.innerHTML = '<div class="loading-state"><div class="spinner"></div><span>Lendo os troféus do console…</span></div>';
  try {
    const t = await api(`/api/trophies?user=${encodeURIComponent(cur.id)}`, { timeout: 120000 });
    if (!t.games.length) { box.innerHTML = '<div class="empty-state">Nenhum troféu encontrado.</div>'; return; }
    const tot = t.totals;
    box.innerHTML = `<div class="trophy-totals">
        <span class="tt plat">${tot.platinum}<small>Platina</small></span><span class="tt gold">${tot.gold}<small>Ouro</small></span>
        <span class="tt silver">${tot.silver}<small>Prata</small></span><span class="tt bronze">${tot.bronze}<small>Bronze</small></span></div>` +
      t.games.map(g => `
      <div class="trophy-row">
        ${g.iconUrl ? `<img src="${esc(g.iconUrl)}" alt="" loading="lazy" data-icon="${esc(g.titleId || '')}">` : '<span class="pkg-icon ph"><svg><use href="#i-trophy"/></svg></span>'}
        <div class="grow">
          <div class="storage-top"><strong>${esc(g.name)}</strong><span class="mono">${g.progress}%</span></div>
          <div class="progress-bar-wrap"><div class="progress-bar" style="width:${g.progress}%"></div></div>
          <small>${g.unlocked} de ${g.total} troféus${g.platinum ? ' · 🏆 Platina' : ''}${g.lastUnlocked ? ' · último ' + esc(g.lastUnlocked) : ''}</small>
        </div>
      </div>`).join('');
  } catch (e) { box.innerHTML = `<div class="empty-state text-danger">${esc(e.message)}</div>`; }
}

// ======================================================= GOLDHEN
function loadGoldhenSub(sub) {
  document.querySelectorAll('#pane-goldhen .subtab').forEach(b => { const on = b.dataset.sub === sub; b.classList.toggle('active', on); b.setAttribute('aria-selected', on); });
  document.querySelectorAll('#pane-goldhen .subpane').forEach(p => p.classList.toggle('active', p.id === sub));
  if (sub === 'gh-config') loadGhConfig();
  if (sub === 'gh-plugins') loadPlugins();
  if (sub === 'gh-patches') loadPatches(false);
  if (sub === 'gh-cheats') loadCheatRepo(false);
}

const GH_LABELS = {
  FTPServer: 'Servidor FTP', BinLoader: 'Servidor BinLoader (payloads)', KLogServer: 'Servidor de log (KLog)', Cheat: 'Cheats',
  GameOverlay: 'Overlay no jogo (FPS e sistema)', PluginLoader: 'Plugins e patches', DebugSettings: 'Debug Settings (instalar PKG)',
  GameDebugSettings: 'Debug dos jogos', PPPoE: 'PPPoE', BDApp: 'Disco Blu-ray', Date_Time: 'Data e hora',
  Enabled: 'Ativado', Menu_Enabled: 'Menu de cheats', AutoApply_Enabled: 'Aplicar cheats automaticamente', Show_TitleId: 'Mostrar Title ID',
  FPS_Enabled: 'Contador de FPS', FPS_Visible: 'FPS visível', SysInfos_Enabled: 'Informações do sistema',
  SysInfos_CPU_Temp_Visible: 'Temperatura da CPU', SysInfos_SOC_Temp_Visible: 'Temperatura do SoC', SysInfos_RAM_Usage_Visible: 'Uso de RAM',
  SysInfos_VRAM_Usage_Visible: 'Uso de VRAM', SysInfos_CPU_Usage_Visible: 'Uso de CPU', SysInfos_IP_Address_Visible: 'Endereço IP',
  Scanlines_Enabled: 'Scanlines', PluginLoader_Enabled: 'Carregar plugins', Game_Patch_Enabled: 'Patches de jogo', AIO_Fix_Enabled: 'Correções AIO',
  Bgft_Enabled: 'Instalar PKG em segundo plano', Auto_Update_Enabled: 'Atualizar data/hora automaticamente', Patch_Enabled: 'Patch PPPoE',
  AutoKill_Enabled: 'Fechar app de disco', AutoEject_Enabled: 'Ejetar disco automaticamente', TTYRedirect: 'Redirecionar TTY'
};

async function loadGhConfig() {
  const box = document.getElementById('ghconfig-container');
  const save = document.getElementById('btn-save-ghconfig');
  fx.ghConfigChanges = {};
  save.disabled = true;
  box.innerHTML = '<div class="loading-state"><div class="spinner"></div></div>';
  try {
    const c = await api('/api/goldhen/config');
    if (!c.found) { box.innerHTML = '<div class="empty-state">Não encontrei <code>/data/GoldHEN/config.ini</code> no PS4.</div>'; return; }
    box.innerHTML = c.sections.map(s => `
      <div class="gh-section">
        <h4>${esc(GH_LABELS[s.name] || s.name || 'Geral')}</h4>
        ${s.keys.map(k => {
          const id = `ghc-${s.name}-${k.key}`.replace(/[^\w-]/g, '_');
          return `<div class="gh-row">
            <label for="${id}" class="grow">${esc(GH_LABELS[k.key] || k.key)} <small class="mono">${esc(k.key)}</small></label>
            ${k.isBool
              ? `<label class="switch"><input type="checkbox" id="${id}" data-sec="${esc(s.name)}" data-key="${esc(k.key)}" ${k.value === '1' ? 'checked' : ''}><span></span></label>`
              : `<input type="text" class="input-sm" id="${id}" data-sec="${esc(s.name)}" data-key="${esc(k.key)}" value="${esc(k.value)}">`}
          </div>`;
        }).join('')}
      </div>`).join('');
    box.querySelectorAll('input[data-key]').forEach(inp => inp.addEventListener('change', () => {
      const v = inp.type === 'checkbox' ? (inp.checked ? '1' : '0') : inp.value.trim();
      fx.ghConfigChanges[inp.dataset.sec + '\u0001' + inp.dataset.key] = { section: inp.dataset.sec, key: inp.dataset.key, value: v };
      save.disabled = false;
    }));
  } catch (e) { box.innerHTML = `<div class="empty-state text-danger">${esc(e.message)}</div>`; }
}

async function saveGhConfig() {
  const changes = Object.values(fx.ghConfigChanges);
  if (!changes.length) return;
  try {
    await postJson('/api/goldhen/config', { changes });
    showToast('Configurações salvas no PS4. Reinicie o console para aplicar.', 'success', 6000);
    loadGhConfig();
  } catch (e) { showToast(e.message, 'error'); }
}

async function loadPlugins() {
  const box = document.getElementById('plugins-container');
  const save = document.getElementById('btn-save-plugins');
  save.disabled = true;
  box.innerHTML = '<div class="loading-state"><div class="spinner"></div></div>';
  try {
    const p = await api('/api/goldhen/plugins');
    // normalise: keep a [default] section first
    if (!p.sections.some(s => s.name === 'default')) p.sections.unshift({ name: 'default', title: '', entries: [] });
    fx.plugins = p;
    renderPlugins();
  } catch (e) { box.innerHTML = `<div class="empty-state text-danger">${esc(e.message)}</div>`; }
}

function renderPlugins() {
  const p = fx.plugins;
  const box = document.getElementById('plugins-container');
  if (!p.available.length) {
    box.innerHTML = '<div class="empty-state">Nenhum plugin (.prx) em <code>/data/GoldHEN/plugins/</code>. Envie os arquivos pela tela Arquivos.</div>';
    return;
  }
  const sectionHtml = (s) => {
    const label = s.name === 'default' ? 'Todos os jogos' : `${s.title || s.name} <small class="mono">${esc(s.name)}</small>`;
    return `<div class="gh-section">
      <div class="gh-section-head"><h4>${label}</h4>${s.name !== 'default' ? `<button class="btn btn-ghost btn-sm" data-fx="plg-remove" data-sec="${esc(s.name)}">Remover</button>` : ''}</div>
      ${p.available.map(a => {
        const e = s.entries.find(x => x.path === a.path);
        return `<div class="gh-row"><span class="grow">${esc(a.name)}</span>
          <label class="switch"><input type="checkbox" data-fx-plg="${esc(s.name)}" data-path="${esc(a.path)}" ${e && e.enabled ? 'checked' : ''}><span></span></label></div>`;
      }).join('')}
    </div>`;
  };
  const games = state.games.filter(g => !p.sections.some(s => s.name === g.id));
  box.innerHTML = p.sections.map(sectionHtml).join('') + `
    <div class="gh-add row">
      <select id="plg-add-game" class="select-sm"><option value="">Adicionar um jogo…</option>${games.map(g => `<option value="${esc(g.id)}">${esc(g.name)}</option>`).join('')}</select>
      <button class="btn btn-outline btn-sm" data-fx="plg-add"><svg><use href="#i-plus"/></svg>Adicionar</button>
    </div>`;
}

document.addEventListener('change', (e) => {
  const inp = e.target.closest('input[data-fx-plg]');
  if (!inp || !fx.plugins) return;
  const s = fx.plugins.sections.find(x => x.name === inp.dataset.fxPlg);
  let entry = s.entries.find(x => x.path === inp.dataset.path);
  if (!entry) { entry = { path: inp.dataset.path, enabled: false }; s.entries.push(entry); }
  entry.enabled = inp.checked;
  document.getElementById('btn-save-plugins').disabled = false;
});

async function savePlugins() {
  try {
    await postJson('/api/goldhen/plugins', { sections: fx.plugins.sections.map(s => ({ name: s.name, entries: s.entries })) });
    showToast('Plugins salvos. Valem na próxima vez que o jogo abrir.', 'success', 6000);
    loadPlugins();
  } catch (e) { showToast(e.message, 'error'); }
}

async function loadPatches(refresh) {
  const box = document.getElementById('patches-container');
  const status = document.getElementById('patches-status');
  box.innerHTML = '<div class="loading-state"><div class="spinner"></div><span>Baixando a lista de patches…</span></div>';
  try {
    const p = await api('/api/goldhen/patches' + (refresh ? '?refresh=1' : ''), { timeout: 120000 });
    status.textContent = `${p.games.length} dos seus jogos têm patches · repositório com ${p.repoSize} jogos · lista de ${p.updated}`;
    box.innerHTML = p.games.length ? p.games.map(g => {
      const usable = g.patches.filter(x => x.matches);
      return `<div class="gh-game">
        <img src="${esc(g.iconUrl)}" alt="" data-icon="${esc(g.id)}" loading="lazy">
        <div class="grow">
          <strong>${esc(g.name)}</strong> <small class="mono">v${esc(g.version || '?')}</small>
          <ul class="patch-list">${g.patches.map(x => `<li class="${x.matches ? '' : 'other-ver'}" title="${esc(x.note || '')}">${esc(x.name)} <small>${x.appVer ? 'v' + esc(x.appVer) : ''}${x.author ? ' · ' + esc(x.author) : ''}</small></li>`).join('')}</ul>
          ${g.patches.length && !usable.length ? '<small class="text-warning">Nenhum patch é para a versão instalada.</small>' : ''}
        </div>
        <div class="row">
          ${g.installed ? '<span class="badge success">Instalado</span>' : ''}
          ${g.available ? `<button class="btn ${g.installed ? 'btn-ghost' : 'btn-primary'} btn-sm" data-fx="patch-install" data-id="${esc(g.id)}">${g.installed ? 'Atualizar' : 'Instalar'}</button>` : ''}
          ${g.installed ? `<button class="btn btn-ghost btn-sm" data-fx="patch-remove" data-id="${esc(g.id)}">Remover</button>` : ''}
        </div>
      </div>`;
    }).join('') : '<div class="empty-state">Nenhum dos seus jogos tem patches no repositório.</div>';
  } catch (e) { box.innerHTML = `<div class="empty-state text-danger">${esc(e.message)}</div>`; }
}

async function patchAction(id, remove) {
  try {
    await postJson(`/api/goldhen/patches/install?id=${encodeURIComponent(id)}${remove ? '&remove=1' : ''}`, {});
    showToast(remove ? 'Patches removidos.' : 'Patches instalados! Ative no menu do GoldHEN dentro do jogo.', 'success', 6000);
    loadPatches(false);
  } catch (e) { showToast(e.message, 'error'); }
}

async function loadCheatRepo(refresh) {
  const box = document.getElementById('cheatrepo-container');
  const status = document.getElementById('cheatrepo-status');
  box.innerHTML = '<div class="loading-state"><div class="spinner"></div></div>';
  try {
    const c = await api('/api/goldhen/cheats-repo' + (refresh ? '?refresh=1' : ''), { timeout: 120000 });
    status.textContent = `${c.games.length} dos seus jogos têm cheats no repositório · lista de ${c.updated}`;
    box.innerHTML = c.games.length ? c.games.map(g => `
      <div class="gh-game">
        <img src="${esc(g.iconUrl)}" alt="" data-icon="${esc(g.id)}" loading="lazy">
        <div class="grow"><strong>${esc(g.name)}</strong> <small class="mono">instalado v${esc(g.version || '?')}</small>
          <div class="cheat-files">${g.files.map(f => `
            <span class="cheat-file ${f.matches ? 'match' : ''}">
              v${esc(f.version)}${f.matches ? ' ✓' : ''}
              ${f.installed ? '<span class="badge success">no PS4</span>' : `<button class="btn btn-ghost btn-sm" data-fx="cheat-install" data-file="${esc(f.file)}">Instalar</button>`}
            </span>`).join('')}</div>
        </div>
      </div>`).join('') : '<div class="empty-state">Nenhum cheat no repositório para os seus jogos.</div>';
  } catch (e) { box.innerHTML = `<div class="empty-state text-danger">${esc(e.message)}</div>`; }
}

async function cheatInstall(file, btn) {
  btn.disabled = true;
  try {
    await postJson(`/api/goldhen/cheats-repo/install?file=${encodeURIComponent(file)}`, {});
    showToast('Cheat instalado no PS4!', 'success');
    btn.outerHTML = '<span class="badge success">no PS4</span>';
    fetchSummary(true);
  } catch (e) { btn.disabled = false; showToast(e.message, 'error'); }
}

// ======================================================= BATCH COVERS
function openBatchCovers() {
  const grid = document.getElementById('batch-grid');
  grid.innerHTML = state.games.map(g => `
    <label class="batch-item" title="${esc(g.name)}">
      <input type="checkbox" value="${esc(g.id)}" ${g.customCover ? '' : 'checked'}>
      <img src="${esc(g.iconUrl)}" alt="" data-icon="${esc(g.id)}" loading="lazy">
      <span>${esc(g.name)}</span>
    </label>`).join('');
  document.getElementById('batch-progress').innerHTML = '';
  updateBatchCount();
  openModal('modal-batch-covers');
}

function updateBatchCount() {
  document.getElementById('batch-count').textContent = document.querySelectorAll('#batch-grid input:checked').length;
}

async function startBatchCovers() {
  const ids = Array.from(document.querySelectorAll('#batch-grid input:checked')).map(i => i.value);
  if (!ids.length) return;
  if (!confirmBox(`Trocar a capa de ${ids.length} jogo(s)? As originais ficam guardadas.`)) return;
  const prog = document.getElementById('batch-progress');
  const btn = document.getElementById('batch-start');
  btn.disabled = true;
  try {
    let s = await postJson('/api/covers/batch', { ids, source: document.getElementById('batch-source').value });
    while (s.running) {
      prog.innerHTML = `<div class="text-sm">${s.done} de ${s.total} · ${esc(s.current)}</div><div class="progress-bar-wrap"><div class="progress-bar" style="width:${s.total ? s.done * 100 / s.total : 0}%"></div></div>`;
      await new Promise(r => setTimeout(r, 1500));
      s = await api('/api/covers/batch');
    }
    prog.innerHTML = `<div class="info-box"><strong>${s.done - s.failed} capas trocadas.</strong>${s.failed ? ` ${s.failed} sem arte encontrada:<br><small>${s.errors.map(esc).join('<br>')}</small>` : ''}</div>`;
    showToast('Capas trocadas! Reinicie o PS4 se a tela inicial não atualizar.', 'success', 7000);
    fetchGames('refresh');
  } catch (e) { prog.innerHTML = `<div class="text-danger">${esc(e.message)}</div>`; }
  btn.disabled = false;
}

// ======================================================= SETTINGS: play limit, PIN, about
async function loadSettingsExtra() {
  try {
    const pl = await api('/api/playlimit');
    document.getElementById('pl-minutes').value = pl.minutes || '';
    document.getElementById('pl-curfew').value = pl.curfew || '';
  } catch (e) { }
  try {
    const a = await api('/api/auth/status');
    document.getElementById('pin-state').innerHTML = a.enabled
      ? '<strong>PIN ativo:</strong> celulares e outros aparelhos pedem o PIN uma vez (fica lembrado por 30 dias). No próprio PC nunca é pedido.'
      : 'Sem PIN: qualquer aparelho no seu Wi-Fi abre o Orbis. No próprio PC nunca é pedido.';
    document.getElementById('btn-clear-pin').hidden = !a.enabled;
  } catch (e) { }
  document.getElementById('about-version').textContent = 'v' + ((state.status && state.status.version) || '');
  const u = fx.appUpdate;
  document.getElementById('about-update').innerHTML = u ? (u.hasUpdate ? `· <a class="link" href="${esc(u.url)}" target="_blank" rel="noopener">nova versão v${esc(u.latest)} disponível</a>` : '· você está na versão mais recente') : '';
}

async function savePlayLimit() {
  try {
    await postJson('/api/playlimit', { minutes: document.getElementById('pl-minutes').value || '0', curfew: document.getElementById('pl-curfew').value || '' });
    showToast('Limite salvo. O aviso aparece na TV (precisa do ps4debug carregado).', 'success', 6000);
  } catch (e) { showToast(e.message, 'error'); }
}

async function savePin(clear) {
  const pin = clear ? '' : document.getElementById('pin-input').value.trim();
  if (!clear && !/^\d{4,8}$/.test(pin)) return showToast('Use de 4 a 8 números.', 'error');
  if (clear && !confirmBox('Remover o PIN? Qualquer aparelho no seu Wi-Fi poderá abrir o Orbis.')) return;
  try {
    await postJson('/api/auth/pin', { pin });
    document.getElementById('pin-input').value = '';
    showToast(clear ? 'PIN removido.' : 'PIN definido. Os outros aparelhos vão pedir o PIN.', 'success');
    loadSettingsExtra();
  } catch (e) { showToast(e.message, 'error'); }
}

// ======================================================= COMMAND PALETTE (Ctrl+K)
const PALETTE_PAGES = [
  ['dashboard', 'Dashboard', '#i-home'], ['games', 'Jogos & Apps', '#i-games'], ['media', 'Capturas', '#i-image'], ['stats', 'Estatísticas', '#i-chart'],
  ['downloads', 'Downloads', '#i-download'], ['saves', 'Saves (backup)', '#i-save'], ['files', 'Arquivos (FTP)', '#i-folder'], ['pkgs', 'Pacotes (PKG)', '#i-package'],
  ['cheats', 'Cheats & Mods', '#i-bolt'], ['goldhen', 'GoldHEN', '#i-sliders'], ['tools', 'Payloads & Ações', '#i-tool'], ['settings', 'Configurações', '#i-settings']
];
const PALETTE_ACTIONS = [
  ['Enviar PKG para o PS4', '#i-upload', () => document.getElementById('pkg-file-input').click()],
  ['Verificar atualizações dos jogos', '#i-download', () => { switchTab('games'); loadUpdates(true); }],
  ['Backup de todos os saves', '#i-save', () => { switchTab('saves'); setTimeout(() => saveBackup('*'), 600); }],
  ['Mandar notificação para a TV', '#i-bell', () => { switchTab('tools'); setTimeout(() => document.getElementById('notify-msg-input').focus(), 100); }],
  ['Trocar capas em lote', '#i-image', () => { switchTab('games'); openBatchCovers(); }],
  ['Patches de jogo (60 FPS…)', '#i-sliders', () => { switchTab('goldhen'); loadGoldhenSub('gh-patches'); }],
  ['Cheats online', '#i-bolt', () => { switchTab('goldhen'); loadGoldhenSub('gh-cheats'); }],
  ['Exportar biblioteca (planilha)', '#i-save', () => { location.href = '/api/export/library.csv'; }],
  ['Carregar ps4debug', '#i-download', () => document.getElementById('btn-load-ps4debug').click()]
];
let paletteItems = [], paletteIndex = 0;

function openPalette() {
  const inp = document.getElementById('palette-input');
  inp.value = '';
  renderPalette('');
  openModal('modal-palette');
  setTimeout(() => inp.focus(), 30);
}

function norm(s) { return (s || '').normalize('NFD').replace(/[̀-ͯ]/g, '').toLowerCase(); }

function renderPalette(q) {
  const n = norm(q);
  const items = [];
  PALETTE_ACTIONS.forEach(([label, icon, run]) => { if (!n || norm(label).includes(n)) items.push({ label, icon, run, kind: 'Ação' }); });
  PALETTE_PAGES.forEach(([tab, label, icon]) => { if (!n || norm(label).includes(n)) items.push({ label, icon, run: () => switchTab(tab), kind: 'Tela' }); });
  if (n) state.games.filter(g => norm(g.name).includes(n) || norm(g.id).includes(n)).slice(0, 12).forEach(g =>
    items.unshift({ label: g.name, img: g.iconUrl, id: g.id, run: () => { switchTab('games'); openGameDetails(g.id); }, kind: g.id }));
  paletteItems = items.slice(0, 18);
  paletteIndex = 0;
  document.getElementById('palette-results').innerHTML = paletteItems.length ? paletteItems.map((it, i) => `
    <li role="option" class="${i === 0 ? 'active' : ''}" data-i="${i}">
      ${it.img ? `<img src="${esc(it.img)}" alt="" data-icon="${esc(it.id)}">` : `<svg><use href="${it.icon}"/></svg>`}
      <span class="grow">${esc(it.label)}</span><small>${esc(it.kind)}</small>
    </li>`).join('') : '<li class="empty">Nada encontrado.</li>';
}

function runPalette(i) {
  const it = paletteItems[i];
  if (!it) return;
  closeModal('modal-palette');
  it.run();
}

// ======================================================= WIRING
document.addEventListener('click', (e) => {
  const el = e.target.closest('[data-fx]');
  if (!el) return;
  const id = el.dataset.id;
  switch (el.dataset.fx) {
    case 'uninstall': uninstall(id, el.dataset.what); break;
    case 'dl': downloadControl(el.dataset.task, el.dataset.act, el.dataset.name); break;
    case 'save-backup': saveBackup(id, el); break;
    case 'save-list': saveListBackups(id); break;
    case 'save-restore': saveRestore(id, el.dataset.stamp); break;
    case 'save-delete': saveDelete(id, el.dataset.stamp); break;
    case 'patch-install': patchAction(id, false); break;
    case 'patch-remove': patchAction(id, true); break;
    case 'cheat-install': cheatInstall(el.dataset.file, el); break;
    case 'plg-remove':
      fx.plugins.sections = fx.plugins.sections.filter(s => s.name !== el.dataset.sec);
      document.getElementById('btn-save-plugins').disabled = false;
      renderPlugins();
      break;
    case 'plg-add': {
      const gid = document.getElementById('plg-add-game').value;
      if (!gid) return;
      const g = state.games.find(x => x.id === gid);
      fx.plugins.sections.push({ name: gid, title: g ? g.name : gid, entries: [] });
      renderPlugins();
      break;
    }
  }
});

document.addEventListener('DOMContentLoaded', () => {
  const on = (id, ev, fn) => { const el = document.getElementById(id); if (el) el.addEventListener(ev, fn); };
  on('btn-check-updates', 'click', () => loadUpdates(true));
  on('btn-batch-covers', 'click', openBatchCovers);
  on('batch-start', 'click', startBatchCovers);
  on('batch-grid', 'change', updateBatchCount);
  on('batch-all', 'click', () => { document.querySelectorAll('#batch-grid input').forEach(i => i.checked = true); updateBatchCount(); });
  on('batch-none', 'click', () => { document.querySelectorAll('#batch-grid input').forEach(i => i.checked = false); updateBatchCount(); });
  on('batch-nocustom', 'click', () => { document.querySelectorAll('#batch-grid input').forEach(i => { const g = state.games.find(x => x.id === i.value); i.checked = !(g && g.customCover); }); updateBatchCount(); });
  document.querySelectorAll('#modal-batch-covers [data-close]').forEach(b => b.addEventListener('click', () => closeModal('modal-batch-covers')));
  on('btn-analyze-pkgs', 'click', analyzePkgs);
  on('btn-select-captures', 'click', () => toggleCaptureSelect(!fx.selectCaptures));
  on('btn-zip-captures', 'click', zipCaptures);
  on('saves-user', 'change', loadSaves);
  on('btn-backup-all', 'click', () => { if (confirmBox('Fazer backup dos saves de todos os jogos? Pode levar alguns minutos.')) saveBackup('*', document.getElementById('btn-backup-all')); });
  on('btn-open-saves-folder', 'click', async () => { try { await postJson('/api/saves/open-folder', {}); } catch (e) { showToast(e.message, 'info'); } });
  on('history-span', 'change', loadHistory);
  on('btn-scan-storage', 'click', () => loadStorage(true));
  on('btn-load-trophies', 'click', loadTrophies);
  document.querySelectorAll('#pane-goldhen .subtab').forEach(b => b.addEventListener('click', () => loadGoldhenSub(b.dataset.sub)));
  on('btn-save-ghconfig', 'click', saveGhConfig);
  on('btn-save-plugins', 'click', savePlugins);
  on('btn-refresh-patches', 'click', () => loadPatches(true));
  on('btn-refresh-cheatrepo', 'click', () => loadCheatRepo(true));
  on('btn-save-playlimit', 'click', savePlayLimit);
  on('btn-save-pin', 'click', () => savePin(false));
  on('btn-clear-pin', 'click', () => savePin(true));

  // Palette
  on('palette-input', 'input', (e) => renderPalette(e.target.value));
  on('palette-input', 'keydown', (e) => {
    const lis = document.querySelectorAll('#palette-results li[data-i]');
    if (e.key === 'ArrowDown' || e.key === 'ArrowUp') {
      e.preventDefault();
      paletteIndex = Math.max(0, Math.min(lis.length - 1, paletteIndex + (e.key === 'ArrowDown' ? 1 : -1)));
      lis.forEach((li, i) => li.classList.toggle('active', i === paletteIndex));
      if (lis[paletteIndex]) lis[paletteIndex].scrollIntoView({ block: 'nearest' });
    } else if (e.key === 'Enter') { e.preventDefault(); runPalette(paletteIndex); }
  });
  on('palette-results', 'click', (e) => { const li = e.target.closest('li[data-i]'); if (li) runPalette(+li.dataset.i); });
  document.addEventListener('keydown', (e) => {
    const typing = /INPUT|TEXTAREA|SELECT/.test((document.activeElement || {}).tagName || '');
    if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 'k') { e.preventDefault(); openPalette(); }
    else if (e.key === '/' && !typing && !document.querySelector('.modal-overlay.active')) { e.preventDefault(); openPalette(); }
  });
  const topRight = document.querySelector('.topbar-right');
  if (topRight) topRight.insertAdjacentHTML('afterbegin', `<button class="btn btn-ghost search-trigger" id="btn-palette" aria-label="Buscar (Ctrl+K)" title="Buscar (Ctrl+K)"><svg><use href="#i-search"/></svg><span class="hide-mobile">Buscar</span><kbd class="hide-mobile">Ctrl K</kbd></button>`);
  on('btn-palette', 'click', openPalette);

  // First loads
  setTimeout(() => { loadActivity(); loadNowPlaying(); loadUpdates(false); checkAppUpdate(); }, 1500);
  setInterval(() => { if (document.visibilityState === 'visible' && state.currentTab === 'dashboard') loadActivity(); }, 30000);

  // PWA (needs a secure context: localhost on the PC; on phones it still works as a home-screen shortcut)
  if ('serviceWorker' in navigator && window.isSecureContext) navigator.serviceWorker.register('/sw.js').catch(() => { });
});
