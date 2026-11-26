# DW2 Empire Overview

A Distant Worlds 2 mod that shows your empire as a web page you can keep open next to the game, for example on a second monitor. The game has no outside API, so the mod runs a small web server inside the game and serves the page and the data itself. Nothing else needs to be installed: open `http://localhost:4722` in any browser while the game is running.

Early stage. What works now:

- **Construction:** everything your yards are building, what is waiting in their queues, facilities under construction on colonies, and idle yards. Filters, text search, sortable columns, live progress.
- **Go to:** click a row and the game selects and centers that yard or colony; click a ship's name to go to the ship.

Next: colonies and research tables, then a command palette (Ctrl+K) to jump to anything. Development notes, in Portuguese: [PLAN.md](PLAN.md) and [FINDINGS.md](FINDINGS.md).

## Install (development)

Requirements: .NET SDK (8 or newer) and the game installed.

```powershell
.\install.ps1 -LiveWeb
```

The script builds the mod, copies the DLL, `mod.json` and the `web\` folder to `<game>\mods\DW2Overview\`, and adds `mods/DW2Overview` to `mods.json` and to the launcher's `mods.<profile>.json` (keeping a backup copy).

Options:
- `-LiveWeb`: the installed mod serves the page straight from this repository's `web\` folder, so a page change only needs F5 in the browser, not a game restart.
- `-NoRegister`: copy only, without touching the mod lists.
- `-GameDir "..."`: another game folder.

While the game is running its DLL is locked: close the game to install a new DLL.

## Settings

Optional `overview.json` in the mod's folder:

```json
{ "port": 4722, "webRoot": "D:\\path\\to\\web" }
```

## How it works

| Part | What it does |
|---|---|
| `src/Snapshot.cs` | A postfix on `DWGame.Update` reads the game's data on the game thread, at most once a second and only while a page has asked for it in the last 10 seconds, and keeps it as ready-made JSON |
| `src/Server.cs` | `HttpListener` on `http://localhost:<port>/` only; serves `web\` and the JSON, never touches game objects |
| `src/Commands.cs` | Commands from the page are queued and run on the game thread in the next frame |
| `web/index.html` | The page |

API (version in `/api/info`):

| Endpoint | |
|---|---|
| `GET /api/info` | mod and API version, empire, date, session token |
| `GET /api/construction` | construction rows |
| `POST /api/goto` | `{"type": "ship" \| "orb", "id": 123}`, header `X-Overview-Token` |

Security: the server listens on localhost only, so other machines cannot reach it. Other websites open in your browser can send requests to localhost, but they cannot read the answers (no CORS headers), requests carrying another site's `Origin` are refused, and commands also need the per-session token, which forces a CORS preflight that the server never grants.

## Credits

Made by Leandro with the help of Claude, Anthropic's AI assistant, which wrote much of the code and the documentation.

## License

MIT, see [LICENSE](LICENSE).
