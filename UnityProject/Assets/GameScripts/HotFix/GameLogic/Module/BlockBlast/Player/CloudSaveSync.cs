using System;
using Cysharp.Threading.Tasks;

namespace GameLogic.BlockBlast.Player
{
    /// <summary>
    /// 云存档同步编排(P3 全栈迁移·客户端段)。持有本地单调 version,在登录后下载冲突解决、在存档边界节流上传。
    ///
    /// 纯逻辑(不依赖 UnityEngine 之外的网络库):经注入的 <see cref="ICloudSaveGateway"/> 发请求、经
    /// <see cref="CloudSaveCodec"/> 序列化/应用本地切片,故可 EditMode 单测(注桩 gateway + InMemory provider)。
    /// </summary>
    /// <remarks>
    /// 【version 语义】本地维护一个单调递增的 version(单独存储键,不塞进 blob 自身):
    /// - 上传前 version+1 再发(服务端要求 version > 0 且 > 已存)。
    /// - 下载到更新存档(ServerVersion > 本地)→ 本地 version = ServerVersion。
    /// - 上传遇 Stale(服务端有更新)→ 采用回带 ServerBlob + 本地 version = 响应 ServerVersion(让位高 version)。
    ///
    /// 【冲突解决】version 高者胜。单机单端常态下 Stale 极少触发;多端并发时按 version 高者胜避免数据丢失歧义。
    ///
    /// 【节流】上传按「有意义的存档边界」批量发,非每次微小变更都传(避免频繁 RPC + version 暴涨)。
    /// 节流 + 防重入 + 脏位:边界触发只标脏,实际上传由 <see cref="TryUploadThrottled"/> 据最小间隔 + 在途标志决策。
    /// </remarks>
    public sealed class CloudSaveSync
    {
        /// <summary>本地单调 version 存储键(独立于 blob,不塞进存档自身)。</summary>
        public const string VersionKey = "block_blast_cloud_version_v1";

        /// <summary>上传最小间隔(毫秒)。同一存档边界高频触发时,据此节流批量上传。</summary>
        public const long UploadThrottleMs = 10_000;

        /// <summary>blob 上限(1MB,服务端硬限;超限本棒先 flag 不强解,见类头)。</summary>
        public const int BlobLimitBytes = 1024 * 1024;

        private readonly ICloudSaveGateway _gateway;
        private readonly Func<long> _nowMsProvider; // 注入便于单测;生产 = 进程毫秒时钟

        private long _localVersion;
        private bool _loaded;

        // 上传节流 + 防重入。
        private bool _uploading;
        private bool _hasUploaded;     // 是否成功上传过(首传无节流;用 bool 而非时刻 sentinel,避免 now - MinValue 溢出)
        private long _lastUploadAtMs;
        private bool _pendingUpload; // 节流窗口内被触发过,待窗口到点补一发

        private bool _isReady;

        // 进玩法入口闸(MainMenuWindow 开始游戏)用:等登录下载对齐完成再恢复场景,避免读到旧本地。
        // 已就绪时 WhenReady 走快路径返 completed;未就绪时返此 source 的 task,在置 Ready 同处 TrySetResult 唤醒。
        private UniTaskCompletionSource _readySource;

        /// <summary>已下载对齐过(登录下载完成)。下载前不主动上传,避免拿未对齐的本地 version 覆盖云端。</summary>
        public bool IsReady
        {
            get => _isReady;
            private set
            {
                _isReady = value;
                if (value) _readySource?.TrySetResult(); // 置位即唤醒所有等待者(WhenReady)
            }
        }

        /// <summary>
        /// 进玩法入口闸:等待登录下载对齐完成。已就绪即返 completed task(常态零等待);
        /// 未就绪返一个在 <see cref="IsReady"/> 置位时完成的 task。
        /// <see cref="DownloadAndResolve"/> 在所有分支末尾置 IsReady,gateway 自带超时降级不挂,故此 task 必在有限时间完成。
        /// 调用方仍应配看门狗超时兜底,避免极端阻塞。
        /// </summary>
        public UniTask WhenReady()
        {
            if (_isReady) return UniTask.CompletedTask;
            _readySource ??= new UniTaskCompletionSource();
            return _readySource.Task;
        }

        /// <summary>当前本地 version(供日志/验收观测)。</summary>
        public long LocalVersion { get { EnsureLoaded(); return _localVersion; } }

        public CloudSaveSync(ICloudSaveGateway gateway, Func<long> nowMsProvider = null)
        {
            _gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
            _nowMsProvider = nowMsProvider ?? DefaultNowMs;
        }

        private static long DefaultNowMs()
        {
            // 进程内单调毫秒时钟(用于节流间隔比较,不用于落盘)。
            return (long)(UnityEngine.Time.realtimeSinceStartupAsDouble * 1000.0);
        }

        // ── 本地 version 持久化 ──────────────────────────────────

        private void EnsureLoaded()
        {
            if (_loaded) return;
            _loaded = true;
            try
            {
                if (Persistence.Provider.TryGet(VersionKey, out string raw) && long.TryParse(raw, out long v) && v > 0)
                    _localVersion = v;
                else
                    _localVersion = 0;
            }
            catch { _localVersion = 0; }
        }

        private void SetLocalVersion(long v)
        {
            EnsureLoaded();
            if (v <= 0) return;
            _localVersion = v;
            try { Persistence.Provider.Set(VersionKey, v.ToString()); }
            catch { /* ignore：version 落盘失败不阻断玩法 */ }
        }

        // ── 登录下载 + 冲突解决 ──────────────────────────────────

        /// <summary>
        /// 登录后拉云存档并冲突解决(在 P0 身份确立、P2 货币快照已应用之后调,避免次序冲突)。
        /// - Success 且 ServerVersion > 本地 → 应用服务端 blob(覆盖局内/标志/权重,保留本地货币+playerId)+ 本地 version = ServerVersion。
        /// - ServerVersion <= 本地 或 NoSnapshot → 保留本地(本地更新或对等),不覆盖。
        /// - ServiceUnavailable / NotLoggedIn → 用本地,稍后可重试,不崩。
        /// 完成(任一分支)后置 <see cref="IsReady"/>=true,此后才允许上传。即发即忘,不阻塞登录主流程。
        /// </summary>
        public async UniTask DownloadAndResolve()
        {
            EnsureLoaded();
            CloudDownloadResult result;
            try { result = await _gateway.DownloadAsync(); }
            catch { IsReady = true; return; } // 防御:gateway 契约不抛,此处兜底

            switch (result.Code)
            {
                case CloudDownloadCode.Success:
                    if (result.ServerVersion > _localVersion && result.ServerBlob != null && result.ServerBlob.Length > 0)
                    {
                        if (CloudSaveCodec.ApplyBlob(result.ServerBlob))
                            SetLocalVersion(result.ServerVersion);
                        // ApplyBlob 失败(blob 损坏):保留本地,不推进 version,不崩。
                    }
                    // ServerVersion <= 本地:本地更新或对等,保留本地。
                    break;
                case CloudDownloadCode.NoSnapshot:
                case CloudDownloadCode.ServiceUnavailable:
                case CloudDownloadCode.NotLoggedIn:
                default:
                    // 保留本地,不覆盖;稍后由上传把本地推到云端(或下次登录重试)。
                    break;
            }

            IsReady = true;
        }

        // ── 关键时机上传 + 节流 + Stale 冲突解决 ──────────────────

        /// <summary>
        /// 存档边界触发上传(节流)。未 Ready(下载未完成)不上传。距上次上传不足
        /// <see cref="UploadThrottleMs"/> → 标记 <see cref="_pendingUpload"/>,等窗口到点由下次触发补发(不丢)。
        /// 在途时直接标 pending 返回(防重入)。即发即忘,不阻塞玩法。
        /// </summary>
        public async UniTask TryUploadThrottled()
        {
            if (!IsReady) return;
            if (_uploading) { _pendingUpload = true; return; }

            // 首传无节流;此后据「距上次上传的真实间隔」判窗口(_hasUploaded 守卫避免 now - sentinel 溢出)。
            if (_hasUploaded && _nowMsProvider() - _lastUploadAtMs < UploadThrottleMs)
            {
                _pendingUpload = true; // 窗口内:压一发,待下次边界(已过窗口)补传
                return;
            }

            await UploadOnce();
        }

        /// <summary>
        /// 立即上传一次(无视节流;用于应用暂停 / 退出等「最后一刀」必落云端的边界)。未 Ready 不上传。
        /// </summary>
        public async UniTask UploadNow()
        {
            if (!IsReady || _uploading) return;
            await UploadOnce();
        }

        private async UniTask UploadOnce()
        {
            EnsureLoaded();
            _uploading = true;
            try
            {
                byte[] blob = CloudSaveCodec.BuildBlob();
                if (blob == null) return; // 组装失败:不发空请求

                if (blob.Length > BlobLimitBytes)
                {
                    // 本棒先 flag 不强解(缩体积 / 分块留后续):记 Log,不发(必被服务端 BlobTooLarge 拒)。
                    UnityEngine.Debug.LogWarning(
                        $"[CloudSave] blob 体积 {blob.Length} 超 1MB 上限,本次跳过上传(需缩减序列化体积或分块)。");
                    return;
                }

                long uploadVersion = _localVersion + 1; // 单调推进:服务端要求 version > 0 且 > 已存
                CloudUploadResult result;
                try { result = await _gateway.UploadAsync(uploadVersion, blob); }
                catch { return; } // 防御:gateway 契约不抛

                switch (result.Code)
                {
                    case CloudUploadCode.Accepted:
                        SetLocalVersion(result.ServerVersion > 0 ? result.ServerVersion : uploadVersion);
                        MarkUploaded();
                        break;

                    case CloudUploadCode.Stale:
                        // 服务端有更新存档(多端并发):采用回带 ServerBlob + 本地 version = 响应 ServerVersion(让位高 version)。
                        if (result.ServerBlob != null && result.ServerBlob.Length > 0)
                            CloudSaveCodec.ApplyBlob(result.ServerBlob);
                        if (result.ServerVersion > 0) SetLocalVersion(result.ServerVersion);
                        MarkUploaded();
                        break;

                    case CloudUploadCode.BlobTooLarge:
                        UnityEngine.Debug.LogWarning("[CloudSave] 服务端拒:BlobTooLarge(blob 超上限,需缩减体积或分块)。");
                        break;

                    case CloudUploadCode.InvalidRequest:
                        UnityEngine.Debug.LogWarning($"[CloudSave] 服务端拒:InvalidRequest(version={uploadVersion})。");
                        break;

                    case CloudUploadCode.NotLoggedIn:
                    case CloudUploadCode.ServiceUnavailable:
                    default:
                        // 稍后重试:不动 version(version 仍 = 本地基线,下次边界自然重发 +1),不崩。
                        break;
                }
            }
            finally
            {
                _uploading = false;
            }

            // 节流窗口内积压的触发:窗口已过则补发一次(只补一发,避免无限链)。
            if (_pendingUpload)
            {
                _pendingUpload = false;
                if (IsReady && (!_hasUploaded || _nowMsProvider() - _lastUploadAtMs >= UploadThrottleMs))
                    await UploadOnce();
            }
        }

        /// <summary>记一次成功上传(置首传标志 + 更新上次上传时刻),供节流窗口比较。</summary>
        private void MarkUploaded()
        {
            _hasUploaded = true;
            _lastUploadAtMs = _nowMsProvider();
        }
    }
}
