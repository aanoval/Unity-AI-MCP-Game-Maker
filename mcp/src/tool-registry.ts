import { Ajv } from "ajv";
import { assetTools } from "./schemas/asset.js";
import { componentTools } from "./schemas/component.js";
import { gameMakerTools } from "./schemas/game-maker.js";
import { gameObjectTools } from "./schemas/game-object.js";
import { prefabTools } from "./schemas/prefab.js";
import { sceneTools } from "./schemas/scene.js";
import { screenshotTools } from "./schemas/screenshot.js";
import { uiTools } from "./schemas/ui.js";
import { McpAdapterError } from "./errors.js";
import type { JsonObject, ToolDefinition } from "./types.js";

const ajv = new Ajv({ allErrors: true, strict: false });

export const toolDefinitions: ToolDefinition[] = [
  {
    name: "unity_health",
    unityTool: null,
    description: "Check whether the Unity AI Game Maker local server is reachable.",
    inputSchema: { type: "object", additionalProperties: false, properties: {} },
    risk: "read"
  },
  {
    name: "unity_list_tools",
    unityTool: null,
    description: "List raw Unity AI Game Maker RPC tools currently exposed by the Unity package.",
    inputSchema: { type: "object", additionalProperties: false, properties: {} },
    risk: "read"
  },
  ...sceneTools,
  ...gameObjectTools,
  ...componentTools,
  ...assetTools,
  ...prefabTools,
  ...uiTools,
  ...screenshotTools,
  ...gameMakerTools
];

export const toolsByName = new Map(toolDefinitions.map((tool) => [tool.name, tool]));

const validators = new Map(
  toolDefinitions.map((tool) => [tool.name, ajv.compile(tool.inputSchema)])
);

export function getTool(name: string): ToolDefinition {
  const tool = toolsByName.get(name);
  if (!tool) {
    throw new McpAdapterError(`Unknown MCP tool: ${name}`, "UNKNOWN_TOOL");
  }

  return tool;
}

export function validateToolArguments(tool: ToolDefinition, args: unknown): JsonObject {
  const value = normalizeArguments(args);
  const validate = validators.get(tool.name);
  if (!validate) {
    throw new McpAdapterError(`Missing validator for ${tool.name}`, "SCHEMA_MISSING");
  }

  if (!validate(value)) {
    throw new McpAdapterError(
      `Invalid arguments for ${tool.name}: ${ajv.errorsText(validate.errors)}`,
      "SCHEMA_VALIDATION_FAILED",
      validate.errors
    );
  }

  return value;
}

function normalizeArguments(args: unknown): JsonObject {
  if (args == null) {
    return {};
  }

  if (typeof args !== "object" || Array.isArray(args)) {
    throw new McpAdapterError("Tool arguments must be a JSON object.", "SCHEMA_VALIDATION_FAILED");
  }

  return args as JsonObject;
}
