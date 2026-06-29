import { emptySchema, objectSchema } from "./common.js";
import type { ToolDefinition } from "../types.js";

export const sceneTools: ToolDefinition[] = [
  {
    name: "unity_scene_list_open",
    unityTool: "scene.listOpen",
    description: "List scenes currently open in the Unity Editor.",
    inputSchema: emptySchema,
    risk: "read"
  },
  {
    name: "unity_scene_open",
    unityTool: "scene.open",
    description: "Open a Unity scene asset by path.",
    inputSchema: objectSchema({
      path: { type: "string", minLength: 1 },
      mode: { type: "string", enum: ["Single", "Additive"] }
    }, ["path"]),
    risk: "write"
  },
  {
    name: "unity_scene_create",
    unityTool: "scene.create",
    description: "Create a new Unity scene and optionally save it to a path.",
    inputSchema: objectSchema({
      path: { type: "string", minLength: 1 },
      setup: { type: "string" }
    }),
    risk: "write"
  },
  {
    name: "unity_scene_save",
    unityTool: "scene.save",
    description: "Save the active scene, or a loaded scene by path.",
    inputSchema: objectSchema({
      path: { type: "string", minLength: 1 }
    }),
    risk: "write"
  },
  {
    name: "unity_scene_save_as",
    unityTool: "scene.saveAs",
    description: "Save the active scene, or a loaded source scene, to a scene asset path.",
    inputSchema: objectSchema({
      path: { type: "string", minLength: 1 },
      sourcePath: { type: "string", minLength: 1 }
    }, ["path"]),
    risk: "write"
  },
  {
    name: "unity_scene_hierarchy",
    unityTool: "scene.hierarchy",
    description: "Inspect scene hierarchy with components and transform metadata.",
    inputSchema: objectSchema({
      scene: { type: "string", minLength: 1 },
      includeInactive: { type: "boolean" },
      maxDepth: { type: "integer", minimum: 0, maximum: 20 }
    }),
    risk: "read"
  },
  {
    name: "unity_scene_build_settings_set",
    unityTool: "scene.buildSettings.set",
    description: "Replace Unity Build Settings scenes with the provided scene paths.",
    inputSchema: objectSchema({
      scenes: {
        type: "array",
        minItems: 1,
        items: { type: "string", minLength: 1 }
      }
    }, ["scenes"]),
    risk: "write"
  }
];
