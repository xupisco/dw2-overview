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
| Conteúdo do estaleiro | `ConstructionSystem.Yards` (`ConstructionYard.ShipUnderConstructionId`, `DesignId`, `ConstructionType`) e `WaitQueue` (`ConstructionYardWait`, mesmos campos) | confirmado (fila ainda não vista) |
| Estaleiro parado | `ShipUnderConstructionId == 0` e `ConstructionType == Undefined`. O `IsEmpty` disse `false` nesses casos, então não serve | confirmado |
| Nave em construção | já é uma `Ship` (`Galaxy.Ships.GetById`), com `ConstructionLevel` de 0 a 1 subindo ao vivo | confirmado |
| Base feita por nave de construção | fica no estaleiro da própria nave de construção | confirmado |
| Instalações planetárias | `Empire.Colonies` → `Orb.Facilities` → `PlanetaryFacility.ConstructionProgress` | lido, nenhuma em construção vista |
| Pesquisa | `Empire.Research` (`ResearchSystem`): `ResearchQueue`, `NextProjects`, `LatestProjects`, `GetTopMostQueuedProject()`; `ResearchProject.Progress`, `ProgressRatio`; `Empire.ConcurrentResearchProjects` | só nomes |
| Colônias | `Colony`: `Population`, `MaximumPopulation`, `DevelopmentLevel`, `TaxRate`, `Corruption`, `EmpireApprovalRating`, `RacialHappiness`, `IsRebelling`, `Automate*` | só nomes |
| Império | `Policy`, `GovernmentId`, `ResourcesKnown`, `CriticalMissingResources`, `ResourceShortages`, listas por tipo de nave | só nomes; dinheiro e renda ainda não achados |

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
