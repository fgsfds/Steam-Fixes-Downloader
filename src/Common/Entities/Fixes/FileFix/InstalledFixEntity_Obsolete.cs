﻿using System.Xml.Serialization;

namespace Common.Entities.Fixes.FileFix
{
    //Obsolete, remove in version 1.0
    [XmlType("InstalledFixEntity")]
    public sealed class InstalledFixEntity_Obsolete() : IInstalledFixEntity
    {
        public InstalledFixEntity_Obsolete(int id, Guid guid, int version, string backupFolder, List<string>? list) : this ()
        {
            GameId = id;
            Guid = guid;
            Version = version;
            BackupFolder = backupFolder;
            FilesList = list;
        }

        /// <summary>
        /// Steam game ID
        /// </summary>
        public int GameId { get; init; }

        /// <summary>
        /// Fix GUID
        /// </summary>
        public Guid Guid { get; init; }

        /// <summary>
        /// Installed version
        /// </summary>
        public int Version { get; init; }

        /// <summary>
        /// Name of the backup folder
        /// </summary>
        public string BackupFolder { get; init; }

        /// <summary>
        /// Paths to files relative to the game folder
        /// </summary>
        public List<string>? FilesList { get; init; }
    }
}
