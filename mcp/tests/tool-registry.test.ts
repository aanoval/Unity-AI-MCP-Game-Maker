import assert from "node:assert/strict";
import test from "node:test";
import { getTool, toolDefinitions, validateToolArguments } from "../src/tool-registry.js";

test("tool registry exposes stable MCP names with Unity mappings", () => {
  assert.ok(toolDefinitions.length >= 40);
  assert.equal(getTool("unity_scene_list_open").unityTool, "scene.listOpen");
  assert.equal(getTool("unity_scene_save_as").unityTool, "scene.saveAs");
  assert.equal(getTool("unity_ui_button_create").unityTool, "ui.button.create");
  assert.equal(getTool("unity_health").unityTool, null);
});

test("tool registry validates arguments with JSON Schema", () => {
  const tool = getTool("unity_game_object_set_transform");
  assert.deepEqual(validateToolArguments(tool, {
    path: "Player",
    position: [0, 1, 2]
  }), {
    path: "Player",
    position: [0, 1, 2]
  });

  assert.throws(
    () => validateToolArguments(tool, { path: "Player", position: [0, 1] }),
    /Invalid arguments/
  );
});

test("tool registry rejects unknown tools", () => {
  assert.throws(() => getTool("unity_nope"), /Unknown MCP tool/);
});
