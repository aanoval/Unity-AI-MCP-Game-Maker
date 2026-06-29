# Roadmap

## Phase 1: Safe Local Core

- Unity Editor package
- local HTTP RPC
- bearer token auth
- scene and object inspection
- basic transform editing
- asset search
- dependency-free CLI

## Phase 2: Production Editor Coverage

- prefab tools
- UI Canvas hierarchy tools
- TextMeshPro and Unity UI helpers
- screenshot capture
- console log capture
- playmode enter/exit
- test runner integration

## Phase 3: Policy Engine

- per-tool allowlist
- destructive tool confirmation policy
- audit log file
- dry-run mode for dangerous changes
- project-local policy file

## Phase 4: MCP Adapter

- expose tools through MCP
- preserve local token auth
- support stdio first
- HTTP transport only with explicit auth

## Phase 5: Release Hardening

- signed release artifacts
- checksum verification
- package validation workflow
- CI tests with Unity batchmode
