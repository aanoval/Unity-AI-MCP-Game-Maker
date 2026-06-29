# Server Layer

The first implementation runs the local HTTP listener inside the Unity Editor package.

The production MCP adapter now lives in:

```text
mcp/
```

The adapter translates MCP tool calls into the local command protocol instead of duplicating Unity logic.

Planned modes:

- `stdio` for local AI clients [implemented in `mcp/`]
- authenticated local HTTP for clients that need it

Non-goals:

- unauthenticated HTTP
- cloud relay by default
- public network binding
