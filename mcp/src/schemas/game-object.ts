import { objectSchema, targetSchemaProperties, vector3Schema } from "./common.js";
import type { ToolDefinition } from "../types.js";

export const gameObjectTools: ToolDefinition[] = [
  {
    name: "unity_game_object_find",
    unityTool: "gameObject.find",
    description: "Find scene GameObjects by name or hierarchy path.",
    inputSchema: objectSchema({
      name: { type: "string" },
      path: { type: "string" },
      includeInactive: { type: "boolean" },
      limit: { type: "integer", minimum: 1, maximum: 500 }
    }),
    risk: "read"
  },
  {
    name: "unity_game_object_create",
    unityTool: "gameObject.create",
    description: "Create a GameObject, optionally parented under an existing hierarchy path.",
    inputSchema: objectSchema({
      name: { type: "string", minLength: 1 },
      parentPath: { type: "string", minLength: 1 }
    }),
    risk: "write"
  },
  {
    name: "unity_game_object_delete",
    unityTool: "gameObject.delete",
    description: "Delete a scene GameObject by path or exact name.",
    inputSchema: objectSchema(targetSchemaProperties),
    risk: "destructive"
  },
  {
    name: "unity_game_object_set_transform",
    unityTool: "gameObject.setTransform",
    description: "Set local position, rotation, or scale for a scene GameObject.",
    inputSchema: objectSchema({
      ...targetSchemaProperties,
      position: vector3Schema,
      rotation: vector3Schema,
      scale: vector3Schema
    }),
    risk: "write"
  },
  {
    name: "unity_game_object_set_active",
    unityTool: "gameObject.setActive",
    description: "Set active state for a scene GameObject.",
    inputSchema: objectSchema({
      ...targetSchemaProperties,
      active: { type: "boolean" }
    }, ["active"]),
    risk: "write"
  },
  {
    name: "unity_game_object_set_parent",
    unityTool: "gameObject.setParent",
    description: "Move a scene GameObject under another GameObject.",
    inputSchema: objectSchema({
      ...targetSchemaProperties,
      parentPath: { type: "string", minLength: 1 },
      worldPositionStays: { type: "boolean" }
    }, ["parentPath"]),
    risk: "write"
  }
];
