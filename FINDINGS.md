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
| Colônias | `Colony`: `Population.TotalAmount`, `MaximumPopulation`, `DevelopmentLevel`, `TaxRate`, `Corruption`, `EmpireApprovalRating`, `RacialHappiness`, `Unrest`, `IsRebelling`, `Automate*`; `CalculateAnnualRevenue(orb, empire, corruption, true)`; `Orb.Quality`. Goma no início: pop 2,3 bi / máx 5,67 bi, development 0,576, approval 6,76, corruption 0,249, tax 0,16, revenue 31.743, quality 0,82. No painel da colônia: 57% (development), 25% (corruption), "33K @ 16%", aprovação 7, 2301M, Quality 82%, +4,4% (crescimento) | confirmado (2026-11-26): development, corruption, tax e quality vão de 0 a 1; approval é o número cru arredondado. A receita com `includeSupportCosts: true` deu 31.744, exatamente 1.000 a menos que o "Private Colony Revenue, Projected" (32.744) do painel do império; sem os custos de suporte deu 32.744 e bate (`includeSupportCosts: false`). `CalculateGrowthRate(orb, empire)` deu 0,7287, que não corresponde ao +4,4% do painel: fica na API, fora da página |
| Economia | `Empire.Economy.Money` (caixa), `PrivateMoney`, `Empire.MoneyAvailable`, `WarWeariness`; fluxo de caixa = `CalculateCashflowActual(1, true)` (423,9 contra "+424" no painel; o `CalculateCashflowState` deu -1.884) | confirmado (2026-11-26): caixa 9.320, privado 168.348 |
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

## Atalhos do jogo como comandos

- `DWGame.DWSettings.KeyMappings` (`KeyMapping`: `Key`, `KeyName`, `ShiftActive`, `UiSection`, `TaskType`, `QueueBaseRole`, `CockpitTaskType`, `Description`) traz os atalhos com os remapeamentos do jogador. `Description` veio vazio em todos.
- `DWGame.InputSystem.HandleKeyPress(galaxy, mapping, gameTime, dt, total, dt)` executa um atalho como se a tecla fosse apertada; roda no postfix de `DWGame.Update(GameTime gameTime)`. **confirmado** (2026-11-26): pausa, telas (Research, Colonies) e ações sem tecla atribuída.
- `DWGame.GetGamePaused()` e `GetGameSpeed()` dão o estado do relógio; `PauseGame`, `ResumeGame`, `ChangeGameSpeed` existem, não usados.

## Recursos

Conferido contra as telas do jogo em 2026-11-26 (painel do império > Resources, e "Resources & Stock Levels").

| O quê | Onde | Estado |
|---|---|---|
| Estoque ("On Hand") | `Empire.CalculateTotalCargoLevels(empireId)` | confirmado (Caslon 4.627, Carbonite 1.104, Cuprica 4.171) |
| Nível a manter ("Maintain") | `Empire.CalculateTotalStockLevels(false)` | confirmado (Caslon 20.000, Carbonite 528, Emeros 100) |
| Taxa por segundo da tela | `ResourceRatesCurrent - ResourceRatesRequired` | confirmado (Caslon 0 - 16,67 = -16,67/s) |
| Preços ("Buy", "Sell") | `ResourcePricesBuy`, `ResourcePricesSell` | confirmado |
| Categorias | as marcas `Resource.IsFuel`, `IsConstruction`, `IsLuxury`. A linha Construction da tabela do jogo tem **todos** os 16 recursos de construção, mesmo os que o império não tem (Dyrillium Quartz e Hexodorium, críticos por estoque zero); Luxury só os que têm demanda ou estoque; Fuel o Caslon. As listas do império (`DetermineConstructionResourcesByTech`, só os da tecnologia atual, 7) não são a regra | confirmado (2026-11-26, listas candidatas no log) |
| Tabela Supply (Adequate / Low / Critical) | crítico = estoque zero (recurso de construção ou combustível, ou com demanda), ou estoque abaixo de metade do Maintain; baixo = abaixo do Maintain | bate com o jogo (Fuel 0/0/1, Construction 14/0/2, Luxury 3/0/2); o limite de "metade" é palpite, não houve "Low" para conferir |
| Não servem | `ResourceRateRequirementsMet` (false para tudo), `CriticalMissingResources` (vazia) | confirmado |
| "Sources" do jogo | conta algo além dos depósitos conhecidos (Emeros "0 of 1" sem depósito conhecido); a nossa coluna se chama Deposits | a investigar |
| Depósitos | `Empire.ExplorationMap.GetAllKnownOrbs(galaxy, empire, 1)`; `Orb.Resources` (`OrbResource.ResourceId`, `Abundance` em %, `DiscoveryLevel` comparado a `ExplorationMap.CheckExplorationLevel(orb)`); estação de mineração = base própria com role de mineração e `Ship.Parent` no orbe | 15 depósitos no save de teste |
| Estação de mineração em obra | já existe como base própria (com role de mineração) desde o início da obra; `Ship.ConstructionLevel` < 1 até ficar pronta. Antes disso o depósito não conta como minerado | visto em 2026-11-26 (Kintaris 10, "building Mining Station here") |

## Sugestões do conselheiro

| O quê | Como | Estado |
|---|---|---|
| Onde ficam | `Empire.EvaluationQueue` (com `IsAdvisorSuggestion`), não em `Empire.Messages`. Toda mensagem passa por essa fila por alguns segundos antes de ir para o log, por isso só as do conselheiro contam | confirmado |
| Na tela | quando o jogo mostra a sugestão no diálogo, ela sai da fila; `UserInterfaceController.EmpireMessageDialogGetActiveMessage()` devolve a que está na tela | confirmado (oferta de contrato) |
| Ação | `GameTask` da mensagem (`Text`: "Build MS-1 (Large Mining Station) at Kintaris 4"); `RelatedObject` vem vazio | confirmado |
| Responder | `ScaledRenderer.OpenMessage(msg, true, false)`, depois o botão do próprio diálogo: `UserInterfaceController.EmpireMessageDialog.YesButton` ("Assign Mission") ou `NoButton` ("Decline"), chamando `PreClickEvent` e `ClickEvent` com `new DWEventArgs(button.ExtraClickData)` (o `ExtraClickData` é a própria `EmpireMessage`) | confirmado (2026-11-26): Accept atribuiu a missão de construção à Shining Lady |
| Alvo da missão de construir | a missão aparece como Build sem alvo na nossa aba Ships (o alvo não é um objeto que resolvemos) | a melhorar |

## Som

- DWGame.DWSettings.MusicVolume (0..1) é o volume da música das opções do jogo; SoundEffectsVolume, UserInterfaceSoundEffectsVolume, NebulaStormSoundEffectsVolume ao lado. DWGame.MusicSystem.Volume existe. A testar (2026-11-26): se mudar MusicVolume em jogo vale na hora (o log do mod mostra o MusicSystem.Volume depois da mudança).
- Mute geral pelas sessões de áudio do processo no Windows (mesmo código do Window Fit); o Windows lembra o mute de um app entre execuções, por isso o mod desfaz o mute ao fechar o jogo.

## Pesquisa

- Abrir a tela de pesquisa num projeto: o atalho do jogo com `UiSection` de pesquisa (mapping 90 no teste) abre a tela; no quadro seguinte, `UserInterfaceController.ResearchScreen` (`ResearchTree`) existe e está visível. `CalculateProjectPosition(project)` dá a posição já descontada do `ScrollOffset` (Armor Plating: 106,2800 com scroll 13,239; 120,3040 com scroll 0); setar `ScrollOffset` rola a árvore e `HoveredProject` destaca. **Confirmado** pelo Leandro em 2026-11-26 (`certinho`). `DisplayRectangle` veio 132x132 (não é o tamanho da tela), então a centralização é aproximada.
- Ids de projeto começam em 0 (Early Beam Weapons tem id 0): 0 é um projeto, não `nenhum`.

## Missões das naves

- `TextHelper.ResolveMissionDescription(galaxy, empire, ship, mission)` dá o texto da missão como no painel do jogo ("Build MS-3 (Large Mining Station) at Kintaris 2", "Mine Kintaris 2, offload at Goma Spaceport", "Survey Osnoa (Alurthas system) (86 secs)"; às vezes sem o espaço antes do tempo). `ShipMission.GetCurrentTarget(galaxy)` é o destino atual.
- Missão `Build`: `PrimaryTarget` é de onde vem o material (Goma) e `SecondaryTarget` é onde a base será construída (Kintaris 2); `BuildDesignId` é o design. **Confirmado** no log em 2026-11-26 (Lone Rebel, Shining Lady). Enquanto a nave vai buscar o material, o estaleiro dela fica vazio: não é estaleiro parado.
- Naves mineradoras mineram sem estação (missão `Mine`/`MineLoad`: "Mine Osnoa (Alurthas system), offload at Goma Spaceport"). O lugar continua em New Mining Locations do jogo, porque essa lista é de lugares sem estação. Em 2026-11-26 a Charming Quest minerava Attarion Fur em Osnoa, e a página dizia "Not mined": agora os depósitos trazem as naves que mineram ali (`ships`, pelo `PrimaryTarget` da missão, que se supõe ser o lugar minerado; o log "Mining ships at work" confere). Que recurso a nave tira: o que ela já carrega daquele lugar; sem carga ainda, qualquer um dos de lá.

## Naves e bases abandonadas

- A aba Exploration → Abandoned Ships and Bases do jogo é `Empire.AbandonedShips` (`PrioritizedTargetList`, na prioridade do jogo: `StellarObject` é a nave, `Available`). `Empire.QueuedAbandonedTargets` são as já ordenadas; `Ship.CanAcquireAbandoned(empire)` diz se dá para tomar.
- Tooltips do jogo (2026-11-26): "Health 59%" (deve ser 1 − `HullDamageLevel`, conferir no log); o raio vermelho é "Our last sighting of this location showed dangerous threats that must first be cleared before civilian ships will travel here" (`Empire.DangerousLocations`, por `LocationId`; o mod testa o `LocationId` da nave e o id do sistema, conferir); o botão à direita é "Repair and Investigate <nave> (Destroyer)", desabilitado com "Construction Ship already repairing (Lucky Beggar)" ou "Ship or base does not need repair".
- **Em aberto:** como se toma uma abandonada com 100% de vida (o botão do jogo fica desabilitado). O log "Abandoned ships" lista as nossas naves cuja missão mira cada uma.

## Marcas nos ícones de recurso dos lugares

- No diálogo de sugestão do advisor (e nos ícones de recurso dos lugares), o pontilhado é a produção, não o estoque: vermelho "PRODUCTION SHORTAGE x/sec" quando `CalculateResourceRateSurplus(id, false)` < 0, amarelo "PRODUCTION OVERSUPPLY x/sec" quando > 0. **Confirmado** em 2026-11-26 no AS693: Mebnar −0,3066 (0.31/sec), Cuprica −0,5074 (vermelho), Steel +4,5334 (4.53/sec, amarelo).
- A abundância no diálogo de sugestão do AS693 era Mebnar 7%, Steel 12%, Cuprica 97%; o nosso `OrbResource.Abundance` dá 5%, 9% e 97%, **igual ao painel do próprio asteroide no jogo** (2026-11-26). Então o nosso número é o do jogo; o diálogo mostra outro valor (talvez com algum bônus de mineração, ou guardado de quando a sugestão foi feita). Não investigado.

## Tabela de suprimento (regra do jogo)

Reconstruída em 2026-11-26 e conferida com o jogo (Fuel 1/0/0, Construction 15/0/1, Luxury 27/8/1):
- Tempo de estoque = estoque / (consumo - produção), em tempo de jogo. Um ano de jogo = 600 s na velocidade 1 (600.000 unidades de data; as taxas são por segundo). Adequate: mais de 1 ano, ou produção cobre o consumo; Low: menos de 1 ano; Critical: menos de 3 meses (tooltip do jogo: `less than 3 months supply`) ou sem estoque.
- Recursos de construção também ficam Critical com `Very low excess supply at spaceports`: estoque abaixo do nível a manter (`CalculateTotalStockLevels`) e produção menor que o consumo (Carbonite, 1.014 contra 1.460, sem produção). O Steel, abaixo do nível mas produzindo mais do que usa, fica Adequate.
- Não servem: comparar só produção e demanda (daria 31 luxos críticos); somar o gasto das obras (`ResourceRatesUnderConstruction`) quebra o Luxury; `DetermineCriticalResourceShortages()` veio vazio.
- O `On Hand` de 488 da tela `Resources & Stock Levels` é de um lugar; o tooltip `Stockpile` da lista dá o total (1.446). `CalculateTotalCargoLevels` inclui a carga nas naves; o mod agora manda também colônias + bases (`onHand`) e naves (`onShips`).
## Publicação
- O DW2 Mod Launcher publica a pasta inteira do mod em `mods\<nome>`, inclusive arquivos que não vieram do build (2026-11-27: o `overview.json` de desenvolvimento foi para a Workshop da 0.1.0 à 0.1.3). Nada de configuração local dentro da pasta do mod.
- A cópia da Workshop fica em `steamapps\workshop\content\1531540\<id>` (a pasta `web` vai junto); o `mods.json` a lista como `steam/<id>`.
