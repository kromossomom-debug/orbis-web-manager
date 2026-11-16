# Orbis Web Manager — Design System

Baseado no método das skills de [awesome-design-skills](https://github.com/bergside/awesome-design-skills)
(estrutura: intenção → tokens → componentes → acessibilidade → tom → anti-padrões → checklist),
combinando as direções **Sleek** ("less is more", paleta 60-30-10, grade de 8pt, interações sutis)
e **Premium** (espaçamento preciso, tipografia moderna, acabamento polido), adaptadas a um tema escuro
de console.

## 1. Intenção
Um painel de controle do PS4 que responde em um olhar: **o console está conectado? o que eu faço agora?**
Calmo, escuro, focado no conteúdo (capas dos jogos) e com um único acento azul para ações.

## 2. Tokens (fonte da verdade: `:root` em `wwwroot/css/style.css`)

| Grupo | Tokens |
|---|---|
| Superfícies (60%) | `--bg #0b0d12`, `--surface #12151c`, `--surface-2 #181c25`, `--surface-3 #20252f` |
| Texto | `--text #eef1f6` (≥15:1), `--text-2 #b4bccb` (≥9:1), `--text-3 #8f99ab` (≥6:1 sobre `--surface`) |
| Bordas | `--border` 7% branco, `--border-strong` 12% branco |
| Acento (10%) | `--accent #3b8bff`, `--accent-hover #5c9dff`, `--accent-soft` 14% |
| Semânticas | `--success #34d399`, `--warning #fbbf24`, `--danger #f87171` — **apenas** para status |
| Tipografia | Inter (UI) / JetBrains Mono (IDs, caminhos, tamanhos). Escala 12 · 13 · 14 · 16 · 20 · 24 · 32 |
| Espaço | grade de 8pt: `--s1 4` `--s2 8` `--s3 12` `--s4 16` `--s5 24` `--s6 32` `--s7 48` |
| Raio | `--r-sm 6` `--r-md 10` `--r-lg 14` `--r-xl 20` |
| Sombra | `--shadow-sm`, `--shadow-md`, `--shadow-lg` (só em elementos flutuantes: modal, toast, menu) |
| Movimento | `--fast 120ms`, `--base 200ms`, `ease-out`; desligado com `prefers-reduced-motion` |

## 3. Componentes
- **Botão**: `primary` (acento, 1 por área), `outline` (secundário), `ghost` (terciário), `danger` (destrutivo).
  Estados: default, hover, `:focus-visible`, active, disabled (`opacity .5`, sem hover). Altura 36px (40px no toque).
  Botões só com ícone **devem** ter `aria-label`.
- **Card**: `--surface`, borda `--border`, raio `--r-lg`, padding `--s5`. Sem brilho colorido; hierarquia por tipografia.
- **Badge/Chip**: 12px, raio total. Cores semânticas só quando transmitem estado (online, cheats, aberta/fechada).
- **Navegação**: agrupada por tarefa (Início · Biblioteca · Console · Ferramentas). Item ativo = fundo `--accent-soft` + texto acento + `aria-current="page"`.
- **Modal**: `role="dialog"`, `aria-modal`, título ligado por `aria-labelledby`, fecha com Esc e clique fora, foco vai para o diálogo.
- **Toast**: região `aria-live="polite"`, ícone + texto, borda esquerda semântica.
- **Estados vazios / erro**: sempre dizem **o que fazer a seguir** (ex.: "Verifique o IP em Configurações").
- **Carregamento**: skeleton nas grades (jogos), spinner pequeno inline nas ações.

## 4. Acessibilidade (WCAG 2.2 AA)
- Texto normal ≥ 4.5:1; texto grande/ícones ≥ 3:1 — todos os tokens de texto acima passam sobre `--surface-2`.
- Todo elemento interativo alcançável por teclado, com anel de foco de 2px `--accent` + 2px de deslocamento.
- Alvos de toque ≥ 40×40px em telas ≤ 768px.
- Status nunca só por cor: sempre acompanhado de texto ("Aberta", "Fechada", "Online").
- `prefers-reduced-motion: reduce` remove animações e transições.

## 5. Tom de texto
Conciso, confiante, útil. Verbos no imperativo nos botões ("Trocar capa", "Testar portas").
Erros explicam a causa e a próxima ação.

## 6. Anti-padrões (não fazer)
- ❌ Várias cores de acento competindo (roxo, ciano, âmbar nas bordas dos cards) → ✅ um acento, semânticas só para status.
- ❌ Brilhos/glow decorativos e gradientes em tudo → ✅ profundidade por camadas de superfície.
- ❌ `style="..."` inline para layout → ✅ classes utilitárias/tokens.
- ❌ Texto cinza escuro de baixo contraste (`#64748b` sobre preto) → ✅ `--text-3`.
- ❌ Espaçamentos arbitrários (13px, 22px) → ✅ múltiplos de 4/8.

## 7. Checklist de QA (revisão de código)
- [ ] Nenhuma cor/tamanho "solto": usa tokens.
- [ ] Todo botão de ícone tem `aria-label`.
- [ ] Foco visível em todos os controles (navegue só com Tab).
- [ ] Estados vazio, carregando e erro existem e orientam o usuário.
- [ ] Funciona em 375px de largura sem rolagem horizontal.
- [ ] Testado com `prefers-reduced-motion`.
