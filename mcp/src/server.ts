#!/usr/bin/env node
import { Server } from "@modelcontextprotocol/sdk/server/index.js";
import { StdioServerTransport } from "@modelcontextprotocol/sdk/server/stdio.js";
import {
  CallToolRequestSchema,
  ListToolsRequestSchema
} from "@modelcontextprotocol/sdk/types.js";
import { parseCliOptions, resolveConfig } from "./config.js";
import { formatError, McpAdapterError } from "./errors.js";
import { assertToolAllowed } from "./policy.js";
import { getTool, toolDefinitions, validateToolArguments } from "./tool-registry.js";
import { UnityAiClient } from "./unity-client.js";

async function main(): Promise<void> {
  const options = parseCliOptions(process.argv.slice(2));
  const config = resolveConfig(options);
  const client = new UnityAiClient(config);

  const server = new Server(
    {
      name: "unity-ai-game-maker",
      version: "0.1.0"
    },
    {
      capabilities: {
        tools: {}
      }
    }
  );

  server.setRequestHandler(ListToolsRequestSchema, async () => ({
    tools: toolDefinitions.map((tool) => ({
      name: tool.name,
      description: `${tool.description} Risk: ${tool.risk}.`,
      inputSchema: tool.inputSchema
    }))
  }));

  server.setRequestHandler(CallToolRequestSchema, async (request) => {
    const startedAt = Date.now();
    const tool = getTool(request.params.name);
    const args = validateToolArguments(tool, request.params.arguments);
    assertToolAllowed(tool, config);

    try {
      const result = tool.name === "unity_health"
        ? await client.health()
        : tool.name === "unity_list_tools"
          ? await client.tools()
          : await client.call(requiredUnityTool(tool.name, tool.unityTool), args);

      log(`${tool.name} ok ${Date.now() - startedAt}ms`);
      return {
        content: [
          {
            type: "text",
            text: JSON.stringify(result, null, 2)
          }
        ]
      };
    } catch (error) {
      log(`${tool.name} failed ${Date.now() - startedAt}ms ${formatError(error)}`);
      return {
        isError: true,
        content: [
          {
            type: "text",
            text: formatError(error)
          }
        ]
      };
    }
  });

  log(`Starting Unity AI Game Maker MCP for ${config.projectPath} at ${config.baseUrl}`);
  await server.connect(new StdioServerTransport());
}

function requiredUnityTool(mcpName: string, unityTool: string | null): string {
  if (!unityTool) {
    throw new McpAdapterError(`${mcpName} is not mapped to a Unity RPC tool.`, "TOOL_MAPPING_MISSING");
  }

  return unityTool;
}

function log(message: string): void {
  console.error(`[unity-ai-mcp] ${message}`);
}

main().catch((error) => {
  console.error(`[unity-ai-mcp] ${formatError(error)}`);
  process.exitCode = error instanceof McpAdapterError && error.code === "HELP" ? 0 : 1;
});
