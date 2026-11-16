# 🎮 Orbis Web Manager (OWM)
### *A Suíte Web Definitiva para PlayStation 4 & GoldHEN*
> Inspirado no **Prospero Manager** do PlayStation 5, desenvolvido especialmente para o ecossistema do **PlayStation 4 com GoldHEN**.

---

## 🌟 O que é o Orbis Web Manager?
O **Orbis Web Manager** é uma central de comando e gerenciamento completa que roda diretamente no navegador do seu **computador, tablet ou smartphone**.

Ele se conecta diretamente ao seu **PlayStation 4** através da rede local (Wi-Fi ou cabo), permitindo controlar o console, visualizar biblioteca de jogos, tempo jogado, gerenciar arquivos, instalar pacotes PKG, adicionar e converter cheats do GoldHEN, e injetar payloads com total facilidade e sem precisar de pendrive.

---

## ✨ Principais Recursos

### 1. 📊 Dashboard & Telemetria em Tempo Real
* **Status de Conexão:** Monitoramento contínuo de ping/latência e conectividade com o console.
* **Verificação de Portas:** Status das portas `2121` (FTP Server) e `9090` (BinLoader Server).
* **Perfil do Usuário:** Identificação automática do usuário ativo no console.
* **Resumo do Sistema:** Contagem de jogos instalados, cheats ativos e total de espaço ocupado por PKGs em `/data/pkg/`.

### 2. 🕹️ Biblioteca de Jogos (Game Library)
* **Capas em Alta Definição:** Ícones oficiais (`icon0.png`) carregados diretamente do disco do PS4.
* **Títulos Oficiais:** Nomes dos jogos extraídos diretamente do banco de dados oficial do console (`app.db`).
* **Estatísticas de Jogo:** Tempo total jogado (*Playtime*) e quantidade de inicializações extraídos do registro do GoldHEN.
* **Indicador de Cheats:** Selo visual destacando quais jogos possuem cheats instalados.

### 3. 📂 Explorador de Arquivos Completo (PS4 FTP Explorer)
* **Navegação em Árvore e Tabela:** Navegue por `/data/`, `/user/`, `/system/`, `/mnt/`, etc.
* **Atalhos Rápidos:** Acesso em 1 clique para pastas essenciais (`/data/pkg`, `/data/GoldHEN/cheats/json`, `/data/GoldHEN/plugins`, `/user/home`).
* **Upload por Arrastar e Soltar (Drag & Drop):** Arraste arquivos do PC ou celular direto para a janela do navegador.
* **Download e Exclusão:** Baixe arquivos do PS4 para o computador ou remova itens com segurança.
* **Criar Pastas:** Criação instantânea de diretórios.

### 4. ⚡ Cheats & Mods (GoldHEN Suite)
* **Visualizador de Cheats:** Veja todos os cheats JSON instalados com detalhes dos mods (*God Mode*, *Itens Máximos*, *Recursos Infinitos*).
* **Conversor de Região CUSA Integrado:**
  * Baixou um cheat de outra região (ex: Americana `CUSA00242`) mas seu jogo é brasileiro (`CUSA00243`)?
  * A ferramenta converte o Title ID internamente e faz o upload automático para o PS4 na pasta certa com 1 clique!

### 5. 📦 Gerenciador de PKGs
* Lista todos os pacotes `.pkg` presentes na pasta `/data/pkg/`.
* Exibe tamanho formatado (MB/GB) e data.
* Uploader dedicado de PKGs com streaming contínuo.
* Opção de excluir PKGs após instalação para liberar espaço no HD interno.

### 6. 📸 Galeria de Capturas & Mídias do PS4
* Navegue e visualize todas as capturas de tela tiradas com o botão Share.
* Filtro automático por jogo com capas em miniatura.
* Lightbox em tela cheia com botão de download em alta resolução direto para o seu computador.

### 7. 🎨 Troca de Capas dos Jogos (Custom Covers)
* Altere a arte de capa de qualquer jogo (`icon0.png`) diretamente pela interface web com upload instantâneo para o console.

### 8. 🚨 Backup de Emergência do Banco de Dados (app.db)
* Baixe com 1 clique uma cópia íntegra de segurança do `app.db` (`/system_data/priv/mms/app.db`) do seu PS4 para prevenir perda de biblioteca.

### 9. 🔔 Notificações na TV (Notify OSD)
* Envie mensagens personalizadas que pipocam imediatamente na tela da TV enquanto você estiver jogando no PlayStation 4.

### 10. ⚡ Instalador Remoto de Pacotes (RPI)
* Dispare a instalação remota de PKGs servidos em rede diretamente na porta `12800` do GoldHEN sem precisar copiar o instalador para o HD interno antes.

### 11. 📱 Acesso pelo Celular (Mobile First)
* Interface 100% responsiva com **Bottom Navigation Bar** tátil no estilo app nativo.
* **QR Code Integrado:** Basta apontar a câmera do celular para abrir o painel na hora via Wi-Fi.

### 12. 💡 Tour Interativo (Tutorial Integrado)
* Balões guiados passo a passo que ensinam o usuário desde a conexão inicial de IP do PS4 até o uso de cheats, capas e backups.

### 13. 💉 Injeção de Payloads
* Envie arquivos `.bin` diretamente para a porta `9090` do GoldHEN sem precisar de programas externos.

---

## ☕ Apoie o Projeto

O **Orbis Web Manager** é mantido e desenvolvido como software livre para a comunidade PS4 Jailbreak. Se o projeto te ajudou e você deseja incentivar a criação de novos recursos, considere fazer uma contribuição:

* **🇧🇷 Pix (Brasil - Chave Aleatória):**
  ```text
  94af8068-d112-4c69-bcfa-8789b233246e
  ```
  *(Você também pode escanear o QR Code interativo direto na aba "Apoiar Projeto" dentro do próprio aplicativo!)*

* **🌍 Buy Me a Coffee (Apoio Internacional):**
  [![Buy Me A Coffee](https://img.shields.io/badge/Buy%20Me%20A%20Coffee-kromossomoc-orange?style=for-the-badge&logo=buy-me-a-coffee)](https://buymeacoffee.com/kromossomoc)

  🔗 Link direto: [buymeacoffee.com/kromossomoc](https://buymeacoffee.com/kromossomoc)

---

## 🌐 Idiomas / Multilingual Support
O aplicativo conta com suporte bilíngue nativo com troca instantânea no cabeçalho:
* 🇧🇷 **Português (Brasil)**
* 🇺🇸 **English (US)**

## 🚀 Como Iniciar

1. Certifique-se de que o PlayStation 4 está ligado com o GoldHEN ativo na mesma rede local.
2. Dê dois cliques no arquivo:
   ```cmd
   start.bat
   ```
3. O servidor iniciará e o navegador abrirá automaticamente em:
   * **No Computador:** `http://localhost:5000`
   * **No Celular / Tablet:** `http://<IP_DO_SEU_PC>:5000` (ou escaneie o QR Code na tela).
4. Na aba **Configurações**, informe o IP do seu PlayStation 4 (ex: `192.168.1.100`) e clique em Salvar!

---

## 🛠️ Arquitetura & Tecnologia
* **Zero Dependências Externas:** Não requer Node.js, Python, npm ou pip instalados. Compilado nativamente com o compilador C# (`csc.exe`) do próprio Windows.
* **Alta Performance:** Servidor multi-thread assíncrono baseado em `HttpListener` e streaming direto de sockets.
* **Design System:** Tema PlayStation Dark Nebula com CSS3 moderno, glassmorphism e animações fluidas.

---

## 📄 Licença
Distribuído sob a licença MIT. Veja `LICENSE` para mais informações.
