#!/usr/bin/env node

import fs from 'node:fs';
import path from 'node:path';

function usage() {
  console.log(`Usage:
  unity-ai <projectPath> health
  unity-ai <projectPath> tools
  unity-ai <projectPath> call <tool> <jsonArgs>

Examples:
  unity-ai /path/to/project health
  unity-ai /path/to/project tools
  unity-ai /path/to/project call scene.listOpen '{}'
`);
}

function readConfig(projectPath) {
  const configPath = path.join(projectPath, 'UserSettings', 'UnityAiConnector.json');
  if (!fs.existsSync(configPath)) {
    throw new Error(`Config not found: ${configPath}. Start the connector once from Unity first.`);
  }

  return JSON.parse(fs.readFileSync(configPath, 'utf8'));
}

async function requestJson(url, options = {}) {
  const response = await fetch(url, options);
  const text = await response.text();
  let data;

  try {
    data = text ? JSON.parse(text) : null;
  } catch {
    data = text;
  }

  if (!response.ok) {
    throw new Error(typeof data === 'string' ? data : JSON.stringify(data));
  }

  return data;
}

async function main() {
  const [projectPath, command, ...rest] = process.argv.slice(2);
  if (!projectPath || !command) {
    usage();
    process.exitCode = 1;
    return;
  }

  const config = readConfig(projectPath);
  const host = config.bindHost ?? '127.0.0.1';
  const port = config.port ?? 6421;
  const baseUrl = `http://${host}:${port}`;

  if (command === 'health') {
    const data = await requestJson(`${baseUrl}/health`);
    console.log(JSON.stringify(data, null, 2));
    return;
  }

  const headers = {
    Authorization: `Bearer ${config.token}`,
  };

  if (command === 'tools') {
    const data = await requestJson(`${baseUrl}/tools`, { headers });
    console.log(JSON.stringify(data, null, 2));
    return;
  }

  if (command === 'call') {
    const [tool, jsonArgs = '{}'] = rest;
    if (!tool) {
      throw new Error('Missing tool name.');
    }

    const args = JSON.parse(jsonArgs);
    const data = await requestJson(`${baseUrl}/rpc`, {
      method: 'POST',
      headers: {
        ...headers,
        'Content-Type': 'application/json',
      },
      body: JSON.stringify({ tool, args }),
    });
    console.log(JSON.stringify(data, null, 2));
    return;
  }

  usage();
  process.exitCode = 1;
}

main().catch((error) => {
  console.error(error.message);
  process.exitCode = 1;
});
