import { objectSchema, targetSchemaProperties, vector3Schema } from "./common.js";
import type { ToolDefinition } from "../types.js";

export const prefabTools: ToolDefinition[] = [
  {
    name: "unity_prefab_instantiate",
    unityTool: "prefab.instantiate",
    description: "Instantiate a prefab asset into the active scene.",
    inputSchema: objectSchema({
      prefabPath: { type: "string", minLength: 1 },
      name: { type: "string", minLength: 1 },
      parentPath: { type: "string", minLength: 1 },
      position: vector3Schema,
      rotation: vector3Schema,
      scale: vector3Schema
    }, ["prefabPath"]),
    risk: "write"
  },
  {
    name: "unity_prefab_create_from_game_object",
    unityTool: "prefab.createFromGameObject",
    description: "Create a prefab asset from a scene GameObject.",
    inputSchema: objectSchema({
      ...targetSchemaProperties,
      prefabPath: { type: "string", minLength: 1 }
    }, ["prefabPath"]),
    risk: "write"
  },
  {
    name: "unity_prefab_child_create",
    unityTool: "prefab.child.create",
    description: "Create a child GameObject inside a prefab asset.",
    inputSchema: objectSchema({
      prefabPath: { type: "string", minLength: 1 },
      parentPath: { type: "string", minLength: 1 },
      name: { type: "string", minLength: 1 },
      position: vector3Schema,
      rotation: vector3Schema,
      scale: vector3Schema
    }, ["prefabPath", "name"]),
    risk: "write"
  },
  {
    name: "unity_prefab_child_delete",
    unityTool: "prefab.child.delete",
    description: "Delete a child GameObject from a prefab asset.",
    inputSchema: objectSchema({
      prefabPath: { type: "string", minLength: 1 },
      childPath: { type: "string", minLength: 1 }
    }, ["prefabPath", "childPath"]),
    risk: "destructive"
  },
  {
    name: "unity_prefab_component_add",
    unityTool: "prefab.component.add",
    description: "Add a component to a GameObject inside a prefab asset.",
    inputSchema: objectSchema({
      prefabPath: { type: "string", minLength: 1 },
      childPath: { type: "string", minLength: 1 },
      type: { type: "string", minLength: 1 },
      reuseExisting: { type: "boolean" }
    }, ["prefabPath", "type"]),
    risk: "write"
  },
  {
    name: "unity_prefab_component_set_property",
    unityTool: "prefab.component.setProperty",
    description: "Set a component property inside a prefab asset.",
    inputSchema: objectSchema({
      prefabPath: { type: "string", minLength: 1 },
      childPath: { type: "string", minLength: 1 },
      type: { type: "string", minLength: 1 },
      property: { type: "string", minLength: 1 },
      value: {}
    }, ["prefabPath", "type", "property", "value"]),
    risk: "write"
  }
];
