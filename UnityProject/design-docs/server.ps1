# design-docs 本地静态服务器(零安装:Windows 自带 .NET HttpListener)
# 双击 serve.bat 调用本脚本;运行时渲染 .md 需经 http:// 打开(file:// 下 fetch 被 CORS 拦)。
param(
  [int]$Port = 8765,
  [string]$Root = $PSScriptRoot,
  [switch]$NoBrowser
)

$Root = (Resolve-Path -LiteralPath $Root).Path
$rootPrefix = $Root.TrimEnd('\') + '\'

$mime = @{
  '.html'='text/html; charset=utf-8'; '.htm'='text/html; charset=utf-8'
  '.js'='application/javascript; charset=utf-8'; '.mjs'='application/javascript; charset=utf-8'
  '.css'='text/css; charset=utf-8'; '.md'='text/markdown; charset=utf-8'
  '.json'='application/json; charset=utf-8'; '.svg'='image/svg+xml'
  '.png'='image/png'; '.jpg'='image/jpeg'; '.jpeg'='image/jpeg'; '.gif'='image/gif'
  '.webp'='image/webp'; '.ico'='image/x-icon'
  '.woff'='font/woff'; '.woff2'='font/woff2'; '.ttf'='font/ttf'
}

$listener = [System.Net.HttpListener]::new()
$prefix = "http://localhost:$Port/"
$listener.Prefixes.Add($prefix)
try {
  $listener.Start()
} catch {
  Write-Host "启动失败:$prefix" -ForegroundColor Red
  Write-Host $_.Exception.Message -ForegroundColor Red
  Write-Host "端口被占用就换端口(serve.bat 改 -Port);或已装 Python 时改用:python -m http.server $Port" -ForegroundColor Yellow
  Read-Host "按回车退出"
  exit 1
}

Write-Host "Serving $Root" -ForegroundColor Green
Write-Host "  $prefix" -ForegroundColor Cyan
Write-Host "关闭此窗口停止服务。" -ForegroundColor Yellow
if (-not $NoBrowser) { try { Start-Process $prefix } catch {} }

while ($listener.IsListening) {
  $ctx = $null
  try { $ctx = $listener.GetContext() } catch { break }
  $res = $ctx.Response
  try {
    $rel = [Uri]::UnescapeDataString($ctx.Request.Url.AbsolutePath).TrimStart('/')
    if ([string]::IsNullOrWhiteSpace($rel)) { $rel = 'index.html' }
    $full = Join-Path $Root $rel
    $resolved = $null
    try { $resolved = (Resolve-Path -LiteralPath $full).Path } catch {}
    $inside = $resolved -and ($resolved -eq $Root -or $resolved.StartsWith($rootPrefix, [System.StringComparison]::OrdinalIgnoreCase))
    if (-not $inside -or -not (Test-Path -LiteralPath $resolved -PathType Leaf)) {
      $res.StatusCode = 404
      $res.ContentType = 'text/plain; charset=utf-8'
      $nb = [System.Text.Encoding]::UTF8.GetBytes("404 Not Found: $rel")
      $res.OutputStream.Write($nb, 0, $nb.Length)
      $res.Close()
      continue
    }
    $ext = [System.IO.Path]::GetExtension($resolved).ToLowerInvariant()
    $ct = $mime[$ext]; if (-not $ct) { $ct = 'application/octet-stream' }
    $res.ContentType = $ct
    $bytes = [System.IO.File]::ReadAllBytes($resolved)
    $res.ContentLength64 = $bytes.Length
    $res.OutputStream.Write($bytes, 0, $bytes.Length)
    $res.Close()
  } catch {
    try { $res.StatusCode = 500; $res.Close() } catch {}
  }
}
