﻿using SteamFDCommon.Config;
using SteamFDCommon.DI;

namespace SteamFDCommon
{
    public class Consts
    {
        public const string ConfigFile = "config.xml";

        public const string FixesFile = "fixes.xml";

        public const string NewsFile = "news.xml";

        public const string InstalledFile = "installed.xml";

        public static string LocalRepo => BindingsManager.Instance.GetInstance<ConfigProvider>().Config.LocalRepoPath;

        public const string GitHubRepo = "https://github.com/fgsfds/SteamFD-Fixes-Repo/raw/master/";

        public const string PCGamingWikiUrl = "https://pcgamingwiki.com/api/appid.php?appid=";

        public const string AdminRegistryKey = "HKEY_CURRENT_USER\\SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\AppCompatFlags\\Layers";

        public const string BackupFolder = ".sfd";
    }
}
