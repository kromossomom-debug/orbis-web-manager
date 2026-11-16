// Orbis Web Manager • PlayStation 4 Web Suite
// Client-side Application Controller

const state = {
  currentTab: 'dashboard',
  currentPath: '/data/',
  ps4Ip: '192.168.18.54',
  ftpPort: 2121,
  games: [],
  cheats: [],
  pkgs: [],
  captures: [],
  captureGames: [],
  selectedCaptureGame: '',
  selectedGameIdForCover: null,
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

// ================= INITIALIZATION =================
document.addEventListener('DOMContentLoaded', () => {
  initI18n();
  initNavigation();
  initDragAndDrop();
  initModals();
  initForms();
  
  // Initial data fetches
  fetchStatus();
  fetchGames();
  fetchNetworkInfo();

  // Status polling interval (every 10s)
  setInterval(fetchStatus, 10000);

  // Auto-launch interactive onboarding tutorial on first visit
  if (!localStorage.getItem('owm_tour_completed')) {
    setTimeout(startTour, 1200);
  }
});

// ================= NAVIGATION =================
function initNavigation() {
  const navItems = document.querySelectorAll('.nav-item, .mobile-nav-item');
  navItems.forEach(item => {
    item.addEventListener('click', () => {
      const tab = item.getAttribute('data-tab');
      switchTab(tab);
    });
  });

  // Action buttons that trigger tabs
  document.querySelectorAll('[data-tab-trigger]').forEach(btn => {
    btn.addEventListener('click', () => {
      const tab = btn.getAttribute('data-tab-trigger');
      switchTab(tab);
    });
  });

  // Action buttons on Dashboard
  document.getElementById('action-upload-pkg').addEventListener('click', () => {
    state.currentPath = '/data/pkg/';
    document.getElementById('pkg-file-input').click();
  });

  document.getElementById('action-upload-cheat').addEventListener('click', () => {
    state.currentPath = '/data/GoldHEN/cheats/json/';
    document.getElementById('cheat-file-input').click();
  });

  document.getElementById('action-browse-files').addEventListener('click', () => {
    switchTab('files');
  });

  document.getElementById('action-convert-cheat').addEventListener('click', () => {
    openModal('modal-converter');
  });

  document.getElementById('btn-quick-upload').addEventListener('click', () => {
    document.getElementById('generic-file-input').click();
  });

  document.getElementById('btn-qr-mobile').addEventListener('click', () => {
    openModal('modal-qr');
  });

  document.getElementById('btn-refresh-games').addEventListener('click', () => {
    fetchGames();
    showToast('Biblioteca recarregada.', 'info');
  });

  document.getElementById('btn-refresh-captures').addEventListener('click', () => {
    loadCaptures(state.selectedCaptureGame);
    showToast('Capturas atualizadas.', 'info');
  });

  // Pix Copy Button
  const btnCopyPix = document.getElementById('btn-copy-pix');
  if (btnCopyPix) {
    btnCopyPix.addEventListener('click', () => {
      const pixKey = '94af8068-d112-4c69-bcfa-8789b233246e';
      if (navigator.clipboard && navigator.clipboard.writeText) {
        navigator.clipboard.writeText(pixKey).then(() => {
          showToast('Chave Pix copiada com sucesso! Obrigado pelo apoio! ❤️', 'success');
        }).catch(() => {
          showToast(`Chave Pix: ${pixKey}`, 'info');
        });
      } else {
        showToast(`Chave Pix: ${pixKey}`, 'info');
      }
    });
  }

  // Notify TV Button
  const btnSendNotify = document.getElementById('btn-send-notify');
  if (btnSendNotify) {
    btnSendNotify.addEventListener('click', async () => {
      const input = document.getElementById('notify-msg-input');
      const msg = input.value.trim();
      if (!msg) {
        showToast('Digite uma mensagem para a notificação.', 'error');
        return;
      }
      try {
        const res = await fetch('/api/system/notify', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ message: msg })
        });
        const d = await res.json();
        if (d.success) {
          showToast('Notificação enviada para a tela do PS4!', 'success');
        } else {
          showToast(d.error || 'Falha ao enviar notificação.', 'error');
        }
      } catch (e) {
        showToast('Erro ao comunicar com console.', 'error');
      }
    });
  }

  // RPI Trigger Button
  const btnTriggerRpi = document.getElementById('btn-trigger-rpi');
  if (btnTriggerRpi) {
    btnTriggerRpi.addEventListener('click', async () => {
      const input = document.getElementById('rpi-pkg-url');
      const url = input.value.trim();
      if (!url) {
        showToast('Informe a URL do arquivo .pkg na rede.', 'error');
        return;
      }
      showToast('Enviando comando para o Package Installer (12800)...', 'info');
      try {
        const res = await fetch('/api/system/rpi', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ url: url })
        });
        const d = await res.json();
        if (d.success) {
          showToast('Instalação iniciada em segundo plano no PS4!', 'success');
        } else {
          showToast(d.error || 'Porta 12800 não respondeu. Certifique-se que o Package Installer está ativo.', 'error');
        }
      } catch (e) {
        showToast('Erro na requisição RPI.', 'error');
      }
    });
  }

  // Language Toggle Button
  const btnLang = document.getElementById('btn-lang-toggle');
  if (btnLang) {
    btnLang.addEventListener('click', () => {
      const nextLang = state.lang === 'pt' ? 'en' : 'pt';
      setLanguage(nextLang);
      showToast(nextLang === 'en' ? 'Language switched to English!' : 'Idioma alterado para Português!', 'info');
    });
  }

  // Tutorial / Tour Button & Controls
  const btnTour = document.getElementById('btn-tour-start');
  if (btnTour) {
    btnTour.addEventListener('click', () => {
      startTour();
    });
  }

  const tourCloseBtn = document.getElementById('tour-close-btn');
  if (tourCloseBtn) tourCloseBtn.addEventListener('click', closeTour);

  const tourBackdrop = document.getElementById('tour-backdrop');
  if (tourBackdrop) tourBackdrop.addEventListener('click', closeTour);

  const tourPrevBtn = document.getElementById('tour-prev-btn');
  if (tourPrevBtn) tourPrevBtn.addEventListener('click', () => showTourStep(currentTourStep - 1));

  const tourNextBtn = document.getElementById('tour-next-btn');
  if (tourNextBtn) tourNextBtn.addEventListener('click', () => showTourStep(currentTourStep + 1));

  // Online Cover Search Trigger
  const btnCoverSearch = document.getElementById('btn-cover-search-exec');
  if (btnCoverSearch) {
    btnCoverSearch.addEventListener('click', () => {
      const q = document.getElementById('cover-search-input').value.trim();
      if (q && state.selectedGameIdForCover) {
        searchCoversOnline(state.selectedGameIdForCover, q);
      }
    });
  }

  const coverSearchInput = document.getElementById('cover-search-input');
  if (coverSearchInput) {
    coverSearchInput.addEventListener('keypress', (e) => {
      if (e.key === 'Enter') {
        const q = coverSearchInput.value.trim();
        if (q && state.selectedGameIdForCover) {
          searchCoversOnline(state.selectedGameIdForCover, q);
        }
      }
    });
  }
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

  // Update nav buttons
  document.querySelectorAll('.nav-item, .mobile-nav-item').forEach(el => {
    el.classList.toggle('active', el.getAttribute('data-tab') === tabId);
  });

  // Update panes
  document.querySelectorAll('.tab-pane').forEach(pane => {
    pane.classList.toggle('active', pane.id === `pane-${tabId}`);
  });

  // Update Topbar Titles
  updateTabTitles(tabId);

  // Trigger lazy loading per tab
  if (tabId === 'files') loadFiles(state.currentPath);
  if (tabId === 'cheats') loadCheats();
  if (tabId === 'pkgs') loadPkgs();
  if (tabId === 'media') loadCaptures();
}

// ================= API CALLS =================

// 1. Status
async function fetchStatus() {
  try {
    const res = await fetch('/api/status');
    const data = await res.json();
    state.status = data;

    const pingIndicator = document.getElementById('ping-indicator');
    const pingVal = pingIndicator.querySelector('.ping-val');
    const userDisplay = document.getElementById('user-display');
    const ipDisplay = document.getElementById('ip-display');

    if (data.online) {
      pingVal.textContent = data.ping >= 0 ? `${data.ping} ms` : 'Online';
      pingIndicator.style.borderColor = 'rgba(0, 230, 118, 0.4)';
    } else {
      pingVal.textContent = 'Offline';
      pingIndicator.style.borderColor = 'rgba(255, 61, 113, 0.4)';
    }

    userDisplay.textContent = data.username || 'PlayStation 4';
    ipDisplay.textContent = `${data.ip}:${data.ftpPort}`;

    const dashStatus = document.getElementById('dash-online-status');
    dashStatus.textContent = data.online ? 'Online • Conectado' : 'Console Desconectado';
    dashStatus.style.color = data.online ? 'var(--accent-green)' : 'var(--accent-red)';

    document.getElementById('dash-banner').textContent = data.banner || 'GoldHEN FTP Server';
    document.getElementById('dash-user').textContent = data.username || 'PlayStation 4';

    const fwBadge = document.getElementById('dash-fw-badge');
    if (fwBadge) {
      const fw = data.fw || '9.00';
      const hen = data.goldhenVer || 'GoldHEN v2.2';
      fwBadge.textContent = `FW: ${fw} • ${hen}`;
    }

    // Populate user profile options in settings if users detected
    if (data.users && data.users.length > 0) {
      const userSelect = document.getElementById('cfg-active-user');
      if (userSelect && userSelect.options.length <= 1) {
        data.users.forEach(u => {
          const opt = document.createElement('option');
          opt.value = u.name;
          opt.textContent = `${u.name} (${u.id})${u.isCurrent ? ' [Ativo]' : ''}`;
          if (u.isCurrent) opt.selected = true;
          userSelect.appendChild(opt);
        });
      }
    }

    document.getElementById('dash-port-ftp').textContent = data.ftpOk ? `FTP ${data.ftpPort}: Ativo` : `FTP ${data.ftpPort}: Inativo`;
    document.getElementById('dash-port-payload').textContent = data.payloadPortOk ? `Porta ${data.payloadPort}: Aberta` : `Porta ${data.payloadPort}: Fechada`;

    document.getElementById('dash-pkg-count').textContent = `${data.pkgCount || 0} pacotes em /data/pkg/`;
    document.getElementById('dash-pkg-size').textContent = formatBytes(data.totalPkgSize || 0);
    document.getElementById('pkgs-count-badge').textContent = data.pkgCount || 0;
    document.getElementById('cheats-count-badge').textContent = data.cheatCount || 0;

  } catch (err) {
    console.error('Status fetch error:', err);
  }
}

// 2. Games
async function fetchGames() {
  try {
    const res = await fetch('/api/games');
    const games = await res.json();
    state.games = games;

    document.getElementById('dash-games-count').textContent = games.length;
    document.getElementById('games-count-badge').textContent = games.length;

    renderGames(games);
    renderRecentGames(games.slice(0, 8));
  } catch (err) {
    console.error('Games fetch error:', err);
  }
}

function renderGames(games) {
  const container = document.getElementById('games-container');
  if (!container) return;

  if (games.length === 0) {
    container.innerHTML = `<div class="empty-state">Nenhum jogo encontrado no console. Verifique a conexão com o PS4.</div>`;
    return;
  }

  container.innerHTML = games.map(game => {
    const hours = Math.floor(game.timePlayedMinutes / 60);
    const mins = game.timePlayedMinutes % 60;
    const timeStr = hours > 0 ? `${hours}h ${mins}m` : `${mins}m`;

    return `
      <div class="game-card" id="game-card-${game.id}">
        <div class="game-cover-box">
          <img id="game-img-${game.id}" src="${game.iconUrl}" alt="${game.name}" loading="lazy" onerror="this.src='data:image/svg+xml,<svg xmlns=\'http://www.w3.org/2000/svg\' viewBox=\'0 0 100 100\' fill=\'%23111827\'><rect width=\'100\' height=\'100\'/><text x=\'50\' y=\'55\' fill=\'%2364748b\' font-size=\'12\' text-anchor=\'middle\'>Sem Capa</text></svg>'">
          ${game.hasCheats ? `<span class="game-badge-cheat">CHEATS</span>` : ''}
          <button class="btn-change-cover" onclick="triggerCoverChange('${game.id}', event)" title="Alterar a capa deste jogo">
            <svg viewBox="0 0 24 24" width="13" height="13" fill="none" stroke="currentColor" stroke-width="2"><path d="M23 19a2 2 0 01-2 2H3a2 2 0 01-2-2V8a2 2 0 012-2h4l2-3h6l2 3h4a2 2 0 012 2z"/><circle cx="12" cy="13" r="4"/></svg>
            <span>Trocar Capa</span>
          </button>
        </div>
        <div class="game-body">
          <div class="game-id-tag">${game.id}</div>
          <h4 class="game-title" title="${game.name}">${game.name}</h4>
          <div class="game-stats-row">
            <span>Tempo: <strong>${timeStr}</strong></span>
            <span>Iniciado: <strong>${game.gameStarts}x</strong></span>
          </div>
        </div>
      </div>
    `;
  }).join('');
}

function renderRecentGames(games) {
  const container = document.getElementById('dash-recent-games');
  if (!container) return;

  if (games.length === 0) {
    container.innerHTML = `<span>Nenhum jogo jogado recentemente.</span>`;
    return;
  }

  container.innerHTML = games.map(game => {
    const hours = Math.floor(game.timePlayedMinutes / 60);
    const mins = game.timePlayedMinutes % 60;
    const timeStr = hours > 0 ? `${hours}h ${mins}m` : `${mins}m`;

    return `
      <div class="game-card-mini" onclick="switchTab('games')">
        <div class="cover-wrap">
          <img src="${game.iconUrl}" alt="${game.name}" loading="lazy">
        </div>
        <div class="info">
          <div class="title" title="${game.name}">${game.name}</div>
          <div class="meta">
            <span>${game.id}</span>
            <span>${timeStr}</span>
          </div>
        </div>
      </div>
    `;
  }).join('');
}

// ================= COVER CHANGER =================
function triggerCoverChange(gameId, event) {
  if (event) event.stopPropagation();
  state.selectedGameIdForCover = gameId;

  let gameTitle = gameId;
  if (state.games && state.games.length > 0) {
    const found = state.games.find(g => g.id === gameId);
    if (found && found.name) gameTitle = found.name;
  }

  openCoverSearchModal(gameId, gameTitle);
}

function triggerLocalFileUpload() {
  closeModal('modal-cover-search');
  const input = document.getElementById('cover-file-input');
  input.value = '';
  input.click();
}

function openCoverSearchModal(gameId, gameTitle) {
  state.selectedGameIdForCover = gameId;
  state.selectedGameTitleForCover = gameTitle;

  const titleEl = document.getElementById('modal-cover-title');
  const subEl = document.getElementById('modal-cover-sub');
  const inputEl = document.getElementById('cover-search-input');

  if (titleEl) titleEl.textContent = `Buscar Capa - ${gameTitle}`;
  if (subEl) subEl.textContent = `Title ID: ${gameId}`;
  if (inputEl) inputEl.value = gameTitle;

  openModal('modal-cover-search');
  searchCoversOnline(gameId, gameTitle);
}

async function searchCoversOnline(gameId, searchTitle) {
  const loading = document.getElementById('cover-search-loading');
  const results = document.getElementById('cover-search-results');
  const sonySec = document.getElementById('sony-cover-section');
  const sgdbGrid = document.getElementById('sgdb-grid-container');
  const sgdbBadge = document.getElementById('sgdb-count-badge');
  const noKeyBanner = document.getElementById('sgdb-no-key-banner');

  if (loading) loading.style.display = 'block';
  if (results) results.style.display = 'none';
  if (sonySec) sonySec.style.display = 'none';
  if (sgdbGrid) sgdbGrid.innerHTML = '';

  try {
    const res = await fetch(`/api/game/covers/search?id=${encodeURIComponent(gameId)}&title=${encodeURIComponent(searchTitle)}`);
    const data = await res.json();

    if (loading) loading.style.display = 'none';
    if (results) results.style.display = 'block';

    // 1. Sony Official TMDB
    if (data.sony && data.sony.available && data.sony.icon) {
      if (sonySec) sonySec.style.display = 'block';
      const previewImg = document.getElementById('sony-cover-preview');
      if (previewImg) previewImg.src = data.sony.icon;
      const btnApplySony = document.getElementById('btn-apply-sony-cover');
      if (btnApplySony) {
        btnApplySony.onclick = () => applyCoverOnline(gameId, data.sony.icon);
      }
    }

    // 2. SteamGridDB
    if (!data.steamgriddb.hasKey) {
      if (noKeyBanner) noKeyBanner.style.display = 'block';
      if (sgdbBadge) sgdbBadge.textContent = 'Requer Chave';
    } else {
      if (noKeyBanner) noKeyBanner.style.display = 'none';
      const grids = data.steamgriddb.grids || [];
      if (sgdbBadge) sgdbBadge.textContent = `${grids.length} encontradas`;

      if (grids.length === 0) {
        if (sgdbGrid) {
          sgdbGrid.innerHTML = `<p class="text-muted text-sm" style="grid-column: 1 / -1;">Nenhuma arte encontrada para "${searchTitle}". Tente simplificar o nome na barra de busca acima.</p>`;
        }
      } else {
        if (sgdbGrid) {
          sgdbGrid.innerHTML = grids.map(g => `
            <div class="sgdb-card" style="position: relative; border-radius: 8px; overflow: hidden; background: var(--bg-surface-elevated); border: 1px solid var(--border-subtle); display: flex; flex-direction: column;">
              <div style="aspect-ratio: 2/3; overflow: hidden; position: relative;">
                <img src="${g.thumb || g.url}" alt="Arte" loading="lazy" style="width: 100%; height: 100%; object-fit: cover;">
                <button class="btn btn-primary btn-sm" onclick="applyCoverOnline('${gameId}', '${g.url}')" style="position: absolute; bottom: 8px; left: 8px; right: 8px; opacity: 0.95; font-size: 11px; padding: 6px;">
                  Aplicar
                </button>
              </div>
              <div style="padding: 6px 8px; font-size: 11px;" class="text-muted text-truncate">
                Por: ${g.author || 'Comunidade'}
              </div>
            </div>
          `).join('');
        }
      }
    }
  } catch (err) {
    if (loading) loading.style.display = 'none';
    if (results) results.style.display = 'block';
    showToast('Erro ao buscar capas online: ' + err.message, 'error');
  }
}

async function applyCoverOnline(gameId, imageUrl) {
  showToast(`Baixando e aplicando capa para ${gameId}...`, 'info');
  try {
    const res = await fetch('/api/game/covers/apply', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ id: gameId, url: imageUrl })
    });
    const data = await res.json();
    if (data.success) {
      showToast(`Capa do jogo ${gameId} atualizada com sucesso!`, 'success');
      const img = document.getElementById(`game-img-${gameId}`);
      if (img) img.src = data.iconUrl;
      closeModal('modal-cover-search');
    } else {
      showToast(`Falha ao aplicar capa: ${data.error}`, 'error');
    }
  } catch (err) {
    showToast(`Erro ao aplicar capa: ${err.message}`, 'error');
  }
}

async function uploadNewCover(file, gameId) {
  if (!file || !gameId) return;

  showToast(`Enviando nova capa para ${gameId}...`, 'info');

  try {
    const res = await fetch(`/api/game/icon/upload?id=${encodeURIComponent(gameId)}`, {
      method: 'POST',
      body: file
    });
    const data = await res.json();

    if (data.success) {
      showToast(`Capa do jogo ${gameId} alterada com sucesso!`, 'success');
      const img = document.getElementById(`game-img-${gameId}`);
      if (img) {
        img.src = `${data.iconUrl}`;
      }
    } else {
      showToast(`Falha ao alterar capa: ${data.error}`, 'error');
    }
  } catch (err) {
    showToast(`Erro ao enviar capa: ${err.message}`, 'error');
  }
}

// 3. File Explorer
async function loadFiles(dirPath) {
  state.currentPath = dirPath;
  const tbody = document.getElementById('file-table-body');
  tbody.innerHTML = `<tr><td colspan="4" class="p-4 text-center">Carregando diretório...</td></tr>`;

  renderBreadcrumb(dirPath);

  try {
    const res = await fetch(`/api/files?path=${encodeURIComponent(dirPath)}`);
    const data = await res.json();

    const items = data.items || [];
    if (items.length === 0) {
      tbody.innerHTML = `<tr><td colspan="4" class="p-4 text-center text-muted">Pasta vazia</td></tr>`;
      return;
    }

    tbody.innerHTML = items.map(item => {
      const fullPath = (dirPath.endsWith('/') ? dirPath : dirPath + '/') + item.name;
      const sizeStr = item.isDir ? '--' : formatBytes(item.size);

      return `
        <tr>
          <td>
            <div class="file-name-cell" onclick="${item.isDir ? `loadFiles('${fullPath}/')` : `previewOrDownload('${fullPath}')`}">
              <svg viewBox="0 0 24 24" width="18" height="18" fill="none" stroke-width="2" class="file-icon ${item.isDir ? 'folder' : ''}">
                ${item.isDir ? '<path d="M3 7v10a2 2 0 002 2h14a2 2 0 002-2V9a2 2 0 00-2-2h-6l-2-2H5a2 2 0 00-2 2z"/>' : '<path d="M14 2H6a2 2 0 00-2 2v16a2 2 0 002 2h12a2 2 0 002-2V8zM14 2v6h6"/>'}
              </svg>
              <span>${item.name}</span>
            </div>
          </td>
          <td style="font-family: monospace;">${sizeStr}</td>
          <td style="color: var(--text-muted);">${item.date}</td>
          <td style="text-align: right;">
            ${!item.isDir ? `
              <a href="/api/files/download?path=${encodeURIComponent(fullPath)}" class="btn btn-sm btn-outline" title="Baixar">
                <svg viewBox="0 0 24 24" width="14" height="14" fill="none" stroke="currentColor" stroke-width="2"><path d="M4 16v1a3 3 0 003 3h10a3 3 0 003-3v-1m-4-4l-4 4m0 0l-4-4m4 4V4"/></svg>
              </a>
            ` : ''}
            <button class="btn btn-sm btn-danger" onclick="deleteItem('${fullPath}', ${item.isDir})" title="Excluir">
              <svg viewBox="0 0 24 24" width="14" height="14" fill="none" stroke="currentColor" stroke-width="2"><path d="M19 7l-.867 12.142A2 2 0 0116.138 21H7.862a2 2 0 01-1.995-1.858L5 7m5 4v6m4-6v6m1-10V4a1 1 0 00-1-1h-4a1 1 0 00-1 1v3M4 7h16"/></svg>
            </button>
          </td>
        </tr>
      `;
    }).join('');

  } catch (err) {
    tbody.innerHTML = `<tr><td colspan="4" class="p-4 text-center text-danger">Erro ao listar arquivos: ${err.message}. Verifique o IP do PS4 nas Configurações.</td></tr>`;
  }
}

function renderBreadcrumb(dirPath) {
  const container = document.getElementById('file-breadcrumb');
  const parts = dirPath.split('/').filter(p => p.length > 0);
  
  let html = `<span class="crumb" onclick="loadFiles('/')">PS4</span>`;
  let accum = '/';
  for (const part of parts) {
    accum += part + '/';
    html += ` / <span class="crumb" onclick="loadFiles('${accum}')">${part}</span>`;
  }
  container.innerHTML = html;

  document.querySelectorAll('.path-chip').forEach(chip => {
    chip.classList.toggle('active', chip.getAttribute('data-path') === dirPath);
  });
}

// 4. Cheats
async function loadCheats() {
  const container = document.getElementById('cheats-container');
  container.innerHTML = `<div class="p-4 text-center">Carregando cheats instalados...</div>`;

  try {
    const res = await fetch('/api/cheats');
    const cheats = await res.json();
    state.cheats = cheats;

    if (cheats.length === 0) {
      container.innerHTML = `<div class="p-4 text-center text-muted">Nenhum cheat encontrado em /data/GoldHEN/cheats/json/.</div>`;
      return;
    }

    renderCheatsList(cheats);

  } catch (err) {
    container.innerHTML = `<div class="p-4 text-danger">Erro ao carregar cheats: ${err.message}. Verifique a conexão com o PS4.</div>`;
  }
}

function renderCheatsList(cheats) {
  const container = document.getElementById('cheats-container');
  container.innerHTML = cheats.map(c => {
    return `
      <div class="cheat-card" onclick="viewCheat('${c.path}', '${c.titleId}', '${c.gameName}', '${c.version}')">
        <div class="c-top">
          <div>
            <div class="c-id">${c.titleId} • v${c.version}</div>
            <div class="c-title">${c.gameName}</div>
          </div>
          <span class="badge cheat">JSON</span>
        </div>
        <div style="font-size: 11px; color: var(--text-muted); display: flex; justify-content: space-between;">
          <span>Arquivo: ${c.filename}</span>
          <span>${formatBytes(c.size)}</span>
        </div>
      </div>
    `;
  }).join('');
}

async function viewCheat(path, titleId, gameName, version) {
  try {
    const res = await fetch(`/api/cheat/read?path=${encodeURIComponent(path)}`);
    const json = await res.json();
    const data = json.data;

    document.getElementById('cheat-view-title').textContent = gameName || titleId;
    document.getElementById('cheat-view-meta').innerHTML = `
      <span class="badge-ps4">${titleId}</span>
      <span class="badge outline">Versão: ${version}</span>
      <span class="badge outline">Processo: ${data ? data.process : 'eboot.bin'}</span>
      ${data && data.credits ? `<span class="badge">Créditos: ${data.credits.join(', ')}</span>` : ''}
    `;

    const modsList = document.getElementById('cheat-view-mods');
    if (data && data.mods && data.mods.length > 0) {
      modsList.innerHTML = data.mods.map((mod, i) => `
        <div class="card p-3 mb-2" style="background: rgba(255,255,255,0.02); border-radius: 8px;">
          <div style="display: flex; justify-content: space-between; align-items: center;">
            <strong>${mod.name}</strong>
            <span class="badge" style="background: var(--ps-blue); color: #fff;">${mod.type}</span>
          </div>
          <div style="font-size: 11px; color: var(--text-muted); margin-top: 6px;">
            Offsets de memória: ${mod.memory ? mod.memory.map(m => m.offset).join(', ') : 'N/A'}
          </div>
        </div>
      `).join('');
    } else {
      modsList.innerHTML = `<pre style="font-size: 12px; background: rgba(0,0,0,0.4); padding: 12px; border-radius: 6px; overflow: auto; max-height: 300px;">${json.raw}</pre>`;
    }

    openModal('modal-cheat-view');
  } catch (err) {
    showToast(`Erro ao ler cheat: ${err.message}`, 'error');
  }
}

// 5. PKGs
async function loadPkgs() {
  const container = document.getElementById('pkgs-container');
  container.innerHTML = `<div class="p-4 text-center">Listando pacotes em /data/pkg/...</div>`;

  try {
    const res = await fetch('/api/pkgs');
    const pkgs = await res.json();
    state.pkgs = pkgs;

    if (pkgs.length === 0) {
      container.innerHTML = `<div class="p-4 text-center text-muted">Nenhum pacote .pkg encontrado em /data/pkg/. Use o botão acima para enviar.</div>`;
      return;
    }

    container.innerHTML = pkgs.map(pkg => `
      <div class="pkg-card">
        <div class="pkg-title">${pkg.name}</div>
        <div class="pkg-meta">
          <span>Tamanho: <strong>${formatBytes(pkg.size)}</strong></span>
          <span>${pkg.date}</span>
        </div>
        <div style="display: flex; gap: 8px; margin-top: 8px;">
          <a href="/api/files/download?path=${encodeURIComponent(pkg.path)}" class="btn btn-sm btn-outline flex-1 text-center">Baixar</a>
          <button class="btn btn-sm btn-danger" onclick="deleteItem('${pkg.path}', false)">Excluir</button>
        </div>
      </div>
    `).join('');

  } catch (err) {
    container.innerHTML = `<div class="p-4 text-danger">Erro ao carregar PKGs: ${err.message}</div>`;
  }
}

// 6. CAPTURES GALLERY (MÍDIA DO PS4)
async function loadCaptures(gameFilter = '') {
  state.selectedCaptureGame = gameFilter;
  const selector = document.getElementById('captures-game-selector');
  const container = document.getElementById('captures-container');

  container.innerHTML = `<div class="p-4 text-center">Carregando capturas do console...</div>`;

  try {
    // 1. Fetch games list with captures
    if (state.captureGames.length === 0) {
      const gRes = await fetch('/api/captures/games');
      state.captureGames = await gRes.json();
    }

    if (state.captureGames.length === 0) {
      selector.innerHTML = '';
      container.innerHTML = `<div class="p-4 text-center text-muted">Nenhuma captura de tela encontrada em /user/av_contents/photo/.</div>`;
      return;
    }

    // Default to first game if none selected
    if (!gameFilter && state.captureGames.length > 0) {
      gameFilter = state.captureGames[0].id;
      state.selectedCaptureGame = gameFilter;
    }

    // Render filter chips
    selector.innerHTML = state.captureGames.map(cg => `
      <button class="cap-chip ${gameFilter === cg.id ? 'active' : ''}" onclick="loadCaptures('${cg.id}')">
        ${cg.name}
      </button>
    `).join('');

    // 2. Fetch captures for selected game
    const url = `/api/captures?game=${encodeURIComponent(gameFilter)}`;
    const res = await fetch(url);
    const captures = await res.json();
    state.captures = captures;

    if (captures.length === 0) {
      container.innerHTML = `<div class="p-4 text-center text-muted">Nenhuma captura encontrada para ${gameFilter}.</div>`;
      return;
    }

    container.innerHTML = captures.map(c => `
      <div class="capture-card" onclick="openLightbox('${c.imageUrl}', '${c.titleId} • ${c.fileName}')">
        <div class="capture-img-box">
          <img src="${c.imageUrl}" alt="${c.fileName}" loading="lazy">
        </div>
        <div class="capture-meta">
          <span>${c.titleId}</span>
          <a href="${c.imageUrl}" download="${c.fileName}" class="btn btn-sm btn-outline" onclick="event.stopPropagation()">Baixar</a>
        </div>
      </div>
    `).join('');

  } catch (err) {
    container.innerHTML = `<div class="p-4 text-danger">Erro ao carregar capturas de tela: ${err.message}</div>`;
  }
}

function openLightbox(imgUrl, title) {
  document.getElementById('lightbox-img').src = imgUrl;
  document.getElementById('lightbox-title').textContent = title;
  document.getElementById('lightbox-download-btn').href = imgUrl;
  openModal('modal-lightbox');
}

// 7. Network Info & QR
async function fetchNetworkInfo() {
  try {
    const res = await fetch('/api/network');
    const net = await res.json();
    state.localIps = net.pcIps || [];

    const bestIp = net.pcIps.length > 0 ? net.pcIps[0] : 'localhost';
    const mobileUrl = `http://${bestIp}:${net.httpPort}`;

    document.getElementById('mobile-url-display').textContent = mobileUrl;
    document.getElementById('modal-qr-url').textContent = mobileUrl;

    const qrImg = `<img src="https://api.qrserver.com/v1/create-qr-code/?size=180x180&data=${encodeURIComponent(mobileUrl)}" alt="QR Code" width="180" height="180">`;
    document.getElementById('qr-container').innerHTML = qrImg;
    document.getElementById('modal-qr-target').innerHTML = qrImg;
  } catch (err) { }
}

// ================= FILE UPLOAD CONTROLLER =================
function uploadFileToPs4(file, targetDir) {
  if (!file) return;

  const modal = document.getElementById('modal-upload');
  const fileName = document.getElementById('upload-file-name');
  const fileSize = document.getElementById('upload-file-size');
  const progressFill = document.getElementById('upload-progress-fill');
  const percentText = document.getElementById('upload-percent');
  const destText = document.getElementById('upload-dest');

  fileName.textContent = file.name;
  fileSize.textContent = formatBytes(file.size);
  progressFill.style.width = '0%';
  percentText.textContent = '0%';
  destText.textContent = `Destino: ${targetDir}`;

  openModal('modal-upload');

  const xhr = new XMLHttpRequest();
  const url = `/api/files/upload?path=${encodeURIComponent(targetDir)}&filename=${encodeURIComponent(file.name)}`;

  xhr.open('POST', url, true);

  xhr.upload.onprogress = (e) => {
    if (e.lengthComputable) {
      const pct = Math.round((e.loaded / e.total) * 100);
      progressFill.style.width = `${pct}%`;
      percentText.textContent = `${pct}% (${formatBytes(e.loaded)} / ${formatBytes(e.total)})`;
    }
  };

  xhr.onload = () => {
    if (xhr.status === 200) {
      progressFill.style.width = '100%';
      percentText.textContent = '100% • Concluído!';
      showToast(`Arquivo "${file.name}" enviado com sucesso!`, 'success');
      setTimeout(() => {
        closeModal('modal-upload');
        if (state.currentTab === 'files') loadFiles(state.currentPath);
        if (state.currentTab === 'pkgs') loadPkgs();
        if (state.currentTab === 'cheats') loadCheats();
      }, 1000);
    } else {
      showToast(`Erro no envio: ${xhr.statusText}`, 'error');
    }
  };

  xhr.onerror = () => {
    showToast('Falha na comunicação durante o upload.', 'error');
  };

  xhr.send(file);
}

// ================= DRAG AND DROP =================
function initDragAndDrop() {
  const dropZone = document.getElementById('file-drop-zone');

  ['dragenter', 'dragover'].forEach(name => {
    dropZone.addEventListener(name, (e) => {
      e.preventDefault();
      dropZone.classList.add('dragover');
    });
  });

  ['dragleave', 'drop'].forEach(name => {
    dropZone.addEventListener(name, (e) => {
      e.preventDefault();
      dropZone.classList.remove('dragover');
    });
  });

  dropZone.addEventListener('drop', (e) => {
    const files = e.dataTransfer.files;
    if (files.length > 0) {
      uploadFileToPs4(files[0], state.currentPath);
    }
  });

  window.addEventListener('dragover', (e) => e.preventDefault());
  window.addEventListener('drop', (e) => {
    e.preventDefault();
    if (e.target.closest('#file-drop-zone')) return;

    const files = e.dataTransfer.files;
    if (files.length > 0) {
      const file = files[0];
      let target = state.currentPath;
      if (file.name.endsWith('.pkg')) target = '/data/pkg/';
      if (file.name.endsWith('.json')) target = '/data/GoldHEN/cheats/json/';
      uploadFileToPs4(file, target);
    }
  });
}

// ================= MODALS & FORMS =================
function initModals() {
  document.querySelectorAll('.modal-close').forEach(btn => {
    btn.addEventListener('click', () => {
      btn.closest('.modal-overlay').classList.remove('active');
    });
  });

  document.getElementById('btn-upload-file').addEventListener('click', () => {
    document.getElementById('generic-file-input').click();
  });

  document.getElementById('btn-upload-pkg-direct').addEventListener('click', () => {
    state.currentPath = '/data/pkg/';
    document.getElementById('pkg-file-input').click();
  });

  document.getElementById('btn-new-cheat').addEventListener('click', () => {
    state.currentPath = '/data/GoldHEN/cheats/json/';
    document.getElementById('cheat-file-input').click();
  });

  document.getElementById('generic-file-input').addEventListener('change', (e) => {
    if (e.target.files.length > 0) uploadFileToPs4(e.target.files[0], state.currentPath);
  });

  document.getElementById('pkg-file-input').addEventListener('change', (e) => {
    if (e.target.files.length > 0) uploadFileToPs4(e.target.files[0], '/data/pkg/');
  });

  document.getElementById('cheat-file-input').addEventListener('change', (e) => {
    if (e.target.files.length > 0) uploadFileToPs4(e.target.files[0], '/data/GoldHEN/cheats/json/');
  });

  // Cover image input
  document.getElementById('cover-file-input').addEventListener('change', (e) => {
    if (e.target.files.length > 0 && state.selectedGameIdForCover) {
      uploadNewCover(e.target.files[0], state.selectedGameIdForCover);
    }
  });

  // Quick paths clicks
  document.querySelectorAll('.path-chip').forEach(chip => {
    chip.addEventListener('click', () => {
      loadFiles(chip.getAttribute('data-path'));
    });
  });

  // Search filters
  document.getElementById('games-search-input').addEventListener('input', (e) => {
    const q = e.target.value.toLowerCase();
    const filtered = state.games.filter(g => g.name.toLowerCase().includes(q) || g.id.toLowerCase().includes(q));
    renderGames(filtered);
  });

  document.getElementById('cheats-search-input').addEventListener('input', (e) => {
    const q = e.target.value.toLowerCase();
    const filtered = state.cheats.filter(c => c.gameName.toLowerCase().includes(q) || c.titleId.toLowerCase().includes(q));
    renderCheatsList(filtered);
  });

  document.getElementById('btn-open-converter').addEventListener('click', () => {
    openModal('modal-converter');
  });

  document.getElementById('btn-run-convert').addEventListener('click', runRegionConversion);
}

async function loadConfig() {
  try {
    const res = await fetch('/api/config');
    if (res.ok) {
      const cfg = await res.json();
      if (cfg.ps4_ip) document.getElementById('cfg-ps4-ip').value = cfg.ps4_ip;
      if (cfg.ftp_port) document.getElementById('cfg-ftp-port').value = cfg.ftp_port;
      if (cfg.payload_port) document.getElementById('cfg-payload-port').value = cfg.payload_port;
      if (cfg.ps4_fw) document.getElementById('cfg-ps4-fw').value = cfg.ps4_fw;
      if (cfg.steamgriddb_api_key) {
        const sgdbInput = document.getElementById('cfg-sgdb-key');
        if (sgdbInput) sgdbInput.value = cfg.steamgriddb_api_key;
      }
      if (cfg.active_user !== undefined && cfg.active_user !== "") {
        const uSel = document.getElementById('cfg-active-user');
        if (uSel) {
          let found = false;
          for (let i = 0; i < uSel.options.length; i++) {
            if (uSel.options[i].value === cfg.active_user) {
              uSel.selectedIndex = i;
              found = true;
              break;
            }
          }
          if (!found) {
            const opt = document.createElement('option');
            opt.value = cfg.active_user;
            opt.textContent = cfg.active_user;
            opt.selected = true;
            uSel.appendChild(opt);
          }
        }
      }
    }
  } catch (err) {
    console.error('Error loading config:', err);
  }
}

function initForms() {
  loadConfig();

  document.getElementById('settings-form').addEventListener('submit', async (e) => {
    e.preventDefault();
    const ip = document.getElementById('cfg-ps4-ip').value.trim();
    const ftp = parseInt(document.getElementById('cfg-ftp-port').value);
    const payload = parseInt(document.getElementById('cfg-payload-port').value);
    const fw = document.getElementById('cfg-ps4-fw').value.trim();
    const activeUser = document.getElementById('cfg-active-user').value;
    const sgdbKey = document.getElementById('cfg-sgdb-key') ? document.getElementById('cfg-sgdb-key').value.trim() : '';

    try {
      const res = await fetch('/api/config', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          ps4_ip: ip,
          ftp_port: ftp,
          payload_port: payload,
          ps4_fw: fw,
          active_user: activeUser,
          steamgriddb_api_key: sgdbKey
        })
      });
      if (res.ok) {
        showToast('Configurações salvas com sucesso!', 'success');
        fetchStatus();
      }
    } catch (err) {
      showToast('Erro ao salvar configurações.', 'error');
    }
  });

  document.getElementById('btn-send-payload').addEventListener('click', async () => {
    const fileInput = document.getElementById('payload-file-input');
    if (fileInput.files.length === 0) {
      showToast('Selecione um arquivo .bin de payload.', 'error');
      return;
    }

    const file = fileInput.files[0];
    showToast(`Injetando payload "${file.name}" na porta 9090...`);

    try {
      const res = await fetch('/api/payload', {
        method: 'POST',
        body: file
      });
      const data = await res.json();
      if (data.success) {
        showToast(data.message, 'success');
      } else {
        showToast(data.error, 'error');
      }
    } catch (err) {
      showToast(`Falha na injeção: ${err.message}`, 'error');
    }
  });

  document.getElementById('btn-mkdir').addEventListener('click', async () => {
    const name = prompt('Nome da nova pasta:');
    if (!name) return;

    const fullPath = (state.currentPath.endsWith('/') ? state.currentPath : state.currentPath + '/') + name;
    try {
      const res = await fetch(`/api/files/mkdir?path=${encodeURIComponent(fullPath)}`, { method: 'POST' });
      if (res.ok) {
        showToast(`Pasta "${name}" criada!`, 'success');
        loadFiles(state.currentPath);
      }
    } catch (err) {
      showToast('Erro ao criar pasta.', 'error');
    }
  });
}

// ================= CHEAT REGION CONVERTER =================
async function runRegionConversion() {
  const fileInput = document.getElementById('converter-file-input');
  const sourceId = document.getElementById('converter-source-id').value.trim().toUpperCase();
  const targetId = document.getElementById('converter-target-id').value.trim().toUpperCase();
  const version = document.getElementById('converter-version').value.trim();

  if (fileInput.files.length === 0) {
    showToast('Selecione o arquivo .json do cheat original.', 'error');
    return;
  }

  const file = fileInput.files[0];
  const reader = new FileReader();

  reader.onload = async (e) => {
    const rawJson = e.target.result;
    showToast(`Convertendo ${sourceId} para ${targetId}...`);

    try {
      const res = await fetch('/api/cheat/convert', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          sourceId: sourceId,
          targetId: targetId,
          version: version,
          json: rawJson
        })
      });

      const data = await res.json();
      if (data.success) {
        showToast(`Cheat ${data.filename} convertido e enviado para o PS4!`, 'success');
        closeModal('modal-converter');
        if (state.currentTab === 'cheats') loadCheats();
      } else {
        showToast('Falha na conversão do cheat.', 'error');
      }
    } catch (err) {
      showToast(`Erro: ${err.message}`, 'error');
    }
  };

  reader.readAsText(file);
}

// ================= FILE ACTIONS =================
async function deleteItem(path, isDir) {
  if (!confirm(`Deseja realmente excluir "${path}"?`)) return;

  try {
    const res = await fetch(`/api/files/delete?path=${encodeURIComponent(path)}&isDir=${isDir}`, { method: 'POST' });
    if (res.ok) {
      showToast('Item excluído com sucesso.', 'success');
      if (state.currentTab === 'files') loadFiles(state.currentPath);
      if (state.currentTab === 'pkgs') loadPkgs();
    }
  } catch (err) {
    showToast('Erro ao excluir item.', 'error');
  }
}

// ================= UTILITIES =================
function openModal(id) {
  document.getElementById(id).classList.add('active');
}

function closeModal(id) {
  document.getElementById(id).classList.remove('active');
}

function showToast(msg, type = 'info') {
  const container = document.getElementById('toast-container');
  const toast = document.createElement('div');
  toast.className = `toast ${type}`;
  toast.textContent = msg;

  container.appendChild(toast);
  setTimeout(() => {
    toast.style.opacity = '0';
    setTimeout(() => toast.remove(), 300);
  }, 3500);
}

function formatBytes(bytes, decimals = 1) {
  if (!+bytes) return '0 B';
  const k = 1024;
  const dm = decimals < 0 ? 0 : decimals;
  const sizes = ['B', 'KB', 'MB', 'GB', 'TB'];
  const i = Math.floor(Math.log(bytes) / Math.log(k));
  return `${parseFloat((bytes / Math.pow(k, i)).toFixed(dm))} ${sizes[i]}`;
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
      desc: "Acompanhe aqui em tempo real a latência (ping), o status do servidor FTP (2121) do GoldHEN e a porta de injeção de Payloads (9090)."
    },
    en: {
      title: "2. Console Status & Network",
      desc: "Check real-time connectivity, latency in milliseconds, and status for GoldHEN FTP (2121) and BinLoader (9090) ports."
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

  prevBtn.style.display = index === 0 ? 'none' : 'inline-block';
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
