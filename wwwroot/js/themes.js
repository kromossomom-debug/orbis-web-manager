/* Orbis Web Manager: PS4 theme repository and builder (Temas tab). */
const th = { list: [], current: null, slot: null, view: 'mock' };

function themePreviewUrl(id) {
  const r = Date.now();
  if (th.view === 'mock') return `/api/themes/mock?id=${encodeURIComponent(id)}&r=${r}`;
  if (th.view === 'functions') return `/api/themes/mock?id=${encodeURIComponent(id)}&screen=functions&r=${r}`;
  return `/api/themes/preview?id=${encodeURIComponent(id)}&r=${r}`;
}

function refreshThemePreview() {
  if (!th.current) return;
  const img = document.getElementById('te-preview');
  img.style.opacity = '0.5';
  img.onload = () => { img.style.opacity = '1'; };
  img.src = themePreviewUrl(th.current.id);
}

const THEME_SLOTS = [
  ['home', 'Fundo da tela inicial', '1920×1080. Os jogos ficam por cima da metade de cima.'],
  ['functions', 'Fundo da área de funções', 'A tela de cima (notificações, amigos…). Se vazio, usa o fundo da tela inicial.'],
  ['icon', 'Ícone / logo', 'Aparece em Configurações › Temas e na prévia. PNG com transparência fica melhor.']
];

async function loadThemes(selectId) {
  const box = document.getElementById('themes-container');
  const status = document.getElementById('themes-status');
  try {
    const d = await api('/api/themes');
    th.list = d.themes;
    status.innerHTML = d.pkgTool ? `${d.themes.length} tema(s) · pasta <code>${esc(d.folder)}</code>`
      : '<span class="text-warning">O PkgTool (LibOrbisPkg) não está em tools\\pkgtool\\: dá para editar, mas não gerar o .pkg.</span>';
    box.innerHTML = d.themes.length ? d.themes.map(t => `
      <button class="theme-card ${th.current && th.current.id === t.id ? 'active' : ''}" data-theme="${esc(t.id)}">
        <img src="/api/themes/preview?id=${encodeURIComponent(t.id)}&t=${encodeURIComponent(t.builtAt || '')}" alt="" loading="lazy">
        <span class="theme-meta">
          <strong>${esc(t.name)}</strong>
          <small>${t.built ? `Gerado ${esc(t.builtAt)} · ${formatBytes(t.pkgSize)}` : 'Ainda não gerado'}${t.personal ? ' · uso pessoal' : ''}</small>
        </span>
        <span class="theme-swatch" style="background:linear-gradient(90deg, ${esc(t.accent)}, ${esc(t.accent2)})"></span>
      </button>`).join('') : '<div class="empty-state">Nenhum tema ainda. Dê um nome e clique em <strong>Criar tema</strong>.</div>';
    const pick = selectId || (th.current && th.current.id);
    if (pick) openThemeEditor(pick);
  } catch (e) { box.innerHTML = `<div class="empty-state text-danger">${esc(e.message)}</div>`; }
}

function openThemeEditor(id) {
  const t = th.list.find(x => x.id === id);
  if (!t) return;
  th.current = t;
  document.querySelectorAll('.theme-card').forEach(c => c.classList.toggle('active', c.dataset.theme === id));
  document.getElementById('theme-editor').hidden = false;
  document.getElementById('te-title').textContent = t.name;
  document.getElementById('te-sub').textContent = t.contentId ? `Content ID ${t.contentId}` : 'Gere o .pkg para criar o Content ID.';
  document.getElementById('te-name').value = t.name;
  document.getElementById('te-accent').value = t.accent || '#ff4fa3';
  document.getElementById('te-accent2').value = t.accent2 || '#ffa24c';
  document.getElementById('te-font').value = t.fontColor || '#ffffff';
  document.getElementById('te-dim').value = t.dim || 0;
  document.getElementById('te-dim-val').textContent = t.dim || 0;
  document.getElementById('te-personal').checked = !!t.personal;
  document.getElementById('te-shift').value = t.homeShift || 0;
  document.getElementById('te-shift-val').textContent = t.homeShift || 0;
  document.getElementById('te-grade').value = t.grade || 0;
  document.getElementById('te-grade-val').textContent = t.grade || 0;
  document.getElementById('te-logo').checked = !!t.logoOnHome;
  document.getElementById('te-logopos').value = t.logoPos || 'br';
  document.getElementById('te-bgm-remove').hidden = !t.bgm;
  if (t.bgm) document.getElementById('te-bgm-state').innerHTML = `<span class="text-success">♪ ${esc(t.bgm)} (${formatBytes(t.bgmSize)})</span>. Gere o .pkg de novo para incluir.`;
  else document.getElementById('te-bgm-state').textContent = 'Arquivo .at9 (ATRAC9). Converta o áudio com o ATRACTool: WAV 48 kHz estéreo → AT9. Toca só na tela inicial e para quando um jogo abre.';
  refreshThemePreview();
  const dl = document.getElementById('te-download');
  dl.href = `/api/themes/pkg?id=${encodeURIComponent(t.id)}`;
  dl.hidden = !t.built;
  document.getElementById('te-send').disabled = !t.built;
  document.getElementById('te-note').innerHTML = !t.built
    ? 'A prévia mostra a tela inicial. Clique em <strong>Enviar ao PS4</strong> (ou Gerar .pkg) quando estiver do seu jeito.'
    : t.stale
      ? '<span class="text-warning"><strong>Há alterações que ainda não estão no .pkg.</strong> Ao clicar em Enviar ao PS4, o Orbis gera o pacote de novo automaticamente.</span>'
      : `Pacote em dia: <strong>${formatBytes(t.pkgSize)}</strong>. No PS4, apague a versão anterior do tema em <em>Configurações › Temas</em> (Options › Excluir), instale em <em>Debug Settings › Package Installer</em> e aplique o tema.`;
  document.getElementById('te-send').disabled = false;
  const current = { home: t.hasHome, functions: t.hasFunctions, icon: t.hasIcon };
  document.getElementById('te-slots').innerHTML = THEME_SLOTS.map(([slot, label, hint]) => `
    <div class="te-slot">
      <div class="grow"><strong>${label}</strong><small>${hint}</small>
        <select class="select-sm" data-te-slot="${slot}">
          <option value="">${slot === 'functions' ? 'Usar o fundo da tela inicial' : '— nenhuma —'}</option>
          ${t.sources.map(f => `<option value="${esc(f)}">${esc(f)}</option>`).join('')}
        </select>
      </div>
      <button class="btn btn-outline btn-sm" data-te-upload="${slot}"><svg><use href="#i-upload"/></svg>Enviar imagem</button>
    </div>`).join('');
  fetchThemeSlots(id);
}

async function fetchThemeSlots(id) {
  // theme.json fields aren't in the list payload, so read them from the dedicated endpoint
  try {
    const t = await api(`/api/themes/get?id=${encodeURIComponent(id)}`);
    ['home', 'functions', 'icon'].forEach(s => { const sel = document.querySelector(`[data-te-slot="${s}"]`); if (sel) sel.value = t[s] || ''; });
  } catch (e) { }
}

function themeForm() {
  const v = (id) => document.getElementById(id).value;
  const body = {
    name: v('te-name'), accent: v('te-accent'), accent2: v('te-accent2'), fontColor: v('te-font'), dim: v('te-dim'),
    homeShift: v('te-shift'), grade: v('te-grade'), logoPos: v('te-logopos'),
    logoOnHome: document.getElementById('te-logo').checked, personal: document.getElementById('te-personal').checked
  };
  document.querySelectorAll('[data-te-slot]').forEach(s => body[s.dataset.teSlot] = s.value);
  return body;
}

async function saveTheme(quiet) {
  if (!th.current) return;
  try {
    await postJson(`/api/themes/update?id=${encodeURIComponent(th.current.id)}`, themeForm());
    if (!quiet) showToast('Tema salvo.', 'success');
    await loadThemes(th.current.id);
  } catch (e) { showToast(e.message, 'error'); }
}

async function buildTheme() {
  if (!th.current) return;
  const btn = document.getElementById('te-build');
  btn.disabled = true;
  btn.innerHTML = '<span class="spinner small"></span> Gerando…';
  try {
    await postJson(`/api/themes/update?id=${encodeURIComponent(th.current.id)}`, themeForm());
    const r = await postJson(`/api/themes/build?id=${encodeURIComponent(th.current.id)}`, {}, { timeout: 300000 });
    showToast(`.pkg gerado (${formatBytes(r.size)}).`, 'success');
    await loadThemes(th.current.id);
  } catch (e) { showToast('Falha ao gerar: ' + e.message, 'error', 9000); }
  btn.disabled = false;
  btn.innerHTML = '<svg><use href="#i-package"/></svg><span>Gerar .pkg</span>';
}

async function sendTheme() {
  if (!th.current) return;
  const btn = document.getElementById('te-send');
  btn.disabled = true;
  btn.innerHTML = '<span class="spinner small"></span> Enviando…';
  try {
    await postJson(`/api/themes/update?id=${encodeURIComponent(th.current.id)}`, themeForm());
    const r = await postJson(`/api/themes/send?id=${encodeURIComponent(th.current.id)}`, {}, { timeout: 600000 });
    showToast(`${r.rebuilt ? 'Pacote gerado de novo com as suas alterações e enviado' : 'Tema enviado'}. No PS4: apague a versão anterior em Temas, instale em Debug Settings › Package Installer e aplique.`, 'success', 12000);
    await loadThemes(th.current.id);
  } catch (e) { showToast('Falha ao enviar: ' + e.message, 'error', 9000); }
  btn.disabled = false;
  btn.innerHTML = '<svg><use href="#i-send"/></svg><span>Enviar ao PS4</span>';
}

async function createTheme() {
  const name = document.getElementById('theme-new-name').value.trim();
  try {
    const t = await postJson('/api/themes/create', { name });
    document.getElementById('theme-new-name').value = '';
    await loadThemes(t.id);
    showToast('Tema criado. Envie as imagens e escolha as cores.', 'success');
  } catch (e) { showToast(e.message, 'error'); }
}

async function deleteTheme() {
  if (!th.current || !confirm(`Apagar o tema "${th.current.name}" do PC? (Não desinstala do PS4.)`)) return;
  try {
    await postJson(`/api/themes/delete?id=${encodeURIComponent(th.current.id)}`, {});
    th.current = null;
    document.getElementById('theme-editor').hidden = true;
    loadThemes();
  } catch (e) { showToast(e.message, 'error'); }
}

async function uploadThemeImage(file) {
  if (!file || !th.current) return;
  try {
    const res = await fetch(`/api/themes/upload?id=${encodeURIComponent(th.current.id)}&slot=${th.slot}`, { method: 'POST', body: file });
    const d = await res.json();
    if (!res.ok) throw new Error(d.error || 'Falha ao enviar a imagem.');
    showToast('Imagem adicionada.', 'success');
    await loadThemes(th.current.id);
  } catch (e) { showToast(e.message, 'error'); }
}

document.addEventListener('DOMContentLoaded', () => {
  const on = (id, ev, fn) => { const el = document.getElementById(id); if (el) el.addEventListener(ev, fn); };
  on('btn-theme-create', 'click', createTheme);
  on('te-save', 'click', () => saveTheme(false));
  on('te-build', 'click', buildTheme);
  on('te-send', 'click', sendTheme);
  on('te-delete', 'click', deleteTheme);
  on('te-dim', 'input', (e) => { document.getElementById('te-dim-val').textContent = e.target.value; });
  on('te-shift', 'input', (e) => { document.getElementById('te-shift-val').textContent = e.target.value; });
  on('te-grade', 'input', (e) => { document.getElementById('te-grade-val').textContent = e.target.value; });
  // sliders and switches re-render the simulation once the user lets go
  ['te-dim', 'te-shift', 'te-grade', 'te-logo', 'te-logopos', 'te-accent', 'te-accent2'].forEach(id => on(id, 'change', () => saveTheme(true)));
  document.querySelectorAll('[data-te-view]').forEach(b => b.addEventListener('click', () => {
    th.view = b.dataset.teView;
    document.querySelectorAll('[data-te-view]').forEach(x => x.classList.toggle('active', x === b));
    refreshThemePreview();
  }));
  on('theme-file-input', 'change', (e) => { uploadThemeImage(e.target.files[0]); e.target.value = ''; });
  on('te-bgm-upload', 'click', () => document.getElementById('theme-bgm-input').click());
  on('theme-bgm-input', 'change', (e) => { th.slot = 'bgm'; uploadThemeImage(e.target.files[0]); e.target.value = ''; });
  on('te-bgm-remove', 'click', async () => {
    if (!th.current) return;
    try { await postJson(`/api/themes/update?id=${encodeURIComponent(th.current.id)}`, { removeBgm: true }); await loadThemes(th.current.id); showToast('Música removida. Gere o .pkg de novo.', 'success'); }
    catch (e) { showToast(e.message, 'error'); }
  });
  document.addEventListener('click', (e) => {
    const card = e.target.closest('[data-theme]');
    if (card) { openThemeEditor(card.dataset.theme); document.getElementById('theme-editor').scrollIntoView({ behavior: 'smooth', block: 'start' }); return; }
    const up = e.target.closest('[data-te-upload]');
    if (up) { th.slot = up.dataset.teUpload; document.getElementById('theme-file-input').click(); }
  });
  document.addEventListener('change', (e) => { if (e.target.closest('[data-te-slot]')) saveTheme(true); });
});
