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
  localIps: []
};

// ================= INITIALIZATION =================
document.addEventListener('DOMContentLoaded', () => {
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
          showToast('Falha ao enviar notificação.', 'error');
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
  const titleMap = {
    dashboard: { title: 'Dashboard', sub: 'Central de Controle do PlayStation 4' },
    games: { title: 'Jogos & Aplicativos', sub: 'Biblioteca instalada e estatísticas de jogo' },
    files: { title: 'Gerenciador de Arquivos', sub: 'Explorador FTP do sistema de arquivos' },
    cheats: { title: 'Cheats & Mods', sub: 'Gerenciador de Cheats do GoldHEN' },
    pkgs: { title: 'Pacotes PKG', sub: 'Instalação e gerenciamento de arquivos .pkg' },
    media: { title: 'Capturas & Prints', sub: 'Screenshots gravados diretamente no console' },
    tools: { title: 'Payloads & Ações', sub: 'Ferramentas do sistema, notificações OSD, RPI e injeção' },
    settings: { title: 'Configurações', sub: 'Parâmetros de rede e conexão com o console' },
    donate: { title: 'Apoiar o Projeto', sub: 'Contribua com o desenvolvimento contínuo do Orbis Web Manager' }
  };

  const t = titleMap[tabId] || titleMap.dashboard;
  document.getElementById('page-title-text').textContent = t.title;
  document.getElementById('page-subtitle-text').textContent = t.sub;

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
  event.stopPropagation();
  state.selectedGameIdForCover = gameId;
  const input = document.getElementById('cover-file-input');
  input.value = '';
  input.click();
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

function initForms() {
  document.getElementById('settings-form').addEventListener('submit', async (e) => {
    e.preventDefault();
    const ip = document.getElementById('cfg-ps4-ip').value.trim();
    const ftp = parseInt(document.getElementById('cfg-ftp-port').value);
    const payload = parseInt(document.getElementById('cfg-payload-port').value);

    try {
      const res = await fetch('/api/config', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ ps4_ip: ip, ftp_port: ftp, payload_port: payload })
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
