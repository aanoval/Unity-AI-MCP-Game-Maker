import type { JsonObject } from "../types.js";

export const emptySchema: JsonObject = {
  type: "object",
  additionalProperties: false,
  properties: {}
};

export const vector3Schema: JsonObject = {
  type: "array",
  minItems: 3,
  maxItems: 3,
  items: { type: "number" }
};

export const vector2Schema: JsonObject = {
  type: "array",
  minItems: 2,
  maxItems: 2,
  items: { type: "number" }
};

export const colorSchema: JsonObject = {
  type: "array",
  minItems: 4,
  maxItems: 4,
  items: { type: "number", minimum: 0, maximum: 1 }
};

export function objectSchema(properties: JsonObject, required: string[] = [], additionalProperties = false): JsonObject {
  return {
    type: "object",
    additionalProperties,
    properties,
    ...(required.length > 0 ? { required } : {})
  };
}

export const targetSchemaProperties: JsonObject = {
  path: { type: "string", minLength: 1 },
  name: { type: "string", minLength: 1 }
};

export const rectTransformProperties: JsonObject = {
  anchoredPosition: vector2Schema,
  sizeDelta: vector2Schema,
  anchorMin: vector2Schema,
  anchorMax: vector2Schema,
  pivot: vector2Schema
};
