import { colorSchema, objectSchema, rectTransformProperties, targetSchemaProperties, vector2Schema } from "./common.js";
import type { ToolDefinition } from "../types.js";

const styleSchema = { type: "string", enum: ["arcade", "casual", "dark", "premium", "soccer_mobile"] };

export const uiTools: ToolDefinition[] = [
  {
    name: "unity_ui_canvas_create",
    unityTool: "ui.canvas.create",
    description: "Create a Unity UI Canvas with EventSystem support.",
    inputSchema: objectSchema({
      name: { type: "string", minLength: 1 },
      renderMode: { type: "string" }
    }),
    risk: "write"
  },
  {
    name: "unity_ui_text_create",
    unityTool: "ui.text.create",
    description: "Create a styled Unity UI Text element.",
    inputSchema: objectSchema({
      parentPath: { type: "string", minLength: 1 },
      name: { type: "string", minLength: 1 },
      text: { type: "string" },
      style: styleSchema,
      role: { type: "string" },
      color: colorSchema,
      ...rectTransformProperties
    }, ["parentPath"]),
    risk: "write"
  },
  {
    name: "unity_ui_button_create",
    unityTool: "ui.button.create",
    description: "Create a styled mobile-friendly Unity UI Button.",
    inputSchema: objectSchema({
      parentPath: { type: "string", minLength: 1 },
      name: { type: "string", minLength: 1 },
      text: { type: "string" },
      style: styleSchema,
      variant: { type: "string", enum: ["primary", "secondary", "danger"] },
      ...rectTransformProperties
    }, ["parentPath"]),
    risk: "write"
  },
  {
    name: "unity_ui_menu_create",
    unityTool: "ui.menu.create",
    description: "Create a production-style game menu layout.",
    inputSchema: objectSchema({
      parentPath: { type: "string", minLength: 1 },
      style: styleSchema,
      title: { type: "string" },
      subtitle: { type: "string" },
      buttons: {
        type: "array",
        items: { type: "string" }
      }
    }, ["parentPath"]),
    risk: "write"
  },
  {
    name: "unity_ui_hud_create",
    unityTool: "ui.hud.create",
    description: "Create a production-style game HUD layout.",
    inputSchema: objectSchema({
      parentPath: { type: "string", minLength: 1 },
      style: styleSchema,
      scoreText: { type: "string" },
      coinsText: { type: "string" }
    }, ["parentPath"]),
    risk: "write"
  },
  {
    name: "unity_ui_validate",
    unityTool: "ui.validate",
    description: "Validate UI for overlap, mobile sizing, and basic layout issues.",
    inputSchema: objectSchema({
      rootPath: { type: "string", minLength: 1 },
      minButtonHeight: { type: "number", minimum: 1 },
      minButtonWidth: { type: "number", minimum: 1 }
    }),
    risk: "read"
  },
  {
    name: "unity_ui_mobile_controls_create",
    unityTool: "ui.mobileControls.create",
    description: "Create mobile game controls including joystick and action button.",
    inputSchema: objectSchema({
      parentPath: { type: "string", minLength: 1 },
      style: styleSchema
    }, ["parentPath"]),
    risk: "write"
  },
  {
    name: "unity_ui_rect_transform_set",
    unityTool: "ui.rectTransform.set",
    description: "Set RectTransform layout properties for a UI GameObject.",
    inputSchema: objectSchema({
      ...targetSchemaProperties,
      ...rectTransformProperties
    }),
    risk: "write"
  },
  {
    name: "unity_ui_virtual_button_create",
    unityTool: "ui.virtualButton.create",
    description: "Create a mobile virtual button.",
    inputSchema: objectSchema({
      parentPath: { type: "string", minLength: 1 },
      name: { type: "string", minLength: 1 },
      text: { type: "string" },
      position: vector2Schema,
      style: styleSchema
    }, ["parentPath"]),
    risk: "write"
  },
  {
    name: "unity_ui_joystick_create",
    unityTool: "ui.joystick.create",
    description: "Create a mobile virtual joystick.",
    inputSchema: objectSchema({
      parentPath: { type: "string", minLength: 1 },
      name: { type: "string", minLength: 1 },
      position: vector2Schema,
      style: styleSchema
    }, ["parentPath"]),
    risk: "write"
  }
];
