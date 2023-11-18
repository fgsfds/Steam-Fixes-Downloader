﻿using Common.Entities;
using Common.Entities.Fixes;
using Common.Entities.Fixes.RegistryFix;
using Common.Helpers;
using Microsoft.Win32;
using System.Runtime.InteropServices;

namespace Common.FixTools.RegistryFix
{
    public static class RegistryFixInstaller
    {
        /// <summary>
        /// Install registry fix
        /// </summary>
        /// <param name="game">Game entity</param>
        /// <param name="fix">Fix entity</param>
        /// <returns>Installed fix entity</returns>
        public static BaseInstalledFixEntity InstallFix(GameEntity game, RegistryFixEntity fix)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return ThrowHelper.PlatformNotSupportedException<BaseInstalledFixEntity>(string.Empty);

            var valueName = fix.ValueName.Replace("{gamefolder}", game.InstallDir).Replace("\\\\", "\\");

            var oldValue = (string?)Registry.GetValue(fix.Key, valueName, null);

            Registry.SetValue(fix.Key, valueName, fix.NewValueData);

            return new RegistryInstalledFixEntity(game.Id, fix.Guid, fix.Version, fix.Key, valueName, oldValue);
        }
    }
}
