import { objectSchema } from "./common.js";
import type { ToolDefinition } from "../types.js";

export const screenshotTools: ToolDefinition[] = [
  {
    name: "unity_screenshot_capture",
    unityTool: "screenshot.capture",
    description: "Capture the active Unity scene to a PNG.",
    inputSchema: objectSchema({
      outputPath: { type: "string", minLength: 1 },
      path: { type: "string", minLength: 1 },
      width: { type: "integer", minimum: 1, maximum: 8192 },
      height: { type: "integer", minimum: 1, maximum: 8192 },
      source: { type: "string", enum: ["camera", "gameView", "playMode", "game"] },
      cameraPath: { type: "string", minLength: 1 }
    }),
    risk: "write"
  },
  {
    name: "unity_screenshots_capture_scenes",
    unityTool: "screenshots.captureScenes",
    description: "Capture multiple Unity scenes to PNGs.",
    inputSchema: objectSchema({
      outputDir: { type: "string", minLength: 1 },
      filter: { type: "string", enum: ["menu", "menusandgameplay", "buildSettings", "gameplay", "all"] },
      width: { type: "integer", minimum: 1, maximum: 8192 },
      height: { type: "integer", minimum: 1, maximum: 8192 },
      source: { type: "string", enum: ["camera", "gameView", "playMode", "game"] },
      waitFrames: { type: "integer", minimum: 0, maximum: 600 },
      namePattern: { type: "string" },
      cameraPath: { type: "string" }
    }),
    risk: "batch"
  }
];
