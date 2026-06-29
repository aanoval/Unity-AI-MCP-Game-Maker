import { McpAdapterError } from "./errors.js";
import type { ResolvedConfig, ToolDefinition } from "./types.js";

export function assertToolAllowed(tool: ToolDefinition, config: ResolvedConfig): void {
  if (tool.risk === "destructive" && !config.allowDangerous) {
    throw new McpAdapterError(
      `${tool.name} is destructive and requires --allow-dangerous or UNITY_AI_ALLOW_DANGEROUS=1.`,
      "TOOL_DENIED"
    );
  }

  if (tool.risk === "playmode" && !config.allowPlaymode) {
    throw new McpAdapterError(
      `${tool.name} changes Play Mode and requires --allow-playmode or UNITY_AI_ALLOW_PLAYMODE=1.`,
      "TOOL_DENIED"
    );
  }

  if (tool.risk === "batch" && !config.allowBatch) {
    throw new McpAdapterError(
      `${tool.name} runs batch-style work and requires --allow-batch or UNITY_AI_ALLOW_BATCH=1.`,
      "TOOL_DENIED"
    );
  }
}
