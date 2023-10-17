﻿using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Win32;
using Common.Helpers;
using System.Runtime.InteropServices;

namespace Common.Entities
{
    public sealed class GameEntity
    {
        public GameEntity(
            int id,
            string name,
            string dir
            )
        {
            Id = id;
            Name = name;
            InstallDir = dir;
        }

        /// <summary>
        /// Steam game ID
        /// </summary>
        public int Id { get; init; }

        /// <summary>
        /// Game title
        /// </summary>
        public string Name { get; init; }

        /// <summary>
        /// Game install directory
        /// </summary>
        public string InstallDir { get; set; }

        /// <summary>
        /// Game icon
        /// </summary>
        public string Icon => SteamTools.SteamInstallPath is null 
            ? string.Empty
            : Path.Combine(
            SteamTools.SteamInstallPath,
            @$"appcache{Path.DirectorySeparatorChar}librarycache{Path.DirectorySeparatorChar}{Id}_icon.jpg"
            );

        public override string ToString() => Name;
    }
}