﻿using Common.Entities;
using Common.Entities.Fixes;
using Common.Entities.Fixes.HostsFix;
using Common.Helpers;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Common.FixTools.HostsFix
{
    public class HostsFixInstaller
    {
        /// <summary>
        /// Install Hosts fix
        /// </summary>
        /// <param name="game">Game entity</param>
        /// <param name="fix">Fix entity</param>
        /// <returns>Installed fix entity</returns>
        public BaseInstalledFixEntity InstallFix(GameEntity game, HostsFixEntity fix)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return ThrowHelper.PlatformNotSupportedException<BaseInstalledFixEntity>(string.Empty);
            }

            try
            {
                if (!CommonProperties.IsAdmin)
                {
                    Process.Start(new ProcessStartInfo { FileName = Environment.ProcessPath, UseShellExecute = true, Verb = "runas" });

                    Environment.Exit(0);
                }
            }
            catch
            {
                ThrowHelper.Exception("Superheater needs to be run as admin in order to install hosts fixes");
            }

            string stringToAdd = string.Empty;

            foreach (var line in fix.Entries)
            {
                stringToAdd += Environment.NewLine + line + $" #{fix.Guid}";
            }

            File.AppendAllText(Consts.Hosts, stringToAdd);

            return new HostsInstalledFixEntity(game.Id, fix.Guid, fix.Version, fix.Entries);
        }
    }
}
