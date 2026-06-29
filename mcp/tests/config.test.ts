import assert from "node:assert/strict";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import test from "node:test";
import { parseCliOptions, resolveConfig } from "../src/config.js";
import { McpAdapterError } from "../src/errors.js";

test("resolveConfig reads Unity project config and builds loopback endpoint", () => {
  const projectPath = createUnityProject({
    bindHost: "127.0.0.1",
    port: 6543,
    authRequired: true,
    token: "secret"
  });

  const config = resolveConfig({ projectPath }, "/tmp");
  assert.equal(config.projectPath, projectPath);
  assert.equal(config.baseUrl, "http://127.0.0.1:6543");
  assert.equal(config.token, "secret");
  assert.equal(config.authRequired, true);
});

test("resolveConfig rejects non-loopback Unity endpoints", () => {
  const projectPath = createUnityProject({
    bindHost: "0.0.0.0",
    port: 6421,
    token: "secret"
  });

  assert.throws(
    () => resolveConfig({ projectPath }),
    (error) => error instanceof McpAdapterError && error.code === "UNSAFE_HOST"
  );
});

test("parseCliOptions handles project and policy flags", () => {
  const options = parseCliOptions([
    "--project",
    "/tmp/unity",
    "--timeout-ms",
    "5000",
    "--allow-playmode",
    "--allow-batch",
    "--allow-dangerous"
  ]);

  assert.deepEqual(options, {
    projectPath: "/tmp/unity",
    timeoutMs: 5000,
    allowPlaymode: true,
    allowBatch: true,
    allowDangerous: true
  });
});

function createUnityProject(config: Record<string, unknown>): string {
  const projectPath = fs.mkdtempSync(path.join(os.tmpdir(), "unity-ai-mcp-config-"));
  fs.mkdirSync(path.join(projectPath, "Packages"), { recursive: true });
  fs.mkdirSync(path.join(projectPath, "UserSettings"), { recursive: true });
  fs.writeFileSync(path.join(projectPath, "Packages", "manifest.json"), "{}\n");
  fs.writeFileSync(
    path.join(projectPath, "UserSettings", "UnityAiGameMaker.json"),
    `${JSON.stringify(config)}\n`
  );
  return projectPath;
}
