# Plano

Notas internas, em português. Achados sobre o jogo em [FINDINGS.md](FINDINGS.md).

## Decisões

- **2026-11-26:** o Overview é uma página HTML servida pelo próprio mod (servidor em localhost), não uma janela dentro do jogo. É mais fácil de testar e evoluir (F5 sem reiniciar o jogo) e não exige instalar nada. Um app Tauri só depois, e se fizer falta; por isso a API fica limpa (`/api/info` com versão, leitura por GET, comandos por POST com token).
- **2026-11-26:** estaleiros parados aparecem como `idle`. O filtro padrão é "Everything" (pedido do Leandro); "Active" esconde os parados.
- **2026-11-26:** acabamento visual fica para depois (o Leandro não está convencido da seta ▸ para expandir).
- **2026-11-26:** visão padrão "One row per yard": o primeiro item do estaleiro (na ordenação atual) representa a linha, e os outros se expandem embaixo (▸ +N). Os filtros agem sobre os itens. "Every item" mostra a lista plana. A página só redesenha quando os dados mudam, para não engolir cliques.
- Nome exibido provisório: "Empire Overview".

## Wishlist (sem ordem, depois da sequência)

- **Detalhe da pesquisa:** ao abrir um projeto, mostrar o que ele libera, como o tooltip do jogo: componentes (`ResearchProjectDefinition.Components`), instalações (`PlanetaryFacilityIds`), cascos (`ShipHulls`), bônus (`EmpireBonuses`), e também os dias que faltam e o custo do crash program. O tooltip do jogo mostra "211 days to research", "55% Complete" (bate com o nosso `ProgressRatio`) e "Crash Program for 3,384 credits from Cash on Hand of 9,320 credits".
- **Dinheiro em caixa** no cabeçalho: feito, com o fluxo de caixa (`Economy.Money`, `CalculateCashflowActual`).

## Sequência

1. [x] Construção: estaleiros, filas, instalações, estaleiros parados. Testado no jogo em 2026-11-26.
2. [x] Clique na linha seleciona e centraliza o estaleiro ou a colônia. Testado em 2026-11-26 (movimento brusco).
   - [x] Clique no nome de uma nave em construção (vai até a nave): testado em 2026-11-26 com a Kintaris Dream (53%) e a Solitary Gamble (na fila, 0%), sem erro no log.
   - [ ] Movimento suave: testar `ZoomToItemButtonClick` ou `ZoomToItem` no lugar de `MoveViewNoZoom`.
   - [x] Item na fila de espera: aparece com nome, design e posição na fila (2026-11-26).
   - [ ] Ainda não vista no jogo: instalação em construção.
3. [x] Repositório próprio (`xupisco/dw2-overview`).
4. [x] Colônias e pesquisa: novas abas com tabela (população, aprovação, corrupção, impostos; fila de pesquisa com %). Conferir as escalas contra a tela do jogo.
   - 2026-11-26: abas Colonies e Research feitas (`/api/colonies`, `/api/research`) e testadas no jogo (coleta total 0,14 ms). Research mostra só a fila por padrão. Falta conferir as escalas das colônias contra a tela do jogo e formatar (%).
5. [x] Mensagens e alertas (pedido do Leandro): `Empire.Messages` / `RecentMessages` (`EmpireMessage`: `Title`, `Text`, `Type`, `SubType`, `Priority`, `StarDate`, `RelatedObject`). Aba com a lista (clique vai até o `RelatedObject`) e notificações do navegador (`Notification` API em localhost). Testado em 2026-11-26: lista, clique até o objeto e notificação do Windows ("New Research Station Completed") funcionam. Falta: botões das sugestões do conselheiro (nenhuma com ação apareceu ainda).
6. [x] Command palette v1: Ctrl+K na página, busca fuzzy nas próprias naves, bases, colônias e frotas (`/api/index`) e, para o resto do que o império conhece, a busca do próprio jogo (`/api/search?q=`, `ResolveKnownItems` na thread do jogo; com busca vazia ele não devolve nada). Testado no jogo em 2026-11-26 (busca, setas, Enter e ir até).
7. [ ] Comandos na palette, um por vez (pausa e velocidade, abrir telas).
8. [ ] Mais visões: naves e frotas paradas, recursos em falta.
   - 2026-11-26: aba Ships (`/api/ships`: missão, alvo, próximas missões, ordens, frota, dano, combustível, atracada) com filtros montados dos dados; "Idle" = sem missão. Falta testar no jogo (DLL).
9. [ ] Acabamento para publicar: opções pelo launcher (porta), descrição da Workshop em inglês com crédito ao Claude, nome final.
