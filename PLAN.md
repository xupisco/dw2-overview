# Plano

Notas internas, em português. Achados sobre o jogo em [FINDINGS.md](FINDINGS.md).

## Decisões

- **2026-11-26:** o Overview é uma página HTML servida pelo próprio mod (servidor em localhost), não uma janela dentro do jogo. É mais fácil de testar e evoluir (F5 sem reiniciar o jogo) e não exige instalar nada. Um app Tauri só depois, e se fizer falta; por isso a API fica limpa (`/api/info` com versão, leitura por GET, comandos por POST com token).
- **2026-11-26:** estaleiros parados aparecem como `idle`, escondidos pelo filtro padrão "Active".
- Nome exibido provisório: "Empire Overview".

## Sequência

1. [x] Construção: estaleiros, filas, instalações, estaleiros parados. Testado no jogo em 2026-11-26.
2. [x] Clique na linha seleciona e centraliza o estaleiro ou a colônia. Testado em 2026-11-26 (movimento brusco).
   - [ ] Clique no nome de uma nave em construção (vai até a nave): falta testar.
   - [ ] Movimento suave: testar `ZoomToItemButtonClick` ou `ZoomToItem` no lugar de `MoveViewNoZoom`.
   - [ ] Ainda não vistos no jogo: item na fila de espera, instalação em construção.
3. [x] Repositório próprio (`xupisco/dw2-overview`).
4. [ ] Colônias e pesquisa: novas abas com tabela (população, aprovação, corrupção, impostos; fila de pesquisa com %). Conferir as escalas contra a tela do jogo.
5. [ ] Command palette v1: `/api/index` (sistemas, colônias, naves, bases, frotas) e Ctrl+K com busca fuzzy, setas e Enter indo pelo `/api/goto`.
6. [ ] Comandos na palette, um por vez (pausa e velocidade, abrir telas).
7. [ ] Mais visões: naves e frotas paradas, recursos em falta.
8. [ ] Acabamento para publicar: opções pelo launcher (porta), descrição da Workshop em inglês com crédito ao Claude, nome final.
