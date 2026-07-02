using TEngine;

namespace GameLogic
{
    /// <summary>
    /// 登录账号本地存储。保存上次登录成功的账号名,供下次启动自动登录与登录窗预填。
    /// 账号名是可丢便捷缓存、非权威:玩家权威身份 playerId 由服务端登录签发(见 FantasyNetwork.OnPlayerIdIssued),
    /// 本地账号名丢失只需用户重新输入登录,不影响服务端数据归属。
    /// </summary>
    public static class LoginAccountStore
    {
        private const string Key = "Login.Account";

        /// <summary>读取已保存的登录账号;无则返回空串。</summary>
        public static string Get() => Utility.PlayerPrefs.GetString(Key, string.Empty);

        /// <summary>保存登录账号(空值忽略,不覆盖既有值)。</summary>
        public static void Save(string account)
        {
            if (string.IsNullOrEmpty(account))
            {
                return;
            }
            Utility.PlayerPrefs.SetString(Key, account);
        }
    }
}
