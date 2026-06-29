import assert from "node:assert/strict";
import test from "node:test";
import { UnityAiClient } from "../src/unity-client.js";
import type { ResolvedConfig } from "../src/types.js";

test("UnityAiClient sends bearer token and unwraps RPC result", async () => {
  const calls: Request[] = [];
  const originalFetch = globalThis.fetch;
  globalThis.fetch = (async (input: RequestInfo | URL, init?: RequestInit) => {
    calls.push(new Request(input, init));
    return new Response(JSON.stringify({ ok: true, result: { saved: true } }), {
      status: 200,
      headers: { "Content-Type": "application/json" }
    });
  }) as typeof fetch;

  try {
    const client = new UnityAiClient(config());
    const result = await client.call("scene.save", {});
    assert.deepEqual(result, { saved: true });
    assert.equal(calls[0].url, "http://127.0.0.1:6421/rpc");
    assert.equal(calls[0].headers.get("Authorization"), "Bearer secret");
    assert.equal(await calls[0].text(), JSON.stringify({ tool: "scene.save", args: {} }));
  } finally {
    globalThis.fetch = originalFetch;
  }
});

test("UnityAiClient reports unauthorized Unity responses", async () => {
  const originalFetch = globalThis.fetch;
  globalThis.fetch = (async () => new Response(JSON.stringify({ ok: false }), { status: 401 })) as typeof fetch;

  try {
    const client = new UnityAiClient(config());
    await assert.rejects(() => client.tools(), /HTTP 401/);
  } finally {
    globalThis.fetch = originalFetch;
  }
});

function config(): ResolvedConfig {
  return {
    projectPath: "/tmp/project",
    configPath: "/tmp/project/UserSettings/UnityAiGameMaker.json",
    baseUrl: "http://127.0.0.1:6421",
    authRequired: true,
    token: "secret",
    timeoutMs: 1000,
    allowDangerous: false,
    allowPlaymode: false,
    allowBatch: false
  };
}
