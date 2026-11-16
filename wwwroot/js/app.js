// Orbis Web Manager • PlayStation 4 Web Suite
// Client-side Application Controller

const state = {
  currentTab: 'dashboard',
  currentPath: '/data/',
  games: [],
  cheats: [],
  pkgs: [],
  captures: [],
  captureGames: [],
  selectedCaptureGame: '',
  summary: null,
  config: null,
  detailId: null,
  status: null,
  localIps: [],
  lang: localStorage.getItem('owm_lang') || 'pt'
};

// ================= I18N (INTERNATIONALIZATION) =================
const translations = {
  pt: {
    nav_dashboard: "Dashboard",
    nav_games: "Jogos & Apps",
    nav_files: "Arquivos (FTP)",
    nav_cheats: "Cheats & Mods",
    nav_pkgs: "Pacotes (PKG)",
    nav_media: "Capturas & Prints",
    nav_tools: "Payloads & Ações",
    nav_settings: "Configurações",
    nav_donate: "Apoiar Projeto",

    topbar_connect_mobile: "Conectar Celular",
    topbar_quick_upload: "Upload Rápido",
    topbar_tutorial: "Tutorial",

    title_dashboard: "Dashboard",
    sub_dashboard: "Central de Controle do PlayStation 4",
    title_games: "Jogos & Aplicativos",
    sub_games: "Biblioteca instalada e estatísticas de jogo",
    title_files: "Gerenciador de Arquivos",
    sub_files: "Explorador FTP do sistema de arquivos",
    title_cheats: "Cheats & Mods",
    sub_cheats: "Gerenciador de Cheats do GoldHEN",
    title_pkgs: "Pacotes PKG",
    sub_pkgs: "Instalação e gerenciamento de arquivos .pkg",
    title_media: "Capturas & Prints",
    sub_media: "Screenshots gravados diretamente no console",
    title_tools: "Payloads & Ações",
    sub_tools: "Ferramentas do sistema, notificações OSD, RPI e injeção",
    title_settings: "Configurações",
    sub_settings: "Parâmetros de rede e conexão com o console",
    title_donate: "Apoiar o Projeto",
    sub_donate: "Contribua com o desenvolvimento contínuo do Orbis Web Manager",

    stat_console_status: "Status do Console",
    stat_active_profile: "Perfil Ativo",
    stat_installed_games: "Jogos Instalados",
    stat_storage_pkgs: "Armazenamento / PKGs",
    stat_connecting: "Conectando...",
    stat_online: "Online • Conectado",
    stat_offline: "Console Desconectado",
    stat_games_sub: "Títulos com metadados e capas",
    stat_cheats_avail: "Cheats Disponíveis",
    stat_pkgs_sub: "pacotes em /data/pkg/",
    stat_ready_install: "Pronto p/ Instalar",
    stat_fw_jailbreak: "FW: 9.00 / 11.00 • Jailbreak Ativo",

    quick_actions: "Ações Rápidas",
    act_upload_pkg_title: "Enviar PKG",
    act_upload_pkg_desc: "Upload direto para /data/pkg/",
    act_upload_cheat_title: "Novo Cheat JSON",
    act_upload_cheat_desc: "Enviar ou converter código",
    act_browse_files_title: "Explorar Disco",
    act_browse_files_desc: "Navegar pelas pastas do PS4",
    act_convert_cheat_title: "Conversor CUSA",
    act_convert_cheat_desc: "Adaptar cheat de outra região",

    tool_backup_title: "Backup de Emergência (app.db)",
    tool_backup_desc: "Baixe uma cópia de segurança imediata da base de dados de jogos e aplicativos do PS4 (/system_data/priv/mms/app.db). Essencial para restaurar sua biblioteca caso corrompa!",
    tool_backup_btn: "Baixar Backup do app.db (.db)",
    tool_notify_title: "Notificação na Tela da TV (Notify OSD)",
    tool_notify_desc: "Envie uma mensagem em tempo real para pipocar no canto da tela do PlayStation 4 enquanto estiver jogando ou navegando na dashboard.",
    tool_notify_ph: "Ex: Pizza chegou! / Backup concluído",
    tool_notify_btn: "Enviar Notificação para a TV",
    tool_rpi_title: "Instalador Remoto de Pacotes (GoldHEN RPI)",
    tool_rpi_desc: "Mande o PS4 instalar pacotes (.pkg) direto via rede HTTP (Porta 12800) em segundo plano, sem ocupar o dobro de espaço no HD.",
    tool_rpi_btn: "Disparar Instalação no PS4 (RPI)",
    tool_payload_title: "Injeção de Payload (.bin)",
    tool_payload_desc: "Envie arquivos de payload compilados diretamente para a porta 9090 (BinLoader Server do GoldHEN).",
    tool_payload_file_label: "Arquivo de Payload (.bin)",
    tool_payload_btn: "Enviar Payload para o PS4",
    tool_phone_title: "Conectar no Smartphone",
    tool_phone_desc: "Acesse o painel direto pelo navegador do seu celular (Chrome, Safari, etc.) na mesma rede Wi-Fi.",
    tool_phone_addr: "Endereço de Acesso Móvel:",

    settings_title: "Configurações de Conexão com o PlayStation 4",
    settings_sub: "Ajuste o IP e as portas de rede do console.",
    settings_ip: "Endereço de IP do PS4",
    settings_ftp: "Porta FTP (GoldHEN)",
    settings_payload: "Porta Payload (BinLoader)",
    settings_fw: "Versão do Firmware do PS4 (FW)",
    settings_active_user: "Perfil de Usuário Ativo",
    settings_user_auto: "Auto-detectar (Mais Recente)",
    settings_sgdb_key: "Chave de API do SteamGridDB (Opcional)",
    settings_save: "Salvar Configurações",

    donate_main_title: "Apoie o Desenvolvimento do Orbis Web Manager ❤️",
    donate_main_desc: "O Orbis Web Manager é um projeto 100% gratuito e open-source para a comunidade PlayStation 4 Jailbreak. Sua contribuição fortalece e acelera novas funções!",
    donate_pix_badge: "Brasil • Pix Instantâneo",
    donate_pix_title: "Doação via Pix",
    donate_pix_desc: "Escaneie o QR Code abaixo com o aplicativo do seu banco ou use a chave aleatória:",
    donate_pix_key_label: "Chave Aleatória:",
    donate_pix_copy_btn: "Copiar Chave Pix",
    donate_bmac_badge: "Internacional",
    donate_bmac_title: "Buy Me a Coffee",
    donate_bmac_desc: "Apoie com um café para impulsionar novos recursos, cheats atualizados e compatibilidade contínua:",
    donate_bmac_strong: "Apoio Internacional",
    donate_bmac_methods: "Cartão, PayPal, Apple Pay",
    donate_bmac_btn: "Página Buy Me a Coffee",
    donate_community_title: "Participe da Comunidade Open Source",
    donate_community_desc: "Deixe uma ⭐ estrela no repositório GitHub para aumentar a visibilidade do projeto na cena PlayStation 4!",
    donate_github_btn: "Ver Repositório no GitHub"
  },
  en: {
    nav_dashboard: "Dashboard",
    nav_games: "Games & Apps",
    nav_files: "Files (FTP)",
    nav_cheats: "Cheats & Mods",
    nav_pkgs: "Packages (PKG)",
    nav_media: "Screenshots & Media",
    nav_tools: "Payloads & Actions",
    nav_settings: "Settings",
    nav_donate: "Support Project",

    topbar_connect_mobile: "Connect Phone",
    topbar_quick_upload: "Quick Upload",
    topbar_tutorial: "Tutorial",

    title_dashboard: "Dashboard",
    sub_dashboard: "PlayStation 4 Central Command",
    title_games: "Games & Applications",
    sub_games: "Installed library and playtime statistics",
    title_files: "File Manager",
    sub_files: "PS4 filesystem FTP explorer",
    title_cheats: "Cheats & Mods",
    sub_cheats: "GoldHEN Cheats Manager",
    title_pkgs: "PKG Packages",
    sub_pkgs: "Installation and management of .pkg files",
    title_media: "Screenshots & Media",
    sub_media: "Direct screenshots captured with the Share button",
    title_tools: "Payloads & Actions",
    sub_tools: "System utilities, OSD TV alerts, RPI and payloads",
    title_settings: "Settings",
    sub_settings: "Console network parameters and connection",
    title_donate: "Support the Project",
    sub_donate: "Contribute to continuous development of Orbis Web Manager",

    stat_console_status: "Console Status",
    stat_active_profile: "Active Profile",
    stat_installed_games: "Installed Games",
    stat_storage_pkgs: "Storage / PKGs",
    stat_connecting: "Connecting...",
    stat_online: "Online • Connected",
    stat_offline: "Console Offline",
    stat_games_sub: "Titles with covers & metadata",
    stat_cheats_avail: "Available Cheats",
    stat_pkgs_sub: "packages in /data/pkg/",
    stat_ready_install: "Ready to Install",
    stat_fw_jailbreak: "FW: 9.00 / 11.00 • Jailbreak Active",

    quick_actions: "Quick Actions",
    act_upload_pkg_title: "Upload PKG",
    act_upload_pkg_desc: "Direct upload to /data/pkg/",
    act_upload_cheat_title: "New Cheat JSON",
    act_upload_cheat_desc: "Upload or convert cheat code",
    act_browse_files_title: "Explore Disk",
    act_browse_files_desc: "Browse PS4 storage folders",
    act_convert_cheat_title: "CUSA Converter",
    act_convert_cheat_desc: "Adapt cheat from another region",

    tool_backup_title: "Emergency Backup (app.db)",
    tool_backup_desc: "Download an immediate full safety copy of the PS4 game & app database (/system_data/priv/mms/app.db). Essential to prevent losing your library if the database gets corrupted!",
    tool_backup_btn: "Download app.db Backup (.db)",
    tool_notify_title: "TV Screen Notification (Notify OSD)",
    tool_notify_desc: "Send a live message that pops up on your PlayStation 4 TV screen while playing games or browsing the dashboard.",
    tool_notify_ph: "e.g., Pizza arrived! / Backup finished",
    tool_notify_btn: "Send Notification to TV",
    tool_rpi_title: "Remote Package Installer (GoldHEN RPI)",
    tool_rpi_desc: "Command your PS4 to install .pkg packages directly over HTTP (Port 12800) in the background without needing double storage space.",
    tool_rpi_btn: "Trigger PS4 Installation (RPI)",
    tool_payload_title: "Payload Injection (.bin)",
    tool_payload_desc: "Send compiled .bin payload files straight to port 9090 (GoldHEN BinLoader Server).",
    tool_payload_file_label: "Payload File (.bin)",
    tool_payload_btn: "Send Payload to PS4",
    tool_phone_title: "Connect via Smartphone",
    tool_phone_desc: "Access this dashboard directly from your phone's browser (Chrome, Safari, etc.) on the same Wi-Fi network.",
    tool_phone_addr: "Mobile Access Address:",

    settings_title: "PlayStation 4 Network Connection Settings",
    settings_sub: "Adjust the console IP address and port configuration.",
    settings_ip: "PS4 IP Address",
    settings_ftp: "FTP Port (GoldHEN)",
    settings_payload: "Payload Port (BinLoader)",
    settings_fw: "PS4 Firmware Version (FW)",
    settings_active_user: "Active User Profile",
    settings_user_auto: "Auto-detect (Most Recent)",
    settings_sgdb_key: "SteamGridDB API Key (Optional)",
    settings_save: "Save Settings",

    donate_main_title: "Support Orbis Web Manager Development ❤️",
    donate_main_desc: "Orbis Web Manager is 100% free and open-source for the PlayStation 4 Jailbreak community. Your support empowers and accelerates new features!",
    donate_pix_badge: "Brazil • Instant Pix",
    donate_pix_title: "Donation via Pix",
    donate_pix_desc: "Scan the QR Code below with your banking app or copy the random key:",
    donate_pix_key_label: "Random Pix Key:",
    donate_pix_copy_btn: "Copy Pix Key",
    donate_bmac_badge: "International",
    donate_bmac_title: "Buy Me a Coffee",
    donate_bmac_desc: "Support with a coffee to boost new features, updated cheats, and continuous platform compatibility:",
    donate_bmac_strong: "International Support",
    donate_bmac_methods: "Card, PayPal, Apple Pay",
    donate_bmac_btn: "Buy Me a Coffee Page",
    donate_community_title: "Join the Open Source Community",
    donate_community_desc: "Drop a ⭐ star on the GitHub repository to boost the project's visibility across the PlayStation 4 community!",
    donate_github_btn: "View GitHub Repository"
  }
};

function initI18n() {
  setLanguage(state.lang);
}

function setLanguage(lang) {
  state.lang = lang;
  localStorage.setItem('owm_lang', lang);

  const flagEl = document.getElementById('lang-flag');
  const codeEl = document.getElementById('lang-code');
  if (flagEl && codeEl) {
    if (lang === 'en') {
      flagEl.textContent = '🇧🇷';
      codeEl.textContent = 'PT';
    } else {
      flagEl.textContent = '🇺🇸';
      codeEl.textContent = 'EN';
    }
  }

  const dict = translations[lang] || translations.pt;
  document.querySelectorAll('[data-i18n]').forEach(el => {
    const key = el.getAttribute('data-i18n');
    if (dict[key]) el.textContent = dict[key];
  });

  document.querySelectorAll('[data-i18n-ph]').forEach(el => {
    const key = el.getAttribute('data-i18n-ph');
    if (dict[key]) el.setAttribute('placeholder', dict[key]);
  });

  updateTabTitles(state.currentTab);
}

// ================= HELPERS =================
const esc = (s) => String(s === undefined || s === null ? '' : s).replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));

async function api(url, opts = {}) {
  const ctrl = new AbortController();
  const timer = setTimeout(() => ctrl.abort(), opts.timeout || 90000);
  try {
    const res = await fetch(url, { ...opts, signal: ctrl.signal });
    const ct = res.headers.get('content-type') || '';
    const data = ct.includes('json') ? await res.json() : null;
    if (!res.ok) throw new Error((data && data.error) || `Erro HTTP ${res.status}`);
    return data;
  } catch (e) {
    if (e.name === 'AbortError') throw new Error('O console demorou demais para responder.');
    throw e;
  } finally {
    clearTimeout(timer);
  }
}

function postJson(url, body, opts = {}) {
  return api(url, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body || {}), ...opts });
}

function fmtPlaytime(min) {
  min = min || 0;
  const h = Math.floor(min / 60), m = min % 60;
  return h > 0 ? `${h}h ${m}m` : `${m}m`;
}

const CATEGORY_NAMES = { gd: 'Jogo', gde: 'App', gdc: 'App', gp: 'Jogo + atualização', ac: 'DLC', bd: 'Blu-ray', gdk: 'App' };
const PLACEHOLDER_ICON = "data:image/svg+xml," + encodeURIComponent("<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 100 100'><rect width='100' height='100' fill='#111827'/><text x='50' y='55' fill='#64748b' font-size='11' font-family='sans-serif' text-anchor='middle'>Sem Capa</text></svg>");

// Broken images get a placeholder (delegated, works for dynamically created <img>)
document.addEventListener('error', (e) => {
  const img = e.target;
  if (img && img.tagName === 'IMG' && img.dataset.icon !== undefined && !img.classList.contains('noimg')) {
    img.classList.add('noimg');
    img.src = PLACEHOLDER_ICON;
  }
}, true);

// ================= INITIALIZATION =================
document.addEventListener('DOMContentLoaded', () => {
  initI18n();
  initNavigation();
  initDragAndDrop();
  initModals();
  initForms();
  initActions();

  fetchStatus();
  fetchSummary();
  fetchGames();
  fetchNetworkInfo();

  // Light polling, only while the tab is visible
  setInterval(() => { if (document.visibilityState === 'visible') fetchStatus(); }, 8000);
  setInterval(() => { if (document.visibilityState === 'visible') fetchSummary(); }, 30000);
  document.addEventListener('visibilitychange', () => { if (document.visibilityState === 'visible') fetchStatus(); });

  if (!localStorage.getItem('owm_tour_completed')) {
    setTimeout(startTour, 1200);
  }
});

// ================= NAVIGATION =================
function initNavigation() {
  document.querySelectorAll('.nav-item, .mobile-nav-item').forEach(item => {
    item.addEventListener('click', () => {
      switchTab(item.getAttribute('data-tab'));
      document.body.classList.remove('sidebar-open');
    });
  });

  document.addEventListener('click', (e) => {
    const trg = e.target.closest('[data-tab-trigger]');
    if (trg) {
      e.preventDefault();
      switchTab(trg.getAttribute('data-tab-trigger'));
    }
    if (document.body.classList.contains('sidebar-open') && !e.target.closest('.sidebar') && !e.target.closest('#mobile-menu-toggle')) {
      document.body.classList.remove('sidebar-open');
    }
  });

  ['mobile-menu-toggle', 'mobile-more'].forEach(id => {
    const b = document.getElementById(id);
    if (b) b.addEventListener('click', (e) => { e.stopPropagation(); document.body.classList.toggle('sidebar-open'); });
  });

  document.getElementById('dash-port-payload').addEventListener('click', runHealthCheck);
  document.getElementById('btn-banner-retry').addEventListener('click', () => { fetchStatus(); fetchSummary(true); fetchGames('refresh'); });
  if (!sessionStorage.getItem('owm_health_done')) setTimeout(runHealthCheck, 1500);

  // Load ps4debug (needed for TV notifications)
  document.getElementById('btn-load-ps4debug').addEventListener('click', async (e) => {
    const btn = e.currentTarget;
    btn.disabled = true;
    showToast('Baixando o ps4debug oficial e enviando ao PS4…', 'info');
    try {
      const d = await postJson('/api/system/ps4debug', {}, { timeout: 60000 });
      showToast(d.message, 'success', 7000);
    } catch (err) {
      showToast(err.message, 'error', 9000);
    } finally {
      btn.disabled = false;
    }
  });

  document.getElementById('action-upload-pkg').addEventListener('click', () => document.getElementById('pkg-file-input').click());
  document.getElementById('action-upload-cheat').addEventListener('click', () => document.getElementById('cheat-file-input').click());
  document.getElementById('action-browse-files').addEventListener('click', () => switchTab('files'));
  document.getElementById('action-convert-cheat').addEventListener('click', () => openModal('modal-converter'));
  document.getElementById('btn-quick-upload').addEventListener('click', () => document.getElementById('generic-file-input').click());
  document.getElementById('btn-qr-mobile').addEventListener('click', () => openModal('modal-qr'));

  document.getElementById('btn-refresh-games').addEventListener('click', (e) => {
    const deep = e.shiftKey;
    fetchGames(deep ? 'deep' : 'refresh');
    showToast(deep ? 'Atualização completa iniciada (nomes e capas).' : 'Atualizando biblioteca...', 'info');
  });

  document.getElementById('btn-refresh-captures').addEventListener('click', () => {
    state.captureGames = [];
    loadCaptures(state.selectedCaptureGame, true);
  });

  const btnCopyPix = document.getElementById('btn-copy-pix');
  if (btnCopyPix) {
    btnCopyPix.addEventListener('click', () => {
      const pixKey = '94af8068-d112-4c69-bcfa-8789b233246e';
      if (navigator.clipboard && navigator.clipboard.writeText) {
        navigator.clipboard.writeText(pixKey)
          .then(() => showToast('Chave Pix copiada com sucesso! Obrigado pelo apoio! ❤️', 'success'))
          .catch(() => showToast(`Chave Pix: ${pixKey}`, 'info'));
      } else {
        showToast(`Chave Pix: ${pixKey}`, 'info');
      }
    });
  }

  // Notification to the TV (ps4debug)
  document.getElementById('btn-send-notify').addEventListener('click', async (e) => {
    const btn = e.currentTarget;
    const msg = document.getElementById('notify-msg-input').value.trim();
    if (!msg) return showToast('Digite uma mensagem para a notificação.', 'error');
    btn.disabled = true;
    try {
      const d = await postJson('/api/system/notify', { message: msg });
      if (d.success) showToast('Notificação enviada para a tela do PS4!', 'success');
      else showToast(d.error || 'Falha ao enviar notificação.', 'error', 8000);
    } catch (err) {
      showToast(err.message, 'error');
    } finally {
      btn.disabled = false;
    }
  });

  // RPI with URL
  document.getElementById('btn-trigger-rpi').addEventListener('click', async () => {
    const url = document.getElementById('rpi-pkg-url').value.trim();
    if (!url) return showToast('Informe a URL do arquivo .pkg na rede.', 'error');
    await startRpiInstall({ url });
  });

  document.getElementById('btn-test-ports').addEventListener('click', testPorts);
  document.getElementById('btn-refresh-pcpkgs').addEventListener('click', loadPcPkgs);

  const btnLang = document.getElementById('btn-lang-toggle');
  if (btnLang) {
    btnLang.addEventListener('click', () => {
      const nextLang = state.lang === 'pt' ? 'en' : 'pt';
      setLanguage(nextLang);
      showToast(nextLang === 'en' ? 'Language switched to English!' : 'Idioma alterado para Português!', 'info');
    });
  }

  const btnTour = document.getElementById('btn-tour-start');
  if (btnTour) btnTour.addEventListener('click', startTour);
  const tourCloseBtn = document.getElementById('tour-close-btn');
  if (tourCloseBtn) tourCloseBtn.addEventListener('click', closeTour);
  const tourBackdrop = document.getElementById('tour-backdrop');
  if (tourBackdrop) tourBackdrop.addEventListener('click', closeTour);
  const tourPrevBtn = document.getElementById('tour-prev-btn');
  if (tourPrevBtn) tourPrevBtn.addEventListener('click', () => showTourStep(currentTourStep - 1));
  const tourNextBtn = document.getElementById('tour-next-btn');
  if (tourNextBtn) tourNextBtn.addEventListener('click', () => showTourStep(currentTourStep + 1));

  // Cover search
  document.getElementById('btn-cover-search-exec').addEventListener('click', () => runCoverSearch());
  document.getElementById('cover-search-input').addEventListener('keydown', (e) => { if (e.key === 'Enter') runCoverSearch(); });
  document.getElementById('sgdb-game-select').addEventListener('change', loadSgdbArt);
  document.getElementById('sgdb-kind-select').addEventListener('change', loadSgdbArt);
  document.querySelectorAll('#cover-target-seg .seg-btn').forEach(b => b.addEventListener('click', () => setCoverTarget(b.dataset.value)));
  document.querySelectorAll('#cover-fit-seg .seg-btn').forEach(b => b.addEventListener('click', () => {
    state.cover.fit = b.dataset.value;
    document.querySelectorAll('#cover-fit-seg .seg-btn').forEach(x => x.classList.toggle('active', x === b));
  }));
  document.getElementById('btn-cover-confirm').addEventListener('click', confirmCoverApply);
}

function updateTabTitles(tabId) {
  const d = translations[state.lang] || translations.pt;
  const titleMap = {
    dashboard: { title: d.title_dashboard, sub: d.sub_dashboard },
    games: { title: d.title_games, sub: d.sub_games },
    files: { title: d.title_files, sub: d.sub_files },
    cheats: { title: d.title_cheats, sub: d.sub_cheats },
    pkgs: { title: d.title_pkgs, sub: d.sub_pkgs },
    media: { title: d.title_media, sub: d.sub_media },
    tools: { title: d.title_tools, sub: d.sub_tools },
    settings: { title: d.title_settings, sub: d.sub_settings },
    donate: { title: d.title_donate, sub: d.sub_donate }
  };
  const t = titleMap[tabId] || titleMap.dashboard;
  const titleEl = document.getElementById('page-title-text');
  const subEl = document.getElementById('page-subtitle-text');
  if (titleEl) titleEl.textContent = t.title;
  if (subEl) subEl.textContent = t.sub;
}

function switchTab(tabId) {
  state.currentTab = tabId;
  document.querySelectorAll('.nav-item, .mobile-nav-item').forEach(el => {
    const on = el.getAttribute('data-tab') === tabId;
    el.classList.toggle('active', on);
    if (on) el.setAttribute('aria-current', 'page'); else el.removeAttribute('aria-current');
  });
  document.querySelectorAll('.tab-pane').forEach(pane => {
    pane.classList.toggle('active', pane.id === `pane-${tabId}`);
  });
  updateTabTitles(tabId);

  if (tabId === 'files') loadFiles(state.currentPath);
  if (tabId === 'cheats') loadCheats();
  if (tabId === 'pkgs') { loadPkgs(); loadPcPkgs(); }
  if (tabId === 'media') loadCaptures(state.selectedCaptureGame);
  if (tabId === 'settings') loadConfig();
}

// ================= DELEGATED ACTIONS (dynamic lists) =================
function initActions() {
  // Keyboard support for card-like buttons (role="button")
  document.addEventListener('keydown', (e) => {
    if (e.key !== 'Enter' && e.key !== ' ') return;
    const el = e.target.closest && e.target.closest('[role="button"][data-action]');
    if (el && e.target === el) { e.preventDefault(); el.click(); }
  });

  document.addEventListener('click', (e) => {
    const el = e.target.closest('[data-action]');
    if (!el) return;
    const a = el.dataset.action;
    const p = el.dataset.path;
    switch (a) {
      case 'open-game': openGameDetails(el.dataset.id); break;
      case 'change-cover': e.stopPropagation(); triggerCoverChange(el.dataset.id, 'icon'); break;
      case 'open-dir': loadFiles(p); break;
      case 'delete': e.stopPropagation(); deleteItem(p, el.dataset.dir === 'true'); break;
      case 'rename': e.stopPropagation(); renameItem(p); break;
      case 'view-cheat': viewCheat(p); break;
      case 'capture-game': loadCaptures(el.dataset.id); break;
      case 'capture': openLightbox(el.dataset.full, el.dataset.title); break;
      case 'pc-install': startRpiInstall({ file: el.dataset.name }); break;
      case 'cover-pick': previewCover(el.dataset.url, el.dataset.target || state.cover.target); break;
      default: return;
    }
  });
}

// ================= API CALLS =================

// 1. Status (fast: ping + FTP keep-alive)
async function fetchStatus() {
  const setDots = (cls) => ['sidebar-dot', 'console-dot', 'ping-dot'].forEach(id => {
    const el = document.getElementById(id);
    if (el) el.className = `status-dot ${cls}`;
  });
  const banner = document.getElementById('conn-banner');
  try {
    const data = await api('/api/status', { timeout: 15000 });
    state.status = data;
    const healthy = data.online && data.ftpOk;

    const pingVal = document.querySelector('#ping-indicator .ping-val');
    pingVal.textContent = data.online ? (data.ping >= 0 ? `${data.ping} ms` : 'online') : 'offline';
    setDots(healthy ? 'online' : (data.online ? 'warn' : 'offline'));

    document.getElementById('ip-display').textContent = `${data.ip}:${data.ftpPort}`;
    document.getElementById('dash-online-status').textContent = healthy ? 'Conectado' : (data.online ? 'FTP do GoldHEN desligado' : 'Console desconectado');
    document.getElementById('dash-banner').textContent = data.transfer ? `Transferindo: ${data.transfer}…` : `${data.ip} · ${data.banner || 'GoldHEN FTP Server'}`;
    document.getElementById('dash-fw-badge').textContent = `FW ${data.fw || '?'} · ${data.goldhenVer || 'GoldHEN'}`;
    document.getElementById('dash-port-ftp').textContent = data.ftpOk ? `FTP ${data.ftpPort}${data.ftpBusy ? ' · ocupado' : ''}` : `FTP ${data.ftpPort} · inativo`;

    if (banner) {
      banner.hidden = healthy;
      if (!healthy) {
        document.getElementById('conn-banner-title').textContent = data.online
          ? 'O PS4 responde, mas o FTP do GoldHEN não'
          : `Não foi possível falar com o PS4 em ${data.ip}`;
        document.getElementById('conn-banner-text').textContent = data.online
          ? 'No menu do GoldHEN, ative "FTP Server" (porta ' + data.ftpPort + ') e tente de novo.'
          : 'Confira se o console está ligado com o GoldHEN ativo e se o IP em Configurações está correto.';
      }
    }
    document.querySelectorAll('.lan-warning').forEach(el => el.hidden = !!data.lanAccess);
  } catch (err) {
    document.querySelector('#ping-indicator .ping-val').textContent = 'sem servidor';
    setDots('offline');
    if (banner) {
      banner.hidden = false;
      document.getElementById('conn-banner-title').textContent = 'O programa Orbis Web Manager não está respondendo';
      document.getElementById('conn-banner-text').textContent = 'Verifique se a janela do OrbisManager (start.bat) ainda está aberta no PC.';
    }
  }
}

// Dashboard checklist: which console services work right now
const HEALTH_INFO = {
  2121: { label: 'Arquivos, jogos e capas', fix: 'Ative "FTP Server" no GoldHEN' },
  9090: { label: 'Envio de payloads', fix: 'Ative "BinLoader Server" no GoldHEN (ou reinicie o PS4)' },
  744: { label: 'Notificações na TV', fix: 'Use "Carregar ps4debug" em Payloads & Ações' },
  12800: { label: 'Instalar PKG pela rede', fix: 'Abra o app Remote Package Installer no PS4' }
};
async function runHealthCheck() {
  const box = document.getElementById('health-list');
  if (!box) return;
  box.innerHTML = `<div class="text-muted text-sm"><span class="spinner small"></span> Verificando…</div>`;
  try {
    const list = await api('/api/ports', { timeout: 20000 });
    sessionStorage.setItem('owm_health_done', '1');
    box.innerHTML = list.map(p => {
      const info = HEALTH_INFO[p.port] || { label: p.name, fix: '' };
      return `
        <div class="health-item ${p.open ? 'ok' : 'bad'}">
          <span class="status-dot"></span>
          <span class="grow">${esc(info.label)}<small>${p.open ? esc(p.name) + ' · porta ' + p.port : esc(info.fix)}</small></span>
          <span class="state">${p.open ? 'OK' : 'Indisponível'}</span>
        </div>`;
    }).join('');
  } catch (err) {
    box.innerHTML = `<div class="text-danger text-sm">${esc(err.message)}</div>`;
  }
}

// 2. Summary (users, cheats, pkgs) - cached on the server
async function fetchSummary(force) {
  try {
    const data = await api('/api/summary' + (force ? '?force=1' : ''), { timeout: 60000 });
    state.summary = data;
    document.getElementById('user-display').textContent = data.username || 'PlayStation 4';
    document.getElementById('dash-user').textContent = data.username || 'PlayStation 4';
    document.getElementById('dash-pkg-count').textContent = `${data.pkgCount || 0} pacotes em /data/pkg/`;
    document.getElementById('dash-pkg-size').textContent = formatBytes(data.totalPkgSize || 0);
    document.getElementById('pkgs-count-badge').textContent = data.pkgCount || 0;
    document.getElementById('cheats-count-badge').textContent = data.cheatCount || 0;
    const cheatsBadge = document.getElementById('dash-active-cheats');
    if (cheatsBadge) cheatsBadge.textContent = data.cheatCount || 0;

    const userSelect = document.getElementById('cfg-active-user');
    if (userSelect && data.users && data.users.length > 0) {
      const current = userSelect.value;
      userSelect.innerHTML = `<option value="">Auto-detectar (Mais Recente)</option>` +
        data.users.map(u => `<option value="${esc(u.name)}">${esc(u.name)} (${esc(u.id)})${u.isCurrent ? ' • ativo' : ''}</option>`).join('');
      userSelect.value = current || (state.config && state.config.active_user) || '';
    }
  } catch (err) { /* console offline: keep previous values */ }
}

// 3. Games (served from cache instantly, refreshed in background)
let gamesPollTimer = null;
async function fetchGames(mode) {
  const statusEl = document.getElementById('games-status');
  if (!state.games.length) {
    const c = document.getElementById('games-container');
    if (c) c.innerHTML = Array.from({ length: 12 }, () =>
      `<div aria-hidden="true"><div class="skeleton skeleton-cover"></div><div class="skeleton skeleton-line"></div><div class="skeleton skeleton-line short"></div></div>`).join('');
  }
  try {
    const q = mode === 'deep' ? '?deep=1' : (mode === 'refresh' ? '?refresh=1' : '');
    const data = await api('/api/games' + q, { timeout: 180000 });
    state.games = data.games || [];
    document.getElementById('dash-games-count').textContent = state.games.length;
    document.getElementById('games-count-badge').textContent = state.games.length;
    applyGamesView();
    renderRecentGames(state.games.slice(0, 8));

    if (statusEl) {
      if (data.refreshing) statusEl.innerHTML = `<span class="spinner small"></span>Atualizando com o console...`;
      else if (data.error) statusEl.innerHTML = `<span class="text-danger">Não foi possível atualizar: ${esc(data.error)}</span>`;
      else statusEl.textContent = data.updated ? `${state.games.length} títulos • atualizado às ${data.updated}` : '';
    }

    clearTimeout(gamesPollTimer);
    if (data.refreshing) gamesPollTimer = setTimeout(() => fetchGames(), 1500);
  } catch (err) {
    if (statusEl) statusEl.innerHTML = `<span class="text-danger">${esc(err.message)}</span>`;
    if (!state.games.length) {
      document.getElementById('games-container').innerHTML = `<div class="empty-state">Não foi possível ler a biblioteca. Verifique o IP do PS4 em Configurações e se o FTP do GoldHEN está ativo.</div>`;
      document.getElementById('dash-recent-games').innerHTML = `<span class="text-muted">Console não conectado.</span>`;
    }
  }
}

function applyGamesView() {
  const q = (document.getElementById('games-search-input').value || '').toLowerCase();
  const sort = document.getElementById('games-sort').value;
  let list = state.games.filter(g => !q || (g.name || '').toLowerCase().includes(q) || g.id.toLowerCase().includes(q));
  list = list.slice();
  if (sort === 'name') list.sort((a, b) => (a.name || '').localeCompare(b.name || ''));
  else if (sort === 'starts') list.sort((a, b) => b.gameStarts - a.gameStarts);
  else if (sort === 'recent') list.sort((a, b) => parseFtpDate(b.installedDate) - parseFtpDate(a.installedDate));
  renderGames(list);
}

function parseFtpDate(s) {
  const t = Date.parse(s || '');
  return isNaN(t) ? 0 : t;
}

function renderGames(games) {
  const container = document.getElementById('games-container');
  if (!container) return;
  if (games.length === 0) {
    container.innerHTML = `<div class="empty-state">Nenhum jogo encontrado.</div>`;
    return;
  }
  container.innerHTML = games.map(game => `
    <div class="game-card" data-action="open-game" data-id="${esc(game.id)}" tabindex="0" role="button" aria-label="${esc(game.name)}">
      <div class="game-cover-box">
        <img data-icon="${esc(game.id)}" src="${esc(game.iconUrl)}" alt="" loading="lazy">
        ${game.hasCheats ? `<span class="game-badge-cheat">CHEATS</span>` : ''}
        ${game.customCover ? `<span class="game-badge-custom" title="Capa personalizada (original guardado)">CUSTOM</span>` : ''}
        <button class="btn-change-cover" data-action="change-cover" data-id="${esc(game.id)}" title="Alterar a capa deste jogo">
          <svg viewBox="0 0 24 24" width="13" height="13" fill="none" stroke="currentColor" stroke-width="2"><path d="M23 19a2 2 0 01-2 2H3a2 2 0 01-2-2V8a2 2 0 012-2h4l2-3h6l2 3h4a2 2 0 012 2z"/><circle cx="12" cy="13" r="4"/></svg>
          <span>Trocar Capa</span>
        </button>
      </div>
      <div class="game-body">
        <div class="game-id-tag">${esc(game.id)}${game.version ? ` • v${esc(game.version)}` : ''}</div>
        <h4 class="game-title" title="${esc(game.name)}">${esc(game.name)}</h4>
        <div class="game-stats-row">
          <span>Tempo: <strong>${fmtPlaytime(game.timePlayedMinutes)}</strong></span>
          <span>Iniciado: <strong>${game.gameStarts}x</strong></span>
        </div>
      </div>
    </div>
  `).join('');
}

function renderRecentGames(games) {
  const container = document.getElementById('dash-recent-games');
  if (!container) return;
  if (games.length === 0) {
    container.innerHTML = `<span class="text-muted">Nenhum jogo encontrado.</span>`;
    return;
  }
  container.innerHTML = games.map(game => `
    <div class="game-card-mini" data-action="open-game" data-id="${esc(game.id)}" tabindex="0" role="button" aria-label="${esc(game.name)}">
      <div class="cover-wrap">
        <img data-icon="${esc(game.id)}" src="${esc(game.iconUrl)}" alt="" loading="lazy">
      </div>
      <div class="info">
        <div class="title" title="${esc(game.name)}">${esc(game.name)}</div>
        <div class="meta">
          <span>${esc(game.id)}</span>
          <span>${fmtPlaytime(game.timePlayedMinutes)}</span>
        </div>
      </div>
    </div>
  `).join('');
}

function refreshIconEverywhere(id, url) {
  document.querySelectorAll(`img[data-icon="${CSS.escape(id)}"]`).forEach(img => {
    img.classList.remove('noimg');
    img.src = url;
  });
  const g = state.games.find(x => x.id === id);
  if (g) g.iconUrl = url;
}

// ================= GAME DETAILS =================
async function openGameDetails(id) {
  const game = state.games.find(g => g.id === id) || { id, name: id };
  state.detailId = id;
  document.getElementById('gd-icon').src = game.iconUrl || '';
  document.getElementById('gd-icon').dataset.icon = id;
  document.getElementById('gd-title').textContent = game.name || id;
  document.getElementById('gd-sub').textContent = id;
  document.getElementById('gd-stats').innerHTML = `<div class="loading-state"><div class="spinner"></div></div>`;
  document.getElementById('gd-cheats').innerHTML = '';
  document.getElementById('gd-btn-download').href = game.iconUrl || '#';
  document.getElementById('gd-btn-download').setAttribute('download', `${id}_icon0.png`);
  document.getElementById('gd-btn-cover').onclick = () => triggerCoverChange(id, 'icon');
  document.getElementById('gd-btn-bg').onclick = () => triggerCoverChange(id, 'background');
  document.getElementById('gd-btn-files').onclick = () => { closeModal('modal-game'); switchTab('files'); loadFiles(`/user/appmeta/${id}/`); };
  const restoreBtn = document.getElementById('gd-btn-restore');
  restoreBtn.hidden = !game.customCover;
  restoreBtn.onclick = () => restoreCover(id);
  openModal('modal-game');

  try {
    const d = await api(`/api/game/details?id=${encodeURIComponent(id)}`);
    const stat = (label, value) => `<div class="gd-stat"><span>${label}</span><strong>${esc(value)}</strong></div>`;
    document.getElementById('gd-sub').textContent = [id, d.contentId].filter(Boolean).join(' • ');
    document.getElementById('gd-stats').innerHTML =
      stat('Tempo jogado', fmtPlaytime(game.timePlayedMinutes)) +
      stat('Inicializações', `${game.gameStarts || 0}x`) +
      stat('Versão', d.version || '—') +
      stat('Tipo', CATEGORY_NAMES[d.category] || d.category || '—') +
      stat('Jogo base', d.appSize ? formatBytes(d.appSize) : '—') +
      stat('Atualização', d.patchSize ? formatBytes(d.patchSize) : '—');
    restoreBtn.hidden = !d.customCover;
    document.getElementById('gd-btn-bg').hidden = !d.hasPic1;
    document.getElementById('gd-cheats').innerHTML = d.cheats && d.cheats.length
      ? `<div class="info-box"><strong>Cheats instalados:</strong> ${d.cheats.map(esc).join(', ')}</div>`
      : '';
  } catch (err) {
    document.getElementById('gd-stats').innerHTML = `<div class="text-danger">${esc(err.message)}</div>`;
  }
}

// ================= COVER CHANGER =================
state.cover = { id: null, title: '', target: 'icon', fit: 'cover', sgdbGames: [], pendingUrl: null, pendingFile: null };

function triggerCoverChange(gameId, target) {
  const g = state.games.find(x => x.id === gameId);
  openCoverSearchModal(gameId, g && g.name ? g.name : gameId, target || 'icon');
}

function setCoverTarget(target) {
  state.cover.target = target;
  document.querySelectorAll('#cover-target-seg .seg-btn').forEach(x => x.classList.toggle('active', x.dataset.value === target));
  const kind = document.getElementById('sgdb-kind-select');
  if (kind) {
    const desired = target === 'background' ? 'heroes' : 'square';
    if (kind.value !== desired && (kind.value === 'heroes' || target === 'background')) {
      kind.value = desired;
      loadSgdbArt();
    }
  }
  if (state.cover.lastSearch) renderSony(state.cover.lastSearch.sony);
}

function triggerLocalFileUpload() {
  const input = document.getElementById('cover-file-input');
  input.value = '';
  input.click();
}

function openCoverSearchModal(gameId, gameTitle, target) {
  state.cover.id = gameId;
  state.cover.title = gameTitle;
  state.cover.lastSearch = null;
  document.getElementById('modal-cover-title').textContent = `Trocar Arte • ${gameTitle}`;
  document.getElementById('modal-cover-sub').textContent = `Title ID: ${gameId}`;
  document.getElementById('cover-search-input').value = gameTitle;
  document.getElementById('sgdb-kind-select').value = target === 'background' ? 'heroes' : 'square';
  setCoverTarget(target || 'icon');
  openModal('modal-cover-search');
  runCoverSearch();
}

async function runCoverSearch() {
  const id = state.cover.id;
  const title = document.getElementById('cover-search-input').value.trim();
  const loading = document.getElementById('cover-search-loading');
  const results = document.getElementById('cover-search-results');
  loading.hidden = false;
  results.hidden = true;
  try {
    const data = await api(`/api/covers/search?id=${encodeURIComponent(id)}&title=${encodeURIComponent(title)}`);
    state.cover.lastSearch = data;
    renderSony(data.sony);

    const s = data.steamgriddb;
    const noKey = document.getElementById('sgdb-no-key-banner');
    const filters = document.getElementById('sgdb-filters');
    const badge = document.getElementById('sgdb-count-badge');
    if (!s.hasKey) {
      noKey.hidden = false;
      filters.hidden = true;
      badge.textContent = 'Requer chave';
      document.getElementById('sgdb-grid-container').innerHTML = '';
    } else {
      noKey.hidden = true;
      filters.hidden = !s.games.length;
      const sel = document.getElementById('sgdb-game-select');
      sel.innerHTML = s.games.map(g => `<option value="${esc(g.id)}">${esc(g.name)}${g.year ? ` (${esc(g.year)})` : ''}</option>`).join('');
      if (s.selectedGameId) sel.value = s.selectedGameId;
      if (s.error) {
        badge.textContent = 'Erro';
        document.getElementById('sgdb-grid-container').innerHTML = `<p class="text-danger text-sm" style="grid-column: 1 / -1;">${esc(s.error)}</p>`;
      } else if (!s.games.length) {
        badge.textContent = '0 encontradas';
        document.getElementById('sgdb-grid-container').innerHTML = `<p class="text-muted text-sm" style="grid-column: 1 / -1;">Nenhum jogo encontrado para "${esc(title)}". Tente um nome mais curto (ex: sem subtítulo).</p>`;
      } else if (document.getElementById('sgdb-kind-select').value === 'square') {
        renderSgdb(s.grids);
      } else {
        loadSgdbArt();
      }
    }
  } catch (err) {
    showToast('Erro ao buscar capas: ' + err.message, 'error');
  } finally {
    loading.hidden = true;
    results.hidden = false;
  }
}

function renderSony(sony) {
  const sec = document.getElementById('sony-cover-section');
  const row = document.getElementById('sony-cover-row');
  if (!sony || (!sony.icon && !sony.background)) { sec.hidden = true; return; }
  sec.hidden = false;
  let html = '';
  if (sony.icon) html += `
    <div class="sony-item">
      <img src="${esc(sony.icon)}" alt="">
      <div class="grow"><strong>Ícone oficial</strong><br><span class="text-muted">PlayStation Store</span></div>
      <button class="btn btn-primary btn-sm" data-action="cover-pick" data-target="icon" data-url="${esc(sony.icon)}">Usar como capa</button>
    </div>`;
  if (sony.background) html += `
    <div class="sony-item">
      <img class="wide" src="${esc(sony.background)}" alt="">
      <div class="grow"><strong>Fundo oficial</strong><br><span class="text-muted">1920×1080</span></div>
      <button class="btn btn-outline btn-sm" data-action="cover-pick" data-target="background" data-url="${esc(sony.background)}">Usar como fundo</button>
    </div>`;
  row.innerHTML = html;
}

async function loadSgdbArt() {
  const gameId = document.getElementById('sgdb-game-select').value;
  const kind = document.getElementById('sgdb-kind-select').value;
  const container = document.getElementById('sgdb-grid-container');
  if (!gameId) return;
  container.innerHTML = `<div class="loading-state"><div class="spinner"></div></div>`;
  try {
    const d = await api(`/api/covers/sgdb?gameId=${encodeURIComponent(gameId)}&kind=${encodeURIComponent(kind)}`);
    renderSgdb(d.grids || [], kind);
  } catch (err) {
    container.innerHTML = `<p class="text-danger text-sm" style="grid-column: 1 / -1;">${esc(err.message)}</p>`;
  }
}

function renderSgdb(grids, kind) {
  kind = kind || document.getElementById('sgdb-kind-select').value;
  const container = document.getElementById('sgdb-grid-container');
  document.getElementById('sgdb-count-badge').textContent = `${grids.length} encontradas`;
  container.classList.toggle('wide', kind === 'heroes');
  if (!grids.length) {
    container.innerHTML = `<p class="text-muted text-sm" style="grid-column: 1 / -1;">Nenhuma arte deste tipo. Tente outro tipo no filtro acima ou outro jogo da lista.</p>`;
    return;
  }
  container.innerHTML = grids.map(g => {
    const ratio = g.width && g.height ? g.width / g.height : 1;
    const shape = ratio > 1.6 ? 'wide' : (ratio < 0.85 ? 'tall' : '');
    return `
      <div class="sgdb-card ${shape}" data-action="cover-pick" data-url="${esc(g.url)}" title="Clique para pré-visualizar">
        <div class="art"><img src="${esc(g.thumb || g.url)}" alt="" loading="lazy"></div>
        <div class="meta"><span class="text-truncate">${esc(g.author || 'Comunidade')}</span><span>${g.width}×${g.height}</span></div>
      </div>`;
  }).join('');
}

async function previewCover(url, target) {
  state.cover.pendingUrl = url;
  state.cover.pendingFile = null;
  state.cover.pendingTarget = target;
  const img = document.getElementById('cover-preview-img');
  document.getElementById('cover-preview-desc').textContent = target === 'background'
    ? 'Assim ficará o fundo (pic1) no PS4:' : 'Assim ficará a capa no PS4 (512×512):';
  img.src = '';
  openModal('modal-cover-preview');
  try {
    const res = await fetch('/api/covers/preview', {
      method: 'POST', headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ url, target, fit: state.cover.fit })
    });
    if (!res.ok) {
      const d = await res.json().catch(() => ({}));
      throw new Error(d.error || 'Falha ao gerar pré-visualização');
    }
    img.src = URL.createObjectURL(await res.blob());
  } catch (err) {
    closeModal('modal-cover-preview');
    showToast(err.message, 'error');
  }
}

async function confirmCoverApply() {
  const { id, pendingUrl, pendingFile, pendingTarget, fit } = state.cover;
  const btn = document.getElementById('btn-cover-confirm');
  btn.disabled = true;
  showToast('Enviando arte para o PS4...', 'info');
  try {
    let d;
    if (pendingFile) {
      d = await api(`/api/game/icon/upload?id=${encodeURIComponent(id)}&target=${encodeURIComponent(pendingTarget)}&fit=${encodeURIComponent(fit)}`, { method: 'POST', body: pendingFile });
    } else {
      d = await postJson('/api/covers/apply', { id, url: pendingUrl, target: pendingTarget, fit });
    }
    closeModal('modal-cover-preview');
    closeModal('modal-cover-search');
    showToast(pendingTarget === 'background'
      ? 'Fundo aplicado! Reinicie o PS4 se ele não mudar na hora.'
      : 'Capa aplicada! Reinicie o PS4 se ela não mudar na hora.', 'success', 6000);
    refreshIconEverywhere(id, d.iconUrl);
    const g = state.games.find(x => x.id === id);
    if (g) g.customCover = true;
    applyGamesView();
    if (state.detailId === id && document.getElementById('modal-game').classList.contains('active')) openGameDetails(id);
  } catch (err) {
    showToast('Falha ao aplicar: ' + err.message, 'error', 7000);
  } finally {
    btn.disabled = false;
  }
}

async function restoreCover(id) {
  if (!confirm('Restaurar a capa original deste jogo?')) return;
  try {
    const d = await postJson('/api/covers/restore', { id, target: 'icon' });
    try { await postJson('/api/covers/restore', { id, target: 'background' }); } catch (e) { /* no background backup */ }
    showToast('Arte original restaurada!', 'success');
    refreshIconEverywhere(id, d.iconUrl);
    const g = state.games.find(x => x.id === id);
    if (g) g.customCover = false;
    applyGamesView();
    openGameDetails(id);
  } catch (err) {
    showToast(err.message, 'error');
  }
}

// ================= FILE EXPLORER =================
async function loadFiles(dirPath) {
  if (!dirPath.endsWith('/')) dirPath += '/';
  state.currentPath = dirPath;
  const tbody = document.getElementById('file-table-body');
  tbody.innerHTML = `<tr><td colspan="4" class="p-4 text-center"><span class="spinner small"></span>Carregando...</td></tr>`;
  renderBreadcrumb(dirPath);

  try {
    const data = await api(`/api/files?path=${encodeURIComponent(dirPath)}`);
    if (state.currentPath !== dirPath) return; // user navigated away meanwhile
    const items = data.items || [];
    if (items.length === 0) {
      tbody.innerHTML = `<tr><td colspan="4" class="p-4 text-center text-muted">Pasta vazia</td></tr>`;
      return;
    }
    tbody.innerHTML = items.map(item => {
      const fullPath = dirPath + item.name;
      const p = esc(item.isDir ? fullPath + '/' : fullPath);
      return `
        <tr>
          <td>
            <div class="file-name-cell" ${item.isDir ? `data-action="open-dir" data-path="${p}"` : ''}>
              <svg viewBox="0 0 24 24" width="18" height="18" fill="none" stroke-width="2" class="file-icon ${item.isDir ? 'folder' : ''}">
                ${item.isDir ? '<path d="M3 7v10a2 2 0 002 2h14a2 2 0 002-2V9a2 2 0 00-2-2h-6l-2-2H5a2 2 0 00-2 2z"/>' : '<path d="M14 2H6a2 2 0 00-2 2v16a2 2 0 002 2h12a2 2 0 002-2V8zM14 2v6h6"/>'}
              </svg>
              <span>${esc(item.name)}</span>
            </div>
          </td>
          <td class="col-size">${item.isDir ? '--' : formatBytes(item.size)}</td>
          <td class="col-date">${esc(item.date)}</td>
          <td class="col-actions">
            ${!item.isDir ? `
              <a href="/api/files/download?path=${encodeURIComponent(fullPath)}" class="btn btn-sm btn-outline" title="Baixar">
                <svg viewBox="0 0 24 24" width="14" height="14" fill="none" stroke="currentColor" stroke-width="2"><path d="M4 16v1a3 3 0 003 3h10a3 3 0 003-3v-1m-4-4l-4 4m0 0l-4-4m4 4V4"/></svg>
              </a>` : ''}
            <button class="btn btn-sm btn-outline" data-action="rename" data-path="${esc(fullPath)}" title="Renomear">
              <svg viewBox="0 0 24 24" width="14" height="14" fill="none" stroke="currentColor" stroke-width="2"><path d="M11 4H4a2 2 0 00-2 2v14a2 2 0 002 2h14a2 2 0 002-2v-7"/><path d="M18.5 2.5a2.121 2.121 0 013 3L12 15l-4 1 1-4 9.5-9.5z"/></svg>
            </button>
            <button class="btn btn-sm btn-danger" data-action="delete" data-path="${esc(fullPath)}" data-dir="${item.isDir}" title="Excluir">
              <svg viewBox="0 0 24 24" width="14" height="14" fill="none" stroke="currentColor" stroke-width="2"><path d="M19 7l-.867 12.142A2 2 0 0116.138 21H7.862a2 2 0 01-1.995-1.858L5 7m5 4v6m4-6v6m1-10V4a1 1 0 00-1-1h-4a1 1 0 00-1 1v3M4 7h16"/></svg>
            </button>
          </td>
        </tr>`;
    }).join('');
  } catch (err) {
    tbody.innerHTML = `<tr><td colspan="4" class="p-4 text-center text-danger">Erro ao listar: ${esc(err.message)}</td></tr>`;
  }
}

function renderBreadcrumb(dirPath) {
  const container = document.getElementById('file-breadcrumb');
  const parts = dirPath.split('/').filter(p => p.length > 0);
  let html = `<span class="crumb" data-action="open-dir" data-path="/">PS4</span>`;
  let accum = '/';
  for (const part of parts) {
    accum += part + '/';
    html += ` / <span class="crumb" data-action="open-dir" data-path="${esc(accum)}">${esc(part)}</span>`;
  }
  container.innerHTML = html;
  document.querySelectorAll('.path-chip').forEach(chip => {
    chip.classList.toggle('active', chip.getAttribute('data-path') === dirPath);
  });
}

async function renameItem(path) {
  const oldName = path.split('/').pop();
  const newName = prompt('Novo nome:', oldName);
  if (!newName || newName === oldName) return;
  if (newName.includes('/')) return showToast('O nome não pode conter "/".', 'error');
  const to = path.substring(0, path.length - oldName.length) + newName;
  try {
    await api(`/api/files/rename?from=${encodeURIComponent(path)}&to=${encodeURIComponent(to)}`, { method: 'POST' });
    showToast('Renomeado!', 'success');
    loadFiles(state.currentPath);
  } catch (err) {
    showToast('Falha ao renomear: ' + err.message, 'error');
  }
}

// ================= CHEATS =================
async function loadCheats() {
  const container = document.getElementById('cheats-container');
  container.innerHTML = `<div class="loading-state"><div class="spinner"></div><span>Carregando cheats instalados...</span></div>`;
  try {
    const cheats = await api('/api/cheats');
    // Cheats for installed games first
    cheats.sort((a, b) => (b.installed - a.installed) || (a.gameName || '').localeCompare(b.gameName || ''));
    state.cheats = cheats;
    document.getElementById('cheats-count-badge').textContent = cheats.length;
    if (cheats.length === 0) {
      container.innerHTML = `<div class="empty-state">Nenhum cheat encontrado em /data/GoldHEN/cheats/json/.</div>`;
      return;
    }
    renderCheatsList(cheats);
  } catch (err) {
    container.innerHTML = `<div class="empty-state text-danger">Erro ao carregar cheats: ${esc(err.message)}</div>`;
  }
}

function renderCheatsList(cheats) {
  const container = document.getElementById('cheats-container');
  const MAX = 300;
  const shown = cheats.slice(0, MAX);
  container.innerHTML = shown.map(c => `
    <div class="cheat-card" data-action="view-cheat" data-path="${esc(c.path)}">
      <div class="c-top">
        <div>
          <div class="c-id">${esc(c.titleId)} • v${esc(c.version)}${c.installed ? ' • <span class="text-success">instalado</span>' : ''}</div>
          <div class="c-title">${esc(c.gameName)}</div>
        </div>
        <span class="badge cheat">JSON</span>
      </div>
      <div class="c-foot">
        <span class="text-truncate">${esc(c.filename)}</span>
        <span>${formatBytes(c.size)}</span>
      </div>
    </div>
  `).join('') + (cheats.length > MAX ? `<div class="empty-state">Mostrando ${MAX} de ${cheats.length}. Use a busca para filtrar.</div>` : '');
}

async function viewCheat(path) {
  const c = state.cheats.find(x => x.path === path) || { titleId: '', gameName: path, version: '' };
  try {
    const json = await api(`/api/cheat/read?path=${encodeURIComponent(path)}`);
    const data = json.data;
    document.getElementById('cheat-view-title').textContent = c.gameName || c.titleId;
    document.getElementById('cheat-view-meta').innerHTML = `
      <span class="badge-ps4">${esc(c.titleId)}</span>
      <span class="badge outline">Versão: ${esc(c.version)}</span>
      <span class="badge outline">Processo: ${esc(data && data.process ? data.process : 'eboot.bin')}</span>
      ${data && Array.isArray(data.credits) ? `<span class="badge">Créditos: ${esc(data.credits.join(', '))}</span>` : ''}
    `;
    const modsList = document.getElementById('cheat-view-mods');
    if (data && Array.isArray(data.mods) && data.mods.length > 0) {
      modsList.innerHTML = data.mods.map(mod => `
        <div class="mod-item">
          <div class="mod-head">
            <strong>${esc(mod.name)}</strong>
            <span class="badge cheat">${esc(mod.type)}</span>
          </div>
          <div class="mod-offsets">
            Offsets: ${esc(Array.isArray(mod.memory) ? mod.memory.map(m => m.offset).join(', ') : 'N/A')}
          </div>
        </div>
      `).join('');
    } else {
      modsList.innerHTML = `<pre class="code-block">${esc(json.raw)}</pre>`;
    }
    openModal('modal-cheat-view');
  } catch (err) {
    showToast(`Erro ao ler cheat: ${err.message}`, 'error');
  }
}

// ================= PKGs =================
async function loadPkgs() {
  const container = document.getElementById('pkgs-container');
  container.innerHTML = `<div class="loading-state"><div class="spinner"></div><span>Listando /data/pkg/...</span></div>`;
  try {
    const pkgs = await api('/api/pkgs');
    state.pkgs = pkgs;
    if (pkgs.length === 0) {
      container.innerHTML = `<div class="empty-state">Nenhum .pkg em /data/pkg/. Use o botão acima para enviar.</div>`;
      return;
    }
    container.innerHTML = pkgs.map(pkg => `
      <div class="pkg-card">
        <div class="pkg-title">${esc(pkg.name)}</div>
        <div class="pkg-meta">
          <span>Tamanho: <strong>${formatBytes(pkg.size)}</strong></span>
          <span>${esc(pkg.date)}</span>
        </div>
        <div class="pkg-actions">
          <a href="/api/files/download?path=${encodeURIComponent(pkg.path)}" class="btn btn-sm btn-outline flex-1 text-center">Baixar</a>
          <button class="btn btn-sm btn-danger" data-action="delete" data-path="${esc(pkg.path)}" data-dir="false">Excluir</button>
        </div>
      </div>
    `).join('');
  } catch (err) {
    container.innerHTML = `<div class="empty-state text-danger">Erro ao carregar PKGs: ${esc(err.message)}</div>`;
  }
}

async function loadPcPkgs() {
  const info = document.getElementById('pcpkgs-info');
  const container = document.getElementById('pcpkgs-container');
  try {
    const d = await api('/api/pc/pkgs');
    if (!d.folder) {
      info.innerHTML = `Configure a <strong>pasta de PKGs do PC</strong> em <a href="#" class="link" data-tab-trigger="settings">Configurações</a> para listar os arquivos aqui.`;
      container.innerHTML = '';
      return;
    }
    if (!d.exists) {
      info.innerHTML = `A pasta <code>${esc(d.folder)}</code> não existe neste PC.`;
      container.innerHTML = '';
      return;
    }
    info.innerHTML = (d.lanAccess
      ? `Pasta: <code>${esc(d.folder)}</code> • o PS4 vai baixar de <code>${esc(d.serverUrl)}</code>. Deixe o <strong>Remote Package Installer</strong> aberto no PS4.`
      : `<span style="color:var(--warning)">O acesso pela rede está desativado — o PS4 não conseguirá baixar. Execute <code>liberar-rede.bat</code> uma vez e reabra o programa.</span>`);
    container.innerHTML = d.files.length ? d.files.map(f => `
      <div class="pkg-card">
        <div class="pkg-title">${esc(f.name)}</div>
        <div class="pkg-meta">
          <span>Tamanho: <strong>${formatBytes(f.size)}</strong></span>
          <span>${esc(f.date)}</span>
        </div>
        <button class="btn btn-sm btn-primary mt-2" data-action="pc-install" data-name="${esc(f.name)}" ${d.lanAccess ? '' : 'disabled'}>Instalar no PS4</button>
      </div>
    `).join('') : `<div class="empty-state">Nenhum .pkg nesta pasta.</div>`;
  } catch (err) {
    info.innerHTML = `<span class="text-danger">${esc(err.message)}</span>`;
  }
}

// Remote Package Installer: start + progress polling
const rpiTasks = {};
async function startRpiInstall(body) {
  showToast('Enviando pedido ao Remote Package Installer...', 'info');
  try {
    const d = await postJson('/api/system/rpi', body);
    if (!d.success) return showToast(d.error || 'O instalador recusou o pedido.', 'error', 8000);
    showToast(`Instalação iniciada no PS4${d.title ? `: ${d.title}` : ''}!`, 'success');
    if (d.taskId !== null && d.taskId !== undefined) {
      rpiTasks[d.taskId] = { title: d.title || body.file || body.url, pct: 0, text: 'Iniciando...' };
      renderRpiTasks();
      pollRpi(d.taskId);
    }
  } catch (err) {
    showToast(err.message, 'error', 8000);
  }
}

async function pollRpi(taskId) {
  const t = rpiTasks[taskId];
  if (!t) return;
  try {
    const p = await api(`/api/system/rpi/progress?task=${encodeURIComponent(taskId)}`, { timeout: 15000 });
    const total = p.length_total || p.length || 0;
    const done = p.transferred_total || p.transferred || 0;
    if (p.status && p.status !== 'success') {
      t.text = 'Concluído ou cancelado';
      t.pct = total && done >= total ? 100 : t.pct;
      t.finished = true;
    } else {
      t.pct = total ? Math.floor(done * 100 / total) : 0;
      const rest = p.rest_sec_total || p.rest_sec;
      t.text = `${formatBytes(done)} / ${formatBytes(total)}${rest ? ` • faltam ~${Math.ceil(rest / 60)} min` : ''}`;
      if (total && done >= total) { t.finished = true; t.text = 'Instalado!'; t.pct = 100; }
    }
  } catch (err) {
    t.text = 'Sem resposta do instalador'; t.finished = true;
  }
  renderRpiTasks();
  if (!t.finished) setTimeout(() => pollRpi(taskId), 3000);
}

function renderRpiTasks() {
  const html = Object.values(rpiTasks).map(t => `
    <div class="rpi-task">
      <strong>${esc(t.title)}</strong> — ${esc(t.text)}
      <div class="progress-bar-wrap"><div class="progress-bar" style="width:${t.pct}%"></div></div>
    </div>`).join('');
  ['rpi-progress', 'pc-rpi-progress'].forEach(id => { const el = document.getElementById(id); if (el) el.innerHTML = html; });
}

// ================= PORTS =================
async function testPorts() {
  const box = document.getElementById('ports-result');
  if (!box) return;
  box.innerHTML = `<div class="text-muted text-sm"><span class="spinner small"></span>Testando...</div>`;
  try {
    const list = await api('/api/ports', { timeout: 20000 });
    box.innerHTML = list.map(p => `
      <div class="port-row ${p.open ? 'open' : ''}">
        <span class="dot"></span>
        <span class="pname">${esc(p.name)} <small>Porta ${p.port} • ${esc(p.use)}</small></span>
        <span class="pstate">${p.open ? 'ABERTA' : 'FECHADA'}</span>
      </div>`).join('');
    const card = document.getElementById('card-ports');
    if (card && state.currentTab === 'tools') card.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
  } catch (err) {
    box.innerHTML = `<div class="text-danger text-sm">${esc(err.message)}</div>`;
  }
}

// ================= CAPTURES =================
async function loadCaptures(gameFilter = '', refresh = false) {
  const selector = document.getElementById('captures-game-selector');
  const container = document.getElementById('captures-container');
  container.innerHTML = `<div class="loading-state"><div class="spinner"></div><span>Carregando capturas...</span></div>`;
  try {
    if (state.captureGames.length === 0) state.captureGames = await api('/api/captures/games');
    if (state.captureGames.length === 0) {
      selector.innerHTML = '';
      container.innerHTML = `<div class="empty-state">Nenhuma captura de tela encontrada no console.</div>`;
      return;
    }
    if (!gameFilter || !state.captureGames.some(g => g.id === gameFilter)) gameFilter = state.captureGames[0].id;
    state.selectedCaptureGame = gameFilter;

    selector.innerHTML = state.captureGames.map(cg => `
      <button class="cap-chip ${gameFilter === cg.id ? 'active' : ''}" data-action="capture-game" data-id="${esc(cg.id)}">${esc(cg.name)}</button>
    `).join('');

    const captures = await api(`/api/captures?game=${encodeURIComponent(gameFilter)}${refresh ? '&refresh=1' : ''}`);
    if (state.selectedCaptureGame !== gameFilter) return;
    state.captures = captures;
    if (captures.length === 0) {
      container.innerHTML = `<div class="empty-state">Nenhuma captura para este jogo.</div>`;
      return;
    }
    container.innerHTML = captures.map(c => `
      <div class="capture-card" data-action="capture" data-full="${esc(c.imageUrl)}" data-title="${esc(c.titleId + ' • ' + c.fileName)}">
        <div class="capture-img-box">
          <img src="${esc(c.thumbUrl)}" alt="" loading="lazy">
        </div>
        <div class="capture-meta">
          <span>${esc(formatCaptureDate(c.fileName))}</span>
          <a href="${esc(c.imageUrl)}" download="${esc(c.fileName)}" class="btn btn-sm btn-outline" onclick="event.stopPropagation()">Baixar</a>
        </div>
      </div>
    `).join('');
  } catch (err) {
    container.innerHTML = `<div class="empty-state text-danger">Erro ao carregar capturas: ${esc(err.message)}</div>`;
  }
}

function formatCaptureDate(fileName) {
  const m = /^(\d{4})(\d{2})(\d{2})_(\d{2})(\d{2})/.exec(fileName || '');
  return m ? `${m[3]}/${m[2]}/${m[1]} ${m[4]}:${m[5]}` : fileName;
}

function openLightbox(imgUrl, title) {
  document.getElementById('lightbox-img').src = imgUrl;
  document.getElementById('lightbox-title').textContent = title;
  document.getElementById('lightbox-download-btn').href = imgUrl;
  openModal('modal-lightbox');
}

// ================= NETWORK / QR =================
async function fetchNetworkInfo() {
  try {
    const net = await api('/api/network');
    state.localIps = net.pcIps || [];
    const bestIp = state.localIps.length > 0 ? state.localIps[0] : 'localhost';
    const mobileUrl = `http://${bestIp}:${net.httpPort}`;
    document.getElementById('mobile-url-display').textContent = mobileUrl;
    document.getElementById('modal-qr-url').textContent = mobileUrl;
    const qrImg = `<img src="https://api.qrserver.com/v1/create-qr-code/?size=180x180&data=${encodeURIComponent(mobileUrl)}" alt="QR Code" width="180" height="180">`;
    document.getElementById('qr-container').innerHTML = qrImg;
    document.getElementById('modal-qr-target').innerHTML = qrImg +
      (net.lanAccess ? '' : `<div class="warn-box mt-3" style="text-align:left">O acesso pela rede está desativado. Feche o programa, execute <code>liberar-rede.bat</code> uma vez e abra de novo.</div>`);
    document.querySelectorAll('.lan-warning').forEach(el => el.hidden = !!net.lanAccess);
  } catch (err) { }
}

// ================= FILE UPLOAD =================
const uploadQueue = [];
let uploading = false;

function uploadFileToPs4(file, targetDir) {
  if (!file) return;
  uploadQueue.push({ file, targetDir });
  if (!uploading) processUploadQueue();
}

function processUploadQueue() {
  const job = uploadQueue.shift();
  if (!job) { uploading = false; return; }
  uploading = true;
  const { file, targetDir } = job;

  const progressFill = document.getElementById('upload-progress-fill');
  const percentText = document.getElementById('upload-percent');
  document.getElementById('upload-file-name').textContent = file.name + (uploadQueue.length ? ` (+${uploadQueue.length} na fila)` : '');
  document.getElementById('upload-file-size').textContent = formatBytes(file.size);
  document.getElementById('upload-dest').textContent = `Destino: ${targetDir}`;
  progressFill.style.width = '0%';
  percentText.textContent = '0%';
  openModal('modal-upload');

  const xhr = new XMLHttpRequest();
  xhr.open('POST', `/api/files/upload?path=${encodeURIComponent(targetDir)}&filename=${encodeURIComponent(file.name)}`, true);
  const started = Date.now();
  xhr.upload.onprogress = (e) => {
    if (!e.lengthComputable) return;
    const pct = Math.round((e.loaded / e.total) * 100);
    const secs = (Date.now() - started) / 1000;
    const speed = secs > 0 ? e.loaded / secs : 0;
    progressFill.style.width = `${pct}%`;
    percentText.textContent = `${pct}% (${formatBytes(e.loaded)} / ${formatBytes(e.total)})${speed ? ` • ${formatBytes(speed)}/s` : ''}`;
  };
  const finish = (ok, msg) => {
    if (ok) {
      progressFill.style.width = '100%';
      percentText.textContent = '100% • Concluído!';
      showToast(`"${file.name}" enviado!`, 'success');
    } else {
      showToast(`Falha ao enviar "${file.name}": ${msg}`, 'error', 7000);
    }
    setTimeout(() => {
      if (!uploadQueue.length) {
        closeModal('modal-upload');
        if (state.currentTab === 'files') loadFiles(state.currentPath);
        if (state.currentTab === 'pkgs') loadPkgs();
        if (state.currentTab === 'cheats') loadCheats();
        fetchSummary(true);
      }
      processUploadQueue();
    }, ok ? 800 : 200);
  };
  xhr.onload = () => {
    let msg = xhr.statusText;
    try { const d = JSON.parse(xhr.responseText); if (d.error) msg = d.error; } catch (e) { }
    finish(xhr.status === 200, msg);
  };
  xhr.onerror = () => finish(false, 'conexão interrompida');
  xhr.send(file);
}

// ================= DRAG AND DROP =================
function initDragAndDrop() {
  const dropZone = document.getElementById('file-drop-zone');
  ['dragenter', 'dragover'].forEach(name => dropZone.addEventListener(name, (e) => { e.preventDefault(); dropZone.classList.add('dragover'); }));
  ['dragleave', 'drop'].forEach(name => dropZone.addEventListener(name, (e) => { e.preventDefault(); dropZone.classList.remove('dragover'); }));
  dropZone.addEventListener('drop', (e) => {
    Array.from(e.dataTransfer.files).forEach(f => uploadFileToPs4(f, state.currentPath));
  });

  window.addEventListener('dragover', (e) => e.preventDefault());
  window.addEventListener('drop', (e) => {
    e.preventDefault();
    if (e.target.closest('#file-drop-zone')) return;
    Array.from(e.dataTransfer.files).forEach(file => {
      let target = state.currentPath;
      const n = file.name.toLowerCase();
      if (n.endsWith('.pkg')) target = '/data/pkg/';
      else if (n.endsWith('.json')) target = '/data/GoldHEN/cheats/json/';
      uploadFileToPs4(file, target);
    });
  });
}

// ================= MODALS & FORMS =================
function initModals() {
  document.querySelectorAll('.modal-close').forEach(btn => {
    btn.addEventListener('click', () => closeModal(btn.closest('.modal-overlay').id));
  });
  document.querySelectorAll('.modal-overlay').forEach(ov => {
    ov.addEventListener('click', (e) => { if (e.target === ov && ov.id !== 'modal-upload') closeModal(ov.id); });
  });
  document.addEventListener('keydown', (e) => {
    if (e.key !== 'Escape') return;
    const open = Array.from(document.querySelectorAll('.modal-overlay.active')).filter(m => m.id !== 'modal-upload');
    if (open.length) closeModal(open[open.length - 1].id);
  });

  document.getElementById('btn-upload-file').addEventListener('click', () => document.getElementById('generic-file-input').click());
  document.getElementById('btn-upload-pkg-direct').addEventListener('click', () => document.getElementById('pkg-file-input').click());
  document.getElementById('btn-new-cheat').addEventListener('click', () => document.getElementById('cheat-file-input').click());

  const multi = (id, dirFn) => {
    const input = document.getElementById(id);
    input.multiple = true;
    input.addEventListener('change', (e) => {
      Array.from(e.target.files).forEach(f => uploadFileToPs4(f, dirFn()));
      input.value = '';
    });
  };
  multi('generic-file-input', () => state.currentPath);
  multi('pkg-file-input', () => '/data/pkg/');
  multi('cheat-file-input', () => '/data/GoldHEN/cheats/json/');

  // Local image for cover: preview, then confirm
  document.getElementById('cover-file-input').addEventListener('change', (e) => {
    const file = e.target.files[0];
    if (!file || !state.cover.id) return;
    state.cover.pendingFile = file;
    state.cover.pendingUrl = null;
    state.cover.pendingTarget = state.cover.target;
    document.getElementById('cover-preview-desc').textContent = 'Imagem escolhida (será ajustada automaticamente para o formato do PS4):';
    document.getElementById('cover-preview-img').src = URL.createObjectURL(file);
    openModal('modal-cover-preview');
  });

  document.querySelectorAll('.path-chip').forEach(chip => chip.addEventListener('click', () => loadFiles(chip.getAttribute('data-path'))));

  document.getElementById('games-search-input').addEventListener('input', applyGamesView);
  document.getElementById('games-sort').addEventListener('change', applyGamesView);

  document.getElementById('cheats-search-input').addEventListener('input', (e) => {
    const q = e.target.value.toLowerCase();
    renderCheatsList(state.cheats.filter(c => (c.gameName || '').toLowerCase().includes(q) || (c.titleId || '').toLowerCase().includes(q)));
  });

  document.getElementById('btn-open-converter').addEventListener('click', () => openModal('modal-converter'));
  document.getElementById('btn-run-convert').addEventListener('click', runRegionConversion);
}

async function loadConfig() {
  try {
    const cfg = await api('/api/config');
    state.config = cfg;
    if (cfg.ps4_ip) document.getElementById('cfg-ps4-ip').value = cfg.ps4_ip;
    if (cfg.ftp_port) document.getElementById('cfg-ftp-port').value = cfg.ftp_port;
    if (cfg.payload_port) document.getElementById('cfg-payload-port').value = cfg.payload_port;
    if (cfg.ps4_fw) document.getElementById('cfg-ps4-fw').value = cfg.ps4_fw;
    document.getElementById('cfg-pc-pkg-folder').value = cfg.pc_pkg_folder || '';
    const keyInput = document.getElementById('cfg-sgdb-key');
    const keyState = document.getElementById('sgdb-key-state');
    keyInput.value = '';
    keyInput.placeholder = cfg.has_sgdb_key ? '•••••••• chave configurada (deixe em branco para manter)' : 'Insira sua chave para liberar capas da comunidade';
    keyState.innerHTML = cfg.has_sgdb_key ? `<span class="text-success">✓ Chave configurada.</span> <a href="#" class="link" id="sgdb-key-remove">remover</a> • ` : '';
    const rm = document.getElementById('sgdb-key-remove');
    if (rm) rm.onclick = async (e) => {
      e.preventDefault();
      await postJson('/api/config', { steamgriddb_api_key: '' });
      showToast('Chave removida.', 'info');
      loadConfig();
    };
    const uSel = document.getElementById('cfg-active-user');
    if (cfg.active_user && !Array.from(uSel.options).some(o => o.value === cfg.active_user)) {
      const opt = document.createElement('option');
      opt.value = cfg.active_user;
      opt.textContent = cfg.active_user;
      uSel.appendChild(opt);
    }
    uSel.value = cfg.active_user || '';
  } catch (err) {
    console.error('Error loading config:', err);
  }
}

function initForms() {
  loadConfig();

  document.getElementById('settings-form').addEventListener('submit', async (e) => {
    e.preventDefault();
    const body = {
      ps4_ip: document.getElementById('cfg-ps4-ip').value.trim(),
      ftp_port: parseInt(document.getElementById('cfg-ftp-port').value, 10),
      payload_port: parseInt(document.getElementById('cfg-payload-port').value, 10),
      ps4_fw: document.getElementById('cfg-ps4-fw').value.trim(),
      active_user: document.getElementById('cfg-active-user').value,
      pc_pkg_folder: document.getElementById('cfg-pc-pkg-folder').value.trim()
    };
    const key = document.getElementById('cfg-sgdb-key').value.trim();
    if (key) body.steamgriddb_api_key = key;
    try {
      await postJson('/api/config', body);
      showToast('Configurações salvas!', 'success');
      loadConfig();
      fetchStatus();
      fetchSummary(true);
      fetchGames();
    } catch (err) {
      showToast('Erro ao salvar: ' + err.message, 'error');
    }
  });

  document.getElementById('btn-send-payload').addEventListener('click', async (e) => {
    const fileInput = document.getElementById('payload-file-input');
    if (fileInput.files.length === 0) return showToast('Selecione um arquivo .bin de payload.', 'error');
    const file = fileInput.files[0];
    const btn = e.currentTarget;
    btn.disabled = true;
    showToast(`Enviando "${file.name}"...`, 'info');
    try {
      const data = await api('/api/payload', { method: 'POST', body: file });
      showToast(data.message, 'success');
    } catch (err) {
      showToast(err.message, 'error', 8000);
    } finally {
      btn.disabled = false;
    }
  });

  document.getElementById('btn-mkdir').addEventListener('click', async () => {
    const name = prompt('Nome da nova pasta:');
    if (!name) return;
    if (name.includes('/')) return showToast('O nome não pode conter "/".', 'error');
    try {
      await api(`/api/files/mkdir?path=${encodeURIComponent(state.currentPath + name)}`, { method: 'POST' });
      showToast(`Pasta "${name}" criada!`, 'success');
      loadFiles(state.currentPath);
    } catch (err) {
      showToast('Erro ao criar pasta: ' + err.message, 'error');
    }
  });
}

// ================= CHEAT REGION CONVERTER =================
async function runRegionConversion() {
  const fileInput = document.getElementById('converter-file-input');
  const sourceId = document.getElementById('converter-source-id').value.trim().toUpperCase();
  const targetId = document.getElementById('converter-target-id').value.trim().toUpperCase();
  const version = document.getElementById('converter-version').value.trim();
  if (fileInput.files.length === 0) return showToast('Selecione o arquivo .json do cheat original.', 'error');

  const rawJson = await fileInput.files[0].text();
  showToast(`Convertendo ${sourceId} → ${targetId}...`, 'info');
  try {
    const data = await postJson('/api/cheat/convert', { sourceId, targetId, version, json: rawJson });
    showToast(`Cheat ${data.filename} convertido e enviado para o PS4!`, 'success');
    closeModal('modal-converter');
    if (state.currentTab === 'cheats') loadCheats();
  } catch (err) {
    showToast(`Falha na conversão: ${err.message}`, 'error', 7000);
  }
}

// ================= FILE ACTIONS =================
async function deleteItem(path, isDir) {
  if (!confirm(`Excluir "${path}" do PS4?\n\nEsta ação não pode ser desfeita.`)) return;
  try {
    await api(`/api/files/delete?path=${encodeURIComponent(path.replace(/\/$/, ''))}&isDir=${isDir}`, { method: 'POST' });
    showToast('Item excluído.', 'success');
    if (state.currentTab === 'files') loadFiles(state.currentPath);
    if (state.currentTab === 'pkgs') loadPkgs();
    fetchSummary(true);
  } catch (err) {
    showToast(err.message, 'error', 6000);
  }
}

// ================= UTILITIES =================
const modalReturnFocus = {};
function openModal(id) {
  const m = document.getElementById(id);
  modalReturnFocus[id] = document.activeElement;
  m.classList.add('active');
  const card = m.querySelector('.modal-card');
  if (card) setTimeout(() => card.focus({ preventScroll: true }), 30);
}

function closeModal(id) {
  document.getElementById(id).classList.remove('active');
  const prev = modalReturnFocus[id];
  if (prev && typeof prev.focus === 'function' && document.body.contains(prev)) prev.focus({ preventScroll: true });
}

const TOAST_ICONS = { success: '#i-star', error: '#i-alert', info: '#i-bell' };
function showToast(msg, type = 'info', duration = 3500) {
  const container = document.getElementById('toast-container');
  const toast = document.createElement('div');
  toast.className = `toast ${type}`;
  toast.setAttribute('role', type === 'error' ? 'alert' : 'status');
  toast.innerHTML = `<svg aria-hidden="true"><use href="${TOAST_ICONS[type] || TOAST_ICONS.info}"/></svg><span></span>`;
  toast.querySelector('span').textContent = msg;
  container.appendChild(toast);
  setTimeout(() => {
    toast.style.opacity = '0';
    setTimeout(() => toast.remove(), 300);
  }, duration);
}

function formatBytes(bytes, decimals = 1) {
  if (!+bytes) return '0 B';
  const k = 1024;
  const sizes = ['B', 'KB', 'MB', 'GB', 'TB'];
  const i = Math.min(sizes.length - 1, Math.floor(Math.log(bytes) / Math.log(k)));
  return `${parseFloat((bytes / Math.pow(k, i)).toFixed(decimals))} ${sizes[i]}`;
}

// ================= INTERACTIVE TUTORIAL TOUR =================
const tourSteps = [
  {
    target: '[data-tab="settings"]',
    pt: {
      title: "1. Conecte seu PlayStation 4",
      desc: "O primeiro passo é abrir Configurações e colocar o endereço de IP do seu PS4 (ex: 192.168.18.54). O console e o computador/celular devem estar na mesma rede local."
    },
    en: {
      title: "1. Connect your PlayStation 4",
      desc: "The first step is to open Settings and enter your PS4's local IP address (e.g. 192.168.18.54). Both console and device must be on the same local network."
    }
  },
  {
    target: '#pane-dashboard .stat-card:first-child',
    switchTab: 'dashboard',
    pt: {
      title: "2. Status do Console & Rede",
      desc: "Acompanhe aqui em tempo real a latência (ping) e o servidor FTP do GoldHEN. Use 'Testar portas' para checar BinLoader (9090), ps4debug (744) e o instalador remoto (12800)."
    },
    en: {
      title: "2. Console Status & Network",
      desc: "Check real-time connectivity and the GoldHEN FTP server. Use 'Test ports' to check BinLoader (9090), ps4debug (744) and the remote installer (12800)."
    }
  },
  {
    target: '[data-tab="games"]',
    pt: {
      title: "3. Jogos, Horas & Troca de Capas",
      desc: "Veja toda sua biblioteca instalada, tempo jogado e altere a capa de qualquer jogo enviando uma imagem nova direto pelo navegador!"
    },
    en: {
      title: "3. Games, Playtime & Covers",
      desc: "Browse installed games, track playtime records, and change game covers on your PS4 by uploading images directly here!"
    }
  },
  {
    target: '[data-tab="cheats"]',
    pt: {
      title: "4. Cheats & Conversor CUSA",
      desc: "Gerencie cheats do GoldHEN e use o Conversor CUSA para transformar cheats de outras regiões (como CUSA00242) para a versão instalada no seu console."
    },
    en: {
      title: "4. Cheats & CUSA Converter",
      desc: "Manage GoldHEN cheats and use the CUSA Converter to adapt cheats from foreign regions (like CUSA00242) into your installed game version."
    }
  },
  {
    target: '#btn-qr-mobile',
    pt: {
      title: "5. Acesso pelo Smartphone",
      desc: "Clique no botão 'Conectar Celular' para gerar um QR Code instantâneo e controlar seu PlayStation 4 diretamente pelo smartphone."
    },
    en: {
      title: "5. Smartphone Access",
      desc: "Click 'Connect Phone' to display an instant QR code and control your PlayStation 4 directly from your smartphone."
    }
  },
  {
    target: '[data-tab="donate"]',
    pt: {
      title: "6. Apoie o Projeto ❤️",
      desc: "Gostou do Orbis Web Manager? Na aba Apoiar Projeto você pode contribuir com o desenvolvedor via Pix QR Code ou Buy Me a Coffee!"
    },
    en: {
      title: "6. Support the Project ❤️",
      desc: "Enjoying Orbis Web Manager? Visit the Support tab to contribute to continuous open-source development via Pix QR code or Buy Me a Coffee!"
    }
  }
];

let currentTourStep = 0;

function startTour() {
  currentTourStep = 0;
  showTourStep(0);
}

function closeTour() {
  const backdrop = document.getElementById('tour-backdrop');
  const balloon = document.getElementById('tour-balloon');
  if (backdrop) backdrop.classList.remove('active');
  if (balloon) balloon.classList.remove('active');
  document.querySelectorAll('.tour-highlight').forEach(el => el.classList.remove('tour-highlight'));
  localStorage.setItem('owm_tour_completed', 'true');
}

function showTourStep(index) {
  if (index < 0 || index >= tourSteps.length) {
    closeTour();
    return;
  }

  currentTourStep = index;
  const step = tourSteps[index];
  const lang = state.lang || 'pt';
  const content = step[lang] || step.pt;

  if (step.switchTab && state.currentTab !== step.switchTab) {
    switchTab(step.switchTab);
  }

  // Clear previous highlight
  document.querySelectorAll('.tour-highlight').forEach(el => el.classList.remove('tour-highlight'));

  const targetEl = document.querySelector(step.target);
  if (targetEl) {
    targetEl.classList.add('tour-highlight');
    try {
      targetEl.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
    } catch { }
  }

  const backdrop = document.getElementById('tour-backdrop');
  const balloon = document.getElementById('tour-balloon');
  const badge = document.getElementById('tour-step-badge');
  const title = document.getElementById('tour-title');
  const desc = document.getElementById('tour-desc');
  const prevBtn = document.getElementById('tour-prev-btn');
  const nextBtn = document.getElementById('tour-next-btn');
  const dots = document.getElementById('tour-dots');

  if (backdrop) backdrop.classList.add('active');
  if (balloon) balloon.classList.add('active');

  badge.textContent = `${index + 1} / ${tourSteps.length}`;
  title.textContent = content.title;
  desc.textContent = content.desc;

  prevBtn.style.visibility = index === 0 ? 'hidden' : 'visible';
  prevBtn.textContent = lang === 'en' ? 'Previous' : 'Anterior';
  nextBtn.textContent = (index === tourSteps.length - 1) ? (lang === 'en' ? 'Finish' : 'Concluir') : (lang === 'en' ? 'Next' : 'Próximo');

  // Dots
  dots.innerHTML = '';
  tourSteps.forEach((_, i) => {
    const dot = document.createElement('span');
    dot.className = `tour-dot ${i === index ? 'active' : ''}`;
    dots.appendChild(dot);
  });

  // Position balloon
  positionBalloon(targetEl, balloon);
}

function positionBalloon(targetEl, balloon) {
  if (!balloon) return;
  const balloonWidth = 320;
  const balloonHeight = balloon.offsetHeight || 200;
  const pad = 14;

  if (!targetEl) {
    balloon.style.top = `${window.innerHeight / 2 - balloonHeight / 2}px`;
    balloon.style.left = `${window.innerWidth / 2 - balloonWidth / 2}px`;
    return;
  }

  const rect = targetEl.getBoundingClientRect();
  let top = rect.bottom + pad;
  let left = rect.left;

  // Horizontal bounding
  if (left + balloonWidth > window.innerWidth - 16) {
    left = window.innerWidth - balloonWidth - 16;
  }
  if (left < 16) left = 16;

  // Vertical bounding
  if (top + balloonHeight > window.innerHeight - 16) {
    top = rect.top - balloonHeight - pad;
    if (top < 16) {
      top = window.innerHeight / 2 - balloonHeight / 2;
    }
  }

  balloon.style.top = `${Math.max(16, top)}px`;
  balloon.style.left = `${Math.max(16, left)}px`;
}
