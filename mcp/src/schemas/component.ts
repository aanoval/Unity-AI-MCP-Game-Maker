import { objectSchema, targetSchemaProperties } from "./common.js";
import type { ToolDefinition } from "../types.js";

const valueSchema = {};

export const componentTools: ToolDefinition[] = [
  {
    name: "unity_component_list",
    unityTool: "component.list",
    description: "List components on a scene GameObject.",
    inputSchema: objectSchema(targetSchemaProperties),
    risk: "read"
  },
  {
    name: "unity_component_add",
    unityTool: "component.add",
    description: "Add a component type to a scene GameObject.",
    inputSchema: objectSchema({
      ...targetSchemaProperties,
      type: { type: "string", minLength: 1 },
      reuseExisting: { type: "boolean" }
    }, ["type"]),
    risk: "write"
  },
  {
    name: "unity_component_set_property",
    unityTool: "component.setProperty",
    description: "Set a public property or field on a component using structured JSON conversion.",
    inputSchema: objectSchema({
      ...targetSchemaProperties,
      type: { type: "string", minLength: 1 },
      property: { type: "string", minLength: 1 },
      value: valueSchema
    }, ["type", "property", "value"]),
    risk: "write"
  },
  {
    name: "unity_component_set_field",
    unityTool: "component.setField",
    description: "Set a component field on a scene GameObject.",
    inputSchema: objectSchema({
      ...targetSchemaProperties,
      type: { type: "string", minLength: 1 },
      field: { type: "string", minLength: 1 },
      value: valueSchema
    }, ["type", "field", "value"]),
    risk: "write"
  }
];
