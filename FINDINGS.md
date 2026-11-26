# Achados

Notas internas, em português. O ambiente, o carregamento de mods e o código protegido do jogo estão descritos no FINDINGS do UI Themes (`xupisco/dw2-uithemes`). Aqui fica só o que é do Overview. Levantado em 2026-11-26 por descompilação (só nomes e assinaturas; os corpos são protegidos) e por testes no jogo.

## API externa

- O jogo não tem nenhuma: nada de HTTP, sockets, pipes ou linguagem de script nos assemblies, e os saves são binários.
- O jogo traz o .NET 8 junto (`includedFrameworks` no `runtimeconfig`), com `System.Net.HttpListener.dll` e `System.Text.Json.dll` na pasta. O servidor do mod usa só isso.
- `HttpListener` em `http://localhost:<porta>/` funciona sem administrador. **confirmado**

## Dados

| O quê | Onde | Estado |
|---|---|---|
| Império do jogador | `ScaledRenderer.PlayerEmpire` (`DWGame.Renderer`) | confirmado |
| Galáxia | `DWGame.Galaxy`; `Galaxy.Ships`, `Galaxy.Orbs`, `Galaxy.Designs` (`GetById`), `Galaxy.Systems` (`TryGetValue`) | confirmado |
| Data | `Galaxy.StarDate`; texto com `TextHelper.ResolveStarDateDescription(starDate)` (ex.: `2757.05.11`) | confirmado |
| Estaleiros em naves e bases | `Ship.ConstructionSystem` | confirmado |
| Estaleiros em colônias | `Orb.Colony.ConstructionSystem` (o `Orb` não tem campo próprio) | lido, sem item visto ainda |
| Conteúdo do estaleiro | `ConstructionSystem.Yards` (`ConstructionYard.ShipUnderConstructionId`, `DesignId`, `ConstructionType`) e `WaitQueue` (`ConstructionYardWait`, mesmos campos) | confirmado. Item na fila já é uma `Ship` com nome, em 0% (2026-11-26) |
| Estaleiro parado | `ShipUnderConstructionId == 0` e `ConstructionType == Undefined`. O `IsEmpty` disse `false` nesses casos, então não serve | confirmado |
| Nave em construção | já é uma `Ship` (`Galaxy.Ships.GetById`), com `ConstructionLevel` de 0 a 1 subindo ao vivo | confirmado |
| Base feita por nave de construção | fica no estaleiro da própria nave de construção | confirmado |
| Instalações planetárias | `Empire.Colonies` → `Orb.Facilities` → `PlanetaryFacility.ConstructionProgress` | lido, nenhuma em construção vista |
| Pesquisa | `Empire.Research` (`ResearchSystem`): `ResearchQueue` (a fila do jogador), `LatestProjects` (pesquisadas, inclusive as iniciais, com `ProgressRatio` 0 nessas), `NextProjects` (disponíveis fora da fila; o Leandro não as enfileirou). `ResearchProject.ProgressRatio` 0 a 1, `Definition.Name`, `Definition.Size`, `Speed`; `Empire.ConcurrentResearchProjects` = 1 no início | confirmado (2026-11-26) |
| Colônias | `Colony`: `Population.TotalAmount`, `MaximumPopulation`, `DevelopmentLevel`, `TaxRate`, `Corruption`, `EmpireApprovalRating`, `RacialHappiness`, `Unrest`, `IsRebelling`, `Automate*`; `CalculateAnnualRevenue(orb, empire, corruption, true)`; `Orb.Quality`. Goma no início: pop 2,3 bi / máx 5,67 bi, development 0,576, approval 6,76, corruption 0,249, tax 0,16, revenue 31.743, quality 0,82. No painel da colônia: 57% (development), 25% (corruption), "33K @ 16%", aprovação 7, 2301M, Quality 82%, +4,4% (crescimento) | confirmado (2026-11-26): development, corruption, tax e quality vão de 0 a 1; approval é o número cru arredondado. A receita com `includeSupportCosts: true` deu 31.744, exatamente 1.000 a menos que o "Private Colony Revenue, Projected" (32.744) do painel do império; agora sem os custos de suporte, a conferir. `CalculateGrowthRate(orb, empire)` acrescentado, escala a conferir com o +4,4% |
| Economia | `Empire.Economy.Money` (caixa), `PrivateMoney`, `Empire.MoneyAvailable`, `WarWeariness`; fluxo de caixa: candidatos `CalculateCashflowState(1, true)` e `CalculateCashflowActual(1, true)`, a comparar com o "Cashflow +424" do painel | só nomes |
| Império | `Policy`, `GovernmentId`, `ResourcesKnown`, `CriticalMissingResources`, `ResourceShortages`, listas por tipo de nave | só nomes |

## Comandos

| O quê | Como | Estado |
|---|---|---|
| Selecionar e centralizar | `ScaledRenderer.SetSelectedObject(obj)` + `MoveViewNoZoom(obj)`, na thread do jogo | confirmado (salto brusco) |
| Alternativas com animação | `ScaledRenderer.ZoomToItem(item, gameTime, timePassed, timeInSeconds)`, `ZoomToItemButtonClick(sender, args)` | não testado |
| Também existem | `UserInterfaceController.MoveViewToItemHandler`, `SetSelectedObjectHandler` (delegates estáticos) | não testado |

## Janela dentro do jogo (caminho não escolhido)

`UserInterfaceController.AddCustomControl(DWControl)` / `RemoveCustomControl` são públicos. Peças: `DWPanel`, `Dialog`, `ScrollableList`, `DWTextBox`, `DWButton`, `PopupList`. As listas das seções saem de `SubSection.ResolveListItems(DrawDetailType, …)`, e `Ship` e `PlanetaryFacility` implementam `IListDrawable`.

## Teclado

Os atalhos do jogo ignoram modificadores (Ctrl+Alt+letra também dispara a letra).
