import { McpAdapterError } from "./errors.js";
import type { JsonObject, JsonValue, ResolvedConfig, UnityRpcResponse } from "./types.js";

export class UnityAiClient {
  constructor(private readonly config: ResolvedConfig) {}

  async health(): Promise<JsonValue> {
    return this.request("/health");
  }

  async tools(): Promise<JsonValue> {
    return this.request("/tools", { authorized: true });
  }

  async call(tool: string, args: JsonObject): Promise<JsonValue> {
    const response = await this.request("/rpc", {
      authorized: true,
      method: "POST",
      body: JSON.stringify({ tool, args }),
      headers: { "Content-Type": "application/json" }
    }) as unknown as UnityRpcResponse;

    if (!response.ok) {
      throw new McpAdapterError(response.error || "Unity RPC failed.", "UNITY_RPC_FAILED", response);
    }

    return response.result ?? null;
  }

  private async request(path: string, options: RequestOptions = {}): Promise<JsonValue> {
    const controller = new AbortController();
    const timeout = setTimeout(() => controller.abort(), this.config.timeoutMs);

    try {
      const headers: Record<string, string> = { ...(options.headers ?? {}) };
      if (options.authorized && this.config.authRequired && this.config.token) {
        headers.Authorization = `Bearer ${this.config.token}`;
      }

      const response = await fetch(`${this.config.baseUrl}${path}`, {
        method: options.method ?? "GET",
        headers,
        body: options.body,
        signal: controller.signal
      });

      const text = await response.text();
      const payload = parseJson(text);
      if (!response.ok) {
        throw new McpAdapterError(
          `Unity endpoint returned HTTP ${response.status}.`,
          response.status === 401 ? "UNITY_UNAUTHORIZED" : "UNITY_HTTP_ERROR",
          payload
        );
      }

      return payload;
    } catch (error) {
      if (error instanceof McpAdapterError) {
        throw error;
      }

      if (error instanceof Error && error.name === "AbortError") {
        throw new McpAdapterError(`Unity request timed out after ${this.config.timeoutMs}ms.`, "UNITY_TIMEOUT");
      }

      throw new McpAdapterError(
        `Unable to reach Unity at ${this.config.baseUrl}. Is the Unity Editor open and the server running?`,
        "UNITY_UNREACHABLE",
        error
      );
    } finally {
      clearTimeout(timeout);
    }
  }
}

interface RequestOptions {
  authorized?: boolean;
  method?: string;
  headers?: Record<string, string>;
  body?: string;
}

function parseJson(text: string): JsonValue {
  if (!text) {
    return null;
  }

  try {
    return JSON.parse(text) as JsonValue;
  } catch {
    return text;
  }
}
