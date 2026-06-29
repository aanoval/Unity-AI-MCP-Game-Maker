export type JsonPrimitive = string | number | boolean | null;
export type JsonValue = JsonPrimitive | JsonValue[] | { [key: string]: JsonValue };
export type JsonObject = { [key: string]: JsonValue };

export type ToolRisk = "read" | "write" | "destructive" | "playmode" | "batch";

export interface UnityAiConfigFile {
  bindHost?: string;
  port?: number;
  authRequired?: boolean;
  token?: string;
}

export interface ResolvedConfig {
  projectPath: string;
  configPath: string;
  baseUrl: string;
  authRequired: boolean;
  token: string | null;
  timeoutMs: number;
  allowDangerous: boolean;
  allowPlaymode: boolean;
  allowBatch: boolean;
}

export interface UnityRpcResponse {
  ok: boolean;
  result?: JsonValue;
  error?: string;
}

export interface ToolDefinition {
  name: string;
  unityTool: string | null;
  description: string;
  inputSchema: JsonObject;
  risk: ToolRisk;
}
