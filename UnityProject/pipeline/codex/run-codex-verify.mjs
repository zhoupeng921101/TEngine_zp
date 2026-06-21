// Codex 验证执行器(单一 recipe 源)。被 pipeline-*-test-codex 启动器 agent 调用:
// 跑 codex exec 做四类验证,codex 把报告写进 state 文件、把三态裁决写进 --output-last-message。
// 用法: node run-codex-verify.mjs --target server --prompt-file <file> --out <file>
//
// 仅服务端:客户端 test 定为永久留 Claude+UnityMCP、不迁 Codex(见 SKILL「端(target)」),故无 client 分支。
// recipe 经实测定型(2026-06-18,Windows):
//   - --sandbox workspace-write 在本机被降级为 read-only(跑不了命令/写不了文件),故用 yolo(用户已授权)。
//   - --ignore-rules 跳过 execpolicy(否则 dotnet/powershell 命令被「blocked by policy」)。
//   - 提示词须强制「原生 UTF-8 读取、勿用 PowerShell Get-Content」,否则中文 state 文件读成乱码。
//   - --cd Fantasy + --add-dir UnityProject(读 state / 写报告);--ignore-user-config 取干净 codex(无多余插件/MCP)。
import { spawn } from "node:child_process";
import process from "node:process";

const CODEX_BIN = process.env.CODEX_BIN
  || "C:/Users/pc/Documents/Codex/2026-06-18/claude-code-codex/outputs/claude-codex-pipeline/node_modules/@openai/codex/bin/codex.js";
const FANTASY = process.env.FANTASY_DIR || "D:/work/TEngine_block/Fantasy";
const UNITY = process.env.UNITY_DIR || "D:/work/TEngine_block/UnityProject";
const SCHEMA = `${UNITY}/pipeline/codex/test-schema.json`;
const TIMEOUT_MS = Number.parseInt(process.env.CODEX_VERIFY_TIMEOUT_MS || "1800000", 10); // 默认 30min

function arg(name) { const i = process.argv.indexOf(name); return i >= 0 ? process.argv[i + 1] : undefined; }
const target = arg("--target") || "server";
const promptFile = arg("--prompt-file");
const out = arg("--out");
if (target !== "server" || !promptFile || !out) {
  console.error("用法: node run-codex-verify.mjs --target server --prompt-file <file> --out <file>");
  process.exitCode = 1;
} else {
  const codexJs = CODEX_BIN.endsWith(".js");
  const codexArgs = [
    ...(codexJs ? [CODEX_BIN] : []),
    "exec", "-",
    "--cd", FANTASY,
    "--add-dir", UNITY,
    "--dangerously-bypass-approvals-and-sandbox",
    "--ignore-rules",
    "--skip-git-repo-check",
    "--ignore-user-config",
    "--output-schema", SCHEMA,
    "--output-last-message", out,
    "--color", "never",
  ];
  const bin = codexJs ? process.execPath : CODEX_BIN;

  // env 降敏(H3a 范式):剔除「像密钥/令牌」的变量,保留 codex 自身认证;收窄注入得手后的外泄面。
  const looksSensitive = n => {
    const s = n.toLowerCase();
    return /(^|_)(secret|token|password|passwd|credential|credentials|apikey|accesskey|privatekey)($|_)/.test(s)
      || /(_|^)api[_-]?key($|_)/.test(s) || /(key|token|secret)$/.test(s)
      || /^(aws|azure|gcp|google|gh|github|npm|slack|stripe|mongo|database)_/.test(s);
  };
  const keep = new Set(["openai_api_key", "codex_home", "codex_bin"]);
  const env = {};
  for (const [k, v] of Object.entries(process.env)) if (keep.has(k.toLowerCase()) || !looksSensitive(k)) env[k] = v;

  const promptText = await (await import("node:fs/promises")).readFile(promptFile, "utf8");
  const child = spawn(bin, codexArgs, { env, windowsHide: true, stdio: ["pipe", "inherit", "inherit"] });
  const timer = setTimeout(() => {
    if (process.platform === "win32") spawn("taskkill", ["/pid", String(child.pid), "/T", "/F"], { windowsHide: true }).on("error", () => {});
    else child.kill("SIGTERM");
  }, TIMEOUT_MS);
  child.on("error", e => { clearTimeout(timer); console.error(`codex failed to start: ${e.message}`); process.exitCode = 1; });
  child.on("close", code => { clearTimeout(timer); process.exitCode = code === 0 ? 0 : (code || 1); });
  child.stdin.on("error", () => {});
  child.stdin.end(promptText);
}
