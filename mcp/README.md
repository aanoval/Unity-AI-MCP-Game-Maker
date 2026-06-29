# Unity AI Game Maker MCP Server

MCP stdio adapter for Unity AI MCP Game Maker.

This package keeps Unity-specific logic inside the Unity Editor package and exposes the existing local JSON RPC protocol through standard MCP tools.

```text
AI client
  -> MCP stdio server
    -> Unity local HTTP RPC
      -> Unity Editor package
```

## Install

From the repository root:

```bash
npm install --prefix mcp
npm --prefix mcp run build
```

Install the Unity package into your Unity project and open Unity so `UserSettings/UnityAiGameMaker.json` exists:

```bash
node cli/unity-ai.js /path/to/UnityProject install
```

## Run

```bash
node mcp/dist/src/server.js --project /path/to/UnityProject
```

The server uses stdio. Logs are written to stderr so stdout remains reserved for MCP protocol messages.

## Client Config

Example MCP client config:

```json
{
  "mcpServers": {
    "unity-ai-game-maker": {
      "command": "node",
      "args": [
        "/path/to/Unity-AI-MCP-Game-Maker/mcp/dist/src/server.js",
        "--project",
        "/path/to/UnityProject"
      ]
    }
  }
}
```

## Options

```text
--project <path>       Unity project path. Can also use UNITY_AI_PROJECT.
--timeout-ms <ms>      Unity HTTP timeout. Defaults to 30000.
--allow-playmode      Allow tools that enter or exit Play Mode.
--allow-batch         Allow batch-style tools such as multi-scene screenshots.
--allow-dangerous     Allow destructive tools such as GameObject or prefab child deletion.
```

Environment alternatives:

```text
UNITY_AI_PROJECT=/path/to/UnityProject
UNITY_AI_TIMEOUT_MS=30000
UNITY_AI_ALLOW_PLAYMODE=1
UNITY_AI_ALLOW_BATCH=1
UNITY_AI_ALLOW_DANGEROUS=1
```

## Tools

The adapter exposes stable MCP tool names and maps them to Unity RPC names.

| MCP tool | Unity RPC tool |
|----------|----------------|
| `unity_health` | `/health` |
| `unity_list_tools` | `/tools` |
| `unity_scene_list_open` | `scene.listOpen` |
| `unity_game_object_find` | `gameObject.find` |
| `unity_component_set_property` | `component.setProperty` |
| `unity_ui_button_create` | `ui.button.create` |
| `unity_screenshot_capture` | `screenshot.capture` |
| `unity_sample_runner3d_create_content` | `sample.runner3D.createContent` |

Each public tool has a JSON Schema input contract. The adapter validates arguments before forwarding calls to Unity.

## Policy

Default policy:

- read tools allowed
- safe structured write tools allowed
- Play Mode tools denied unless explicitly enabled
- batch tools denied unless explicitly enabled
- destructive tools denied unless explicitly enabled

This is MCP-side defense in depth. Unity package security defaults still apply: loopback-only HTTP, bearer token auth, no cloud mode, and no arbitrary C# execution.

## Development

```bash
npm --prefix mcp run check
npm --prefix mcp test
```

Tests use mocked HTTP and do not require Unity.
