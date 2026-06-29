# Server Layer

The first implementation runs the local HTTP listener inside the Unity Editor package.

This folder is reserved for the future MCP adapter. The adapter should translate MCP tool calls into the local command protocol instead of duplicating Unity logic.

Planned modes:

- `stdio` for local AI clients
- authenticated local HTTP for clients that need it

Non-goals:

- unauthenticated HTTP
- cloud relay by default
- public network binding
