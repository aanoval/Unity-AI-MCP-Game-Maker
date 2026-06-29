import { colorSchema, objectSchema } from "./common.js";
import type { ToolDefinition } from "../types.js";

export const assetTools: ToolDefinition[] = [
  {
    name: "unity_asset_find",
    unityTool: "asset.find",
    description: "Find Unity assets using AssetDatabase filters.",
    inputSchema: objectSchema({
      filter: { type: "string" },
      folders: {
        type: "array",
        items: { type: "string", minLength: 1 }
      },
      limit: { type: "integer", minimum: 1, maximum: 500 }
    }),
    risk: "read"
  },
  {
    name: "unity_asset_material_create",
    unityTool: "asset.material.create",
    description: "Create a Unity material asset with optional color.",
    inputSchema: objectSchema({
      path: { type: "string", minLength: 1 },
      name: { type: "string", minLength: 1 },
      color: colorSchema,
      shader: { type: "string", minLength: 1 }
    }),
    risk: "write"
  }
];
