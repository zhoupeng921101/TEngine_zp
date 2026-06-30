#!/usr/bin/env bash
# ============================================================================
# 客户端构建产物上传脚本（增量，Linux + ssh/scp 目标）
#
# 把本地目录增量上传到远程目录：md5 比对，只传变化/新增文件；不删远端多余文件
# （避免误删服务器上的其它内容）。打包工具「一键部署」按钮经 Git Bash 调用本脚本，
# 也可手动跑：
#   LOCAL_DIR=/d/Builds/WebGL/... REMOTE_DIR=/workspace/.../WebGL \
#   SERVER_HOST=1.2.3.4 SSH_KEY=/d/path/key.pem bash upload.sh
#
# 环境变量：
#   LOCAL_DIR    必填，本地产物目录（上传其全部内容，保留相对结构）
#   REMOTE_DIR   必填，远端目标目录
#   SERVER_HOST  必填，SSH 主机（IP 或 ~/.ssh/config 别名）
#   SERVER_USER  SSH 用户名，默认 root
#   SSH_PORT     SSH 端口，默认 22
#   SSH_KEY      可选，私钥文件路径（.pem）；设置后 ssh 自动带 -i
#                （Git Bash 里 Windows 路径写成 /d/... 形式）
# ============================================================================
set -euo pipefail

LOCAL_DIR="${LOCAL_DIR:-}"
REMOTE_DIR="${REMOTE_DIR:-}"
SERVER_HOST="${SERVER_HOST:-}"
SERVER_USER="${SERVER_USER:-root}"
SSH_PORT="${SSH_PORT:-22}"
SSH_KEY="${SSH_KEY:-}"

[[ -z "$LOCAL_DIR" ]]   && { echo "ERROR: 必须设置 LOCAL_DIR" >&2; exit 1; }
[[ -z "$REMOTE_DIR" ]]  && { echo "ERROR: 必须设置 REMOTE_DIR" >&2; exit 1; }
[[ -z "$SERVER_HOST" ]] && { echo "ERROR: 必须设置 SERVER_HOST" >&2; exit 1; }
[[ ! -d "$LOCAL_DIR" ]] && { echo "ERROR: 本地产物目录不存在：$LOCAL_DIR" >&2; exit 1; }

# 有 SSH_KEY 则带 -i，并关掉首次连接的指纹确认交互
SSH_OPTS=(-p "$SSH_PORT")
if [[ -n "$SSH_KEY" ]]; then
  [[ ! -f "$SSH_KEY" ]] && { echo "ERROR: SSH_KEY 文件不存在：$SSH_KEY" >&2; exit 1; }
  chmod 600 "$SSH_KEY" 2>/dev/null || true
  SSH_OPTS+=(-i "$SSH_KEY" -o StrictHostKeyChecking=accept-new)
fi

TARGET="$SERVER_USER@$SERVER_HOST"
echo "==> 目标 $TARGET:$REMOTE_DIR (port $SSH_PORT)"
echo "==> 本地 $LOCAL_DIR"

ssh "${SSH_OPTS[@]}" "$TARGET" "mkdir -p '$REMOTE_DIR'"

# 归一化：消除 Git Bash(二进制模式 'hash *path') 与 Linux(文本模式 'hash  path') 的格式差异，
# 统一成 "hash path"（hash=前32列，path=第35列起）再排序，否则同内容文件会被误判为“变化”。
norm() { awk 'NF{print substr($0,1,32)" "substr($0,35)}' | LC_ALL=C sort; }
remote_md5="$(ssh "${SSH_OPTS[@]}" "$TARGET" \
  "cd '$REMOTE_DIR' && find . -type f -exec md5sum {} + 2>/dev/null" | norm || true)"
local_md5="$(cd "$LOCAL_DIR" && find . -type f -exec md5sum {} + | norm)"
# comm -23 取“只在本地出现”的行（新增 或 内容变更）→ 取路径
changed="$(LC_ALL=C comm -23 <(printf '%s\n' "$local_md5") <(printf '%s\n' "$remote_md5") | cut -d' ' -f2-)"
changed="$(printf '%s\n' "$changed" | grep -v '^$' || true)"

if [ -z "$changed" ]; then
  echo "==> 无文件变化，跳过传输。"
else
  n="$(printf '%s\n' "$changed" | wc -l | tr -d ' ')"
  total="$(printf '%s\n' "$local_md5" | wc -l | tr -d ' ')"
  echo "==> 变化文件 $n 个（共 $total 个）："
  printf '%s\n' "$changed" | sed 's#^\./#    #'
  # 打包变化文件 → 流式传输 → 远端解包（一次 ssh 连接，保留相对目录结构）
  printf '%s\n' "$changed" | tar -C "$LOCAL_DIR" -czf - -T - \
    | ssh "${SSH_OPTS[@]}" "$TARGET" "tar -C '$REMOTE_DIR' -xzf -"
fi

echo "==> 上传完成。"
