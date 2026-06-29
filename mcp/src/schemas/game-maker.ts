import { objectSchema } from "./common.js";
import type { ToolDefinition } from "../types.js";

export const gameMakerTools: ToolDefinition[] = [
  {
    name: "unity_script_create",
    unityTool: "script.create",
    description: "Create a C# MonoBehaviour script asset from a template or supplied content.",
    inputSchema: objectSchema({
      className: { type: "string", minLength: 1 },
      path: { type: "string", minLength: 1 },
      template: { type: "string", enum: ["MonoBehaviour", "Trigger", "Collision", "Singleton"] },
      content: { type: "string" }
    }, ["className"]),
    risk: "write"
  },
  {
    name: "unity_physics_rigidbody_add",
    unityTool: "physics.rigidbody.add",
    description: "Add a Rigidbody to a GameObject.",
    inputSchema: objectSchema({
      path: { type: "string", minLength: 1 },
      name: { type: "string", minLength: 1 },
      mass: { type: "number" },
      useGravity: { type: "boolean" },
      isKinematic: { type: "boolean" }
    }),
    risk: "write"
  },
  {
    name: "unity_physics_collider_add",
    unityTool: "physics.collider.add",
    description: "Add a Collider to a GameObject.",
    inputSchema: objectSchema({
      path: { type: "string", minLength: 1 },
      name: { type: "string", minLength: 1 },
      type: { type: "string", enum: ["BoxCollider", "SphereCollider", "CapsuleCollider", "MeshCollider"] },
      isTrigger: { type: "boolean" }
    }),
    risk: "write"
  },
  {
    name: "unity_light_create",
    unityTool: "light.create",
    description: "Create a Unity Light GameObject.",
    inputSchema: objectSchema({
      name: { type: "string", minLength: 1 },
      type: { type: "string", enum: ["Directional", "Point", "Spot", "Area"] },
      intensity: { type: "number" }
    }),
    risk: "write"
  },
  {
    name: "unity_camera_create",
    unityTool: "camera.create",
    description: "Create a Unity Camera GameObject.",
    inputSchema: objectSchema({
      name: { type: "string", minLength: 1 },
      parentPath: { type: "string", minLength: 1 },
      fieldOfView: { type: "number" },
      orthographic: { type: "boolean" }
    }),
    risk: "write"
  },
  {
    name: "unity_console_clear",
    unityTool: "console.clear",
    description: "Clear Unity Editor console logs.",
    inputSchema: objectSchema({}),
    risk: "write"
  },
  {
    name: "unity_console_read",
    unityTool: "console.read",
    description: "Read recent Unity Editor console entries.",
    inputSchema: objectSchema({
      limit: { type: "integer", minimum: 1, maximum: 500 }
    }),
    risk: "read"
  },
  {
    name: "unity_playmode_start",
    unityTool: "playmode.start",
    description: "Enter Unity Play Mode.",
    inputSchema: objectSchema({}),
    risk: "playmode"
  },
  {
    name: "unity_playmode_stop",
    unityTool: "playmode.stop",
    description: "Exit Unity Play Mode.",
    inputSchema: objectSchema({}),
    risk: "playmode"
  },
  {
    name: "unity_sample_runner3d_create_scripts",
    unityTool: "sample.runner3D.createScripts",
    description: "Generate scripts for the bundled 3D runner sample.",
    inputSchema: objectSchema({}),
    risk: "write"
  },
  {
    name: "unity_sample_runner3d_create_content",
    unityTool: "sample.runner3D.createContent",
    description: "Generate scenes, prefabs, materials, and build settings for the bundled 3D runner sample.",
    inputSchema: objectSchema({}),
    risk: "write"
  }
];
