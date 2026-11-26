# Plano

Notas internas, em português. Achados sobre o jogo em [FINDINGS.md](FINDINGS.md).

## Decisões

- **2026-11-26:** o Overview é uma página HTML servida pelo próprio mod (servidor em localhost), não uma janela dentro do jogo. É mais fácil de testar e evoluir (F5 sem reiniciar o jogo) e não exige instalar nada. Um app Tauri só depois, e se fizer falta; por isso a API fica limpa (`/api/info` com versão, leitura por GET, comandos por POST com token).
- **2026-11-26:** estaleiros parados aparecem como `idle`. O filtro padrão é "Everything" (pedido do Leandro); "Active" esconde os parados.
- **2026-11-26:** acabamento visual fica para depois (o Leandro não está convencido da seta ▸ para expandir).
- **2026-11-26:** visão padrão "One row per yard": o primeiro item do estaleiro (na ordenação atual) representa a linha, e os outros se expandem embaixo (▸ +N). Os filtros agem sobre os itens. "Every item" mostra a lista plana. A página só redesenha quando os dados mudam, para não engolir cliques.
- Nome exibido provisório: "Empire Overview".

## Sequência

1. [x] Construção: estaleiros, filas, instalações, estaleiros parados. Testado no jogo em 2026-11-26.
2. [x] Clique na linha seleciona e centraliza o estaleiro ou a colônia. Testado em 2026-11-26 (movimento brusco).
   - [ ] Clique no nome de uma nave em construção (vai até a nave): falta testar.
   - [ ] Movimento suave: testar `ZoomToItemButtonClick` ou `ZoomToItem` no lugar de `MoveViewNoZoom`.
   - [x] Item na fila de espera: aparece com nome, design e posição na fila (2026-11-26).
   - [ ] Ainda não vista no jogo: instalação em construção.
3. [x] Repositório próprio (`xupisco/dw2-overview`).
4. [ ] Colônias e pesquisa: novas abas com tabela (população, aprovação, corrupção, impostos; fila de pesquisa com %). Conferir as escalas contra a tela do jogo.
5. [ ] Command palette v1: `/api/index` (sistemas, colônias, naves, bases, frotas) e Ctrl+K com busca fuzzy, setas e Enter indo pelo `/api/goto`.
6. [ ] Comandos na palette, um por vez (pausa e velocidade, abrir telas).
7. [ ] Mais visões: naves e frotas paradas, recursos em falta.
8. [ ] Acabamento para publicar: opções pelo launcher (porta), descrição da Workshop em inglês com crédito ao Claude, nome final.
