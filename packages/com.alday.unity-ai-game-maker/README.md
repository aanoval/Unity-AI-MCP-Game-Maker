# Unity AI MCP Game Maker Package

Editor-only Unity package for local AI automation.

## Editor Menu

```text
Tools > Unity AI Game Maker > Start Local Server
Tools > Unity AI Game Maker > Stop Local Server
Tools > Unity AI Game Maker > Print Token
Tools > Unity AI Game Maker > Open Config
Tools > Unity AI Game Maker > Run Batch File...
Tools > Unity AI Game Maker > Capture Menu Screenshots (Editor Camera)
Tools > Unity AI Game Maker > Capture Menu Screenshots (Play Mode)
Tools > Unity AI Game Maker > Run Play Mode Screenshot Batch
```

## Config

Generated config path:

```text
UserSettings/UnityAiGameMaker.json
```

Defaults in `0.4.0+`:

- `autoStart: true` — local HTTP server starts when Unity Editor loads
- `bindHost: 127.0.0.1`
- `port: 6421`
- `authRequired: true`

Disable auto-start by setting `"autoStart": false` in the config file.

## HTTP RPC

| Endpoint | Method | Auth | Purpose |
|----------|--------|------|---------|
| `/health` | GET | No | Server status |
| `/tools` | GET | Yes | List tools |
| `/rpc` | POST | Yes | Run a tool |

Example:

```bash
curl -H "Authorization: Bearer <token>" \
  -H "Content-Type: application/json" \
  -d '{"tool":"scene.listOpen","args":{}}' \
  http://127.0.0.1:6421/rpc
```

## Batch Mode (no running server required)

Run a JSON batch file from terminal:

```bash
UNITY_AI_GAME_MAKER_BATCH_FILE=/tmp/commands.json \
UNITY_AI_GAME_MAKER_BATCH_OUT=/tmp/result.json \
/Applications/Unity/Hub/Editor/6000.5.1f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -quit \
  -projectPath /path/to/UnityProject \
  -executeMethod Alday.UnityAiGameMaker.Editor.UnityAiGameMakerBatch.RunFromEnvironment
```

Or use the repo CLI:

```bash
node cli/unity-ai.js /path/to/UnityProject unity-batch ./examples/batch.menu-screenshots.json
```

Important: do **not** use `-nographics` for screenshot tools. Unity needs a graphics device for `screenshot.capture` and `screenshots.captureScenes`.

## Screenshot Tools

Choose the capture mode that fits your goal:

| Mode | `source` / entry point | Best for | Speed | Quality |
|------|------------------------|----------|-------|---------|
| Editor camera | `camera` (default) | Menu/UI scenes, sharp PNGs | Fast | High |
| Play game | `playMode` or `game` | Runtime bootstrap, gameplay systems | Slower | Game View based |
| Game View (editor open) | `gameView` | Manual review while Unity is open | Medium | Depends on Game View size |

### `screenshot.capture`

Capture the active scene to PNG.

```json
{
  "tool": "screenshot.capture",
  "args": {
    "outputPath": "/absolute/path/Main_Menu.png",
    "width": 1080,
    "height": 1920,
    "source": "camera",
    "cameraPath": "Camera"
  }
}
```

Args:

- `outputPath` — destination PNG (preferred)
- `path` — legacy alias for `outputPath`
- `width`, `height` — output size
- `source` — `camera` (default), `playMode` / `game`, or `gameView`
- `cameraPath` — hierarchy path to a Camera GameObject

`source: "camera"` renders directly at the requested resolution. This is the recommended default for menu/UI screenshots.

`source: "playMode"` requires an active Play Mode session. For batch automation, use the play mode batch runner below.

`source: "gameView"` reads the Unity Editor Game View render texture. It is best for interactive editor review while Unity is open.

### `screenshots.captureScenes`

Open multiple scenes and capture each one using the **editor camera** path.

```json
{
  "tool": "screenshots.captureScenes",
  "args": {
    "filter": "menu",
    "outputDir": "/absolute/path/menu-screenshots",
    "width": 1080,
    "height": 1920,
    "source": "camera"
  }
}
```

Filters:

- `menu` — scenes named `Menu_*`, `Main_Menu`, or `MainMenu`
- `menusandgameplay` — menu scenes plus scenes containing `Gameplay`
- `buildSettings` — all enabled scenes in Build Settings
- `gameplay` — scenes containing `Gameplay`
- `all` — same as `buildSettings`

Editor camera CLI:

```bash
node cli/unity-ai.js /path/to/UnityProject capture-scenes \
  --output ../menu-screenshots \
  --filter menu \
  --source camera
```

Play mode CLI:

```bash
node cli/unity-ai.js /path/to/UnityProject capture-scenes \
  --output ../menu-screenshots \
  --filter menusandgameplay \
  --source playMode \
  --wait-frames 30
```

Play mode batch entry point:

```bash
UNITY_AI_GAME_MAKER_BATCH_FILE=/tmp/playmode.json \
UNITY_AI_GAME_MAKER_BATCH_OUT=/tmp/playmode.out.json \
/Applications/Unity/Hub/Editor/6000.5.1f1/Unity.app/Contents/MacOS/Unity \
  -batchmode \
  -projectPath /path/to/UnityProject \
  -executeMethod Alday.UnityAiGameMaker.Editor.UnityAiScreenshotPlayModeBatch.RunFromEnvironment
```

Notes:

- Do **not** pass `source: playMode` to the regular `UnityAiGameMakerBatch` runner.
- Play mode batch keeps Unity open until all scenes finish, then calls `EditorApplication.Exit(0)`.
- Do **not** use `-nographics` or `-quit` with the play mode batch runner.

Batch macros:

```json
{
  "commands": [
    {
      "tool": "screenshots.captureMenus",
      "args": {
        "output": "../menu-screenshots",
        "source": "camera"
      }
    }
  ]
}
```

```json
{
  "commands": [
    {
      "tool": "screenshots.captureMenusPlayMode",
      "args": {
        "output": "../menu-screenshots",
        "waitFrames": 30
      }
    }
  ]
}
```
