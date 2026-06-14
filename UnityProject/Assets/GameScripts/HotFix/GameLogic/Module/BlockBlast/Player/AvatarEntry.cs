namespace GameLogic.BlockBlast.Player
{
    /// <summary>头像类型（设计 18 §3.5，= Luban avatar.EAvatarType 的语义值）。</summary>
    public static class AvatarType
    {
        /// <summary>头像。</summary>
        public const int Avatar = 1;
        /// <summary>头像框。</summary>
        public const int Frame = 2;
    }

    /// <summary>解锁条件（设计 18 §3.5，= Luban avatar.EUnlockCond 的语义值）。</summary>
    public static class UnlockCond
    {
        /// <summary>等级解锁。</summary>
        public const int Level = 1;
        /// <summary>活动发放（本轮留钩子 O3）。</summary>
        public const int Event = 2;
    }

    /// <summary>
    /// 头像/框定义的运行期 POCO，隔离 Luban 生成类型 <c>GameConfig.Avatar</c>（设计 18 §3.5）。
    /// 业务侧只认本类型，仿 <see cref="GameLogic.BlockBlast.Item.ItemDef"/>。
    /// </summary>
    /// <remarks>
    /// <see cref="Type"/>/<see cref="UnlockCond"/> 存枚举底层 int（业务侧不依赖 Luban 枚举类型，
    /// 解锁服务保持纯逻辑、不引生成代码）。头像与框共表，靠 <see cref="Type"/> 区分。
    /// </remarks>
    public sealed class AvatarEntry
    {
        /// <summary>主键。头像 id 用 1–100 段、框用 101+ 段（编排习惯；运行期靠 Type 区分）。</summary>
        public int Id;
        /// <summary>类型底层值（AvatarType：1 头像 / 2 框）。</summary>
        public int Type;
        /// <summary>图片资源名（占位，无美术，O2）。</summary>
        public string Image;
        /// <summary>解锁说明文本 id（指向多语言表，本轮存 id，O4）。</summary>
        public int UnlockText;
        /// <summary>解锁条件底层值（UnlockCond：1 等级 / 2 活动发放）。</summary>
        public int UnlockCond;
        /// <summary>条件参数：LEVEL 时 = 解锁所需等级；EVENT 时 = 活动 id（本轮不判，留值，§3.5）。</summary>
        public int UnlockParam;
    }
}
