# DW2 Empire Overview

A Distant Worlds 2 mod that shows your empire as a web page you can keep open next to the game, for example on a second monitor. The game has no outside API, so the mod runs a small web server inside the game and serves the page and the data itself. Nothing else needs to be installed: open `http://localhost:4722` in any browser while the game is running.

The idea is a helper, not a second game: each screen answers "what needs me?" or "what is happening?", and anything you click takes you to it in the game.

> **ALPHA.** This is an early version: it works and is played with every day, but expect rough edges, screens that change between versions, and numbers that may not match the game in every case.
>
> A confession: I'm fairly new to Distant Worlds 2. I built this while learning the game (and learned more about it building this than in my first 40 hours of playing), so some things here may not make much sense to a seasoned player: a number read the wrong way, a screen that misses what really matters, an alert nobody needs. If something looks off, or you miss something you check all the time in the game, please [open an issue](https://github.com/xupisco/dw2-overview/issues) or comment on the [Steam Workshop page](https://steamcommunity.com/sharedfiles/filedetails/?id=3846464373). Feedback and suggestions are very welcome; a screenshot of the page next to the game's own screen helps the most.

Get it on the [Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=3846464373). Requires the [DW2 Mod Launcher](https://github.com/lucky-wolf/DW2-Mod-Launcher/releases). Tested with Distant Worlds 2 version 1.3.8.4.

## What it shows

- **Overview:** cash, population, yards, the research in progress and idle ships at a glance; **Needs attention** (advisor suggestions you can accept or decline right there, idle yards, an empty research queue, critical resources and whether anyone is already on them, unhappy colonies, low fuel); the **supply table** as the game shows it; what the yards are building. Blocks can be turned on, resized and dragged into another order.
- **Construction:** everything being built or queued in yards, bases built in place, construction ships on their way to build, bases ordered and still waiting for a ship, and idle yards. A mining base says what its site holds, coloured by how short the empire is of it. Filters by state, kind, work and role.
- **Ships:** every ship and base with its mission in the game's own words (the places in it are links), what it carries, and fuel or energy only when they are low. Grouped by role, mission or system; rows or cards; idle ships, ships short of fuel and damaged ships first.
- **Colonies:** cards with a planet drawn from the game's own colours, population and its growth, development, corruption, tax, approval, the planet's resources, and colonies in trouble first.
- **Resources:** the supply table (Adequate / Low / Critical, matching the game's rule), each resource with its stock, how long it lasts at the current pace, and the next step: where it is mined, a station being built or ordered, the best known deposit, or none known. Mined and new mining locations (in the game's own priority, with who is already going there). Table or cards; details in a modal.
- **Trends:** the mod records the empire every game month, page open or not (`data\Logs\DW2Overview\history`, one JSON Lines file per game; an earlier save drops the later months). Each resource has a 5-year line of its stock; a chart beside the supply table compares up to 8 resources as % of the level to keep, stock, net rate or price; a strip under the supply table shows how many were low or critical each month; a resource's details chart it over the whole game.
- **Research:** the queue and the projects available; a click opens the project in the game's research screen.
- **Messages:** the game's message log; a message opens whole in a modal, with the game's own buttons (Accept, Dismiss, "Take me to…").

## Playing with it

- **Alerts:** new game messages as toasts (or Windows notifications while the page is in the background, if you turn them on), by priority, with mute per message type. **Watch** rules alert you when something changes: the game paused itself, nothing is being researched, a yard ran out of work, a ship became idle, a colony is unhappy, a resource became critical, cashflow turned negative.
- **Command palette (Ctrl+K):** find any ship, base, colony, fleet, resource or anything the game knows, by name, kind, system or mission; `>` runs the game's own commands (open screens, pause…).
- **Game controls:** play / pause (the page's edges glow while the game is paused), mute the game, music volume.
- **From the game:** `Ctrl+Alt+9` opens the page in your default browser (the game says so the first time a save is loaded).
- **Keys on the page:** `Space` play / pause, `Q` the message log, `/` the search, `Esc` clears the filters.
- **Narrow windows:** a menu button, tables as cards (the server answers on this computer only, so a phone cannot reach it yet).

## Privacy and performance

- **Nothing leaves your computer.** The server answers only on this machine (`localhost`), so other machines cannot reach it, and the page loads nothing from the internet (fonts and scripts are in the mod). No telemetry, no accounts.
- **Other websites can't use it:** they cannot read its answers, and commands need a per-session token (details under [How it works](#how-it-works)).
- **Light on the game:** it reads the game at most once a second, and only while a page is open; with no page open it only records one small point a game month for the trends. Early in a game a read takes a few milliseconds (each list's time is in the log). Big late games are not measured yet: if you notice a slowdown, please [say so](https://github.com/xupisco/dw2-overview/issues).

## Install (development)

Requirements: .NET SDK (8 or newer) and the game installed.

```powershell
.\install.ps1 -LiveWeb
```

The script builds the mod, copies the DLL, `mod.json` and the `web\` folder to `<game>\mods\DW2Overview\`, and adds `mods/DW2Overview` to `mods.json` and to the launcher's `mods.<profile>.json` (keeping a backup copy).

Options:
- `-LiveWeb`: the installed mod serves the page straight from this repository's `web\` folder, so a page change only needs F5 in the browser, not a game restart. Without it, the installed page is served, as players get it.
- `-NoRegister`: copy only, without touching the mod lists.
- `-GameDir "..."`: another game folder.

While the game is running its DLL is locked: close the game to install a new DLL.

## Settings

In the DW2 Mod Launcher, the mod's settings have **Web page port** (4722 by default; a whole number from 1024 to 65535, used the next time the game starts).

For development, an optional `overview.json` in `<game>\data\Logs\DW2Overview\` (next to the log, outside the mod folder, so it is never published with the mod); the keys it has win over the launcher's:

```json
{ "port": 4722, "webRoot": "D:\\path\\to\\web" }
```

A `webRoot` without `index.html` is ignored (logged), and the mod's own `web\` folder is served.

## How it works

| Part | What it does |
|---|---|
| `src/Snapshot.cs` | A postfix on `DWGame.Update` reads the game's data on the game thread, at most once a second and only while a page has asked for it recently, and keeps it as ready-made JSON |
| `src/History.cs` | One point a game month (cash, population, and each resource's stock, rates, level to keep and price), recorded whether or not a page is open, in a JSON Lines file per game; an earlier save drops the later points |
| `src/Views.cs`, `src/Resources.cs` | What each list holds: colonies, research, messages, ships, the index for the palette, resources and deposits |
| `src/Server.cs` | `HttpListener` on `http://localhost:<port>/` only; serves `web\` and the JSON, never touches game objects |
| `src/Commands.cs` | Commands from the page are queued and run on the game thread in the next frame |
| `src/Audio.cs` | Mute (the game's audio sessions in the Windows mixer) and the music volume (the game's own setting) |
| `web/index.html` | The page (plain HTML and JavaScript, no build step) |

The page is one client of a small JSON API, so other front ends could use it too. Version in `/api/info`.

| Read (`GET`) | |
|---|---|
| `/api/info` | mod and API version, empire, date, economy, counts, game paused / speed, sound, session token |
| `/api/construction`, `/api/ships`, `/api/colonies`, `/api/research`, `/api/messages`, `/api/resources` | the lists behind each tab |
| `/api/index`, `/api/search?q=` | everything the palette can find; the game's own "search known items" |
| `/api/commands` | the game's key mappings, as commands |
| `/api/history?since=&epoch=` | the empire over time, one point a game month (cash, cashflow, population, colonies, and per resource stock, rates, level to keep, price); with `since` and the `epoch` it last got, only the newer points |

| Commands (`POST`, header `X-Overview-Token`) | |
|---|---|
| `/api/goto` | `{"type": "ship" \| "orb" \| "fleet", "id": 123}`: select and center it in the game |
| `/api/key` | `{"id": 7}`: run one of the game's key mappings |
| `/api/suggestion` | `{"key": "...", "accept": true}` or `{"key": "...", "button": "yes" \| "no" \| "other" \| "show"}`: answer an advisor suggestion or press a button of the game's message dialog |
| `/api/research` | `{"id": 266}`: open a project in the research screen |
| `/api/audio` | `{"mute": true}` or `{"music": 0.5}` |

Security: the server listens on localhost only, so other machines cannot reach it. Other websites open in your browser can send requests to localhost, but they cannot read the answers (no CORS headers), requests carrying another site's `Origin` are refused, and commands also need the per-session token, which forces a CORS preflight that the server never grants.

Development notes, in Portuguese: [PLAN.md](PLAN.md) (plan and decisions) and [FINDINGS.md](FINDINGS.md) (how the game works inside, as found while building this: the supply rule, missions, stock, and more).

## Credits

Made by Leandro with the help of Claude, Anthropic's AI assistant, which wrote much of the code and the documentation.

## License

MIT, see [LICENSE](LICENSE).
