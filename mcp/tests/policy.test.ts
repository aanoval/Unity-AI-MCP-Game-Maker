import assert from "node:assert/strict";
import test from "node:test";
import { assertToolAllowed } from "../src/policy.js";
import type { ResolvedConfig, ToolDefinition } from "../src/types.js";

test("policy allows read and safe write tools by default", () => {
  assert.doesNotThrow(() => assertToolAllowed(tool("read"), config()));
  assert.doesNotThrow(() => assertToolAllowed(tool("write"), config()));
});

test("policy denies elevated risk tools unless explicitly enabled", () => {
  assert.throws(() => assertToolAllowed(tool("destructive"), config()), /requires --allow-dangerous/);
  assert.throws(() => assertToolAllowed(tool("playmode"), config()), /requires --allow-playmode/);
  assert.throws(() => assertToolAllowed(tool("batch"), config()), /requires --allow-batch/);

  assert.doesNotThrow(() => assertToolAllowed(tool("destructive"), config({ allowDangerous: true })));
  assert.doesNotThrow(() => assertToolAllowed(tool("playmode"), config({ allowPlaymode: true })));
  assert.doesNotThrow(() => assertToolAllowed(tool("batch"), config({ allowBatch: true })));
});

function tool(risk: ToolDefinition["risk"]): ToolDefinition {
  return {
    name: `tool_${risk}`,
    unityTool: "raw.tool",
    description: "test",
    inputSchema: {},
    risk
  };
}

function config(overrides: Partial<ResolvedConfig> = {}): ResolvedConfig {
  return {
    projectPath: "/tmp/project",
    configPath: "/tmp/project/UserSettings/UnityAiGameMaker.json",
    baseUrl: "http://127.0.0.1:6421",
    authRequired: true,
    token: "secret",
    timeoutMs: 1000,
    allowDangerous: false,
    allowPlaymode: false,
    allowBatch: false,
    ...overrides
  };
}
