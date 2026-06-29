import fs from "node:fs";
import path from "node:path";
import { McpAdapterError } from "./errors.js";
import type { ResolvedConfig, UnityAiConfigFile } from "./types.js";

const DEFAULT_PORT = 6421;
const DEFAULT_TIMEOUT_MS = 30_000;

export interface CliOptions {
  projectPath?: string;
  timeoutMs?: number;
  allowDangerous?: boolean;
  allowPlaymode?: boolean;
  allowBatch?: boolean;
}

export function parseCliOptions(argv: string[]): CliOptions {
  const options: CliOptions = {};

  for (let index = 0; index < argv.length; index += 1) {
    const arg = argv[index];
    const readValue = () => {
      const value = argv[index + 1];
      if (!value) {
        throw new McpAdapterError(`Missing value for ${arg}.`, "CONFIG_INVALID");
      }
      index += 1;
      return value;
    };

    if (arg === "--project") {
      options.projectPath = readValue();
    } else if (arg === "--timeout-ms") {
      options.timeoutMs = Number.parseInt(readValue(), 10);
    } else if (arg === "--allow-dangerous") {
      options.allowDangerous = true;
    } else if (arg === "--allow-playmode") {
      options.allowPlaymode = true;
    } else if (arg === "--allow-batch") {
      options.allowBatch = true;
    } else if (arg === "--help" || arg === "-h") {
      throw new McpAdapterError(helpText(), "HELP");
    } else {
      throw new McpAdapterError(`Unknown argument: ${arg}`, "CONFIG_INVALID");
    }
  }

  return options;
}

export function resolveConfig(options: CliOptions = {}, cwd = process.cwd()): ResolvedConfig {
  const projectPath = path.resolve(options.projectPath ?? process.env.UNITY_AI_PROJECT ?? cwd);
  const manifestPath = path.join(projectPath, "Packages", "manifest.json");
  if (!fs.existsSync(manifestPath)) {
    throw new McpAdapterError(
      `Unity project manifest not found at ${manifestPath}. Pass --project <UnityProject>.`,
      "PROJECT_NOT_FOUND"
    );
  }

  const configPath = path.join(projectPath, "UserSettings", "UnityAiGameMaker.json");
  if (!fs.existsSync(configPath)) {
    throw new McpAdapterError(
      `Unity AI Game Maker config not found at ${configPath}. Open Unity once or start the package to generate it.`,
      "UNITY_CONFIG_NOT_FOUND"
    );
  }

  const config = readConfigFile(configPath);
  const bindHost = config.bindHost || "127.0.0.1";
  if (bindHost !== "127.0.0.1" && bindHost !== "localhost") {
    throw new McpAdapterError(
      `Refusing non-loopback Unity endpoint host: ${bindHost}`,
      "UNSAFE_HOST"
    );
  }

  const port = config.port && config.port > 0 ? config.port : DEFAULT_PORT;
  const authRequired = config.authRequired !== false;
  const token = config.token || null;
  if (authRequired && !token) {
    throw new McpAdapterError("Unity auth is required but no token is configured.", "TOKEN_MISSING");
  }

  const requestedTimeout = options.timeoutMs ?? Number.parseInt(process.env.UNITY_AI_TIMEOUT_MS ?? "", 10);
  const timeoutMs = Number.isFinite(requestedTimeout) ? requestedTimeout : DEFAULT_TIMEOUT_MS;
  if (!Number.isFinite(timeoutMs) || timeoutMs <= 0) {
    throw new McpAdapterError("Timeout must be a positive integer.", "CONFIG_INVALID");
  }

  return {
    projectPath,
    configPath,
    baseUrl: `http://${bindHost}:${port}`,
    authRequired,
    token,
    timeoutMs,
    allowDangerous: options.allowDangerous ?? process.env.UNITY_AI_ALLOW_DANGEROUS === "1",
    allowPlaymode: options.allowPlaymode ?? process.env.UNITY_AI_ALLOW_PLAYMODE === "1",
    allowBatch: options.allowBatch ?? process.env.UNITY_AI_ALLOW_BATCH === "1"
  };
}

function readConfigFile(configPath: string): UnityAiConfigFile {
  try {
    return JSON.parse(fs.readFileSync(configPath, "utf8")) as UnityAiConfigFile;
  } catch (error) {
    throw new McpAdapterError(`Failed to read Unity config: ${configPath}`, "CONFIG_READ_FAILED", error);
  }
}

function helpText(): string {
  return `Usage: unity-ai-game-maker-mcp --project <UnityProject> [--timeout-ms <ms>] [--allow-playmode] [--allow-batch] [--allow-dangerous]`;
}
