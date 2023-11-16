﻿using Common.Entities;
using Common.Entities.Fixes;
using Common.Entities.Fixes.FileFix;
using Common.Helpers;

namespace Common.FixTools.FileFix
{
    public sealed class FileFixUninstaller : IFixUninstaller
    {
        /// <summary>
        /// Uninstall fix: delete files, restore backup
        /// </summary>
        /// <param name="game">Game entity</param>
        /// <param name="installedFix">Installed fix entity</param>
        /// <param name="fixEntity">Fix entity</param>
        public void UninstallFix(GameEntity game, IInstalledFixEntity installedFix, IFixEntity fixEntity)
        {
            if (installedFix is not FileInstalledFixEntity fileInstalledFix) { ThrowHelper.ArgumentException(nameof(installedFix)); return; }
            if (fixEntity is not FileFixEntity fileFixEntity) { ThrowHelper.ArgumentException(nameof(fixEntity)); return; }

            DeleteFiles(game.InstallDir, fileInstalledFix.FilesList);

            RestoreBackup(game.InstallDir, fileInstalledFix, fileFixEntity.Url);

            DeleteBackupFolderIfEmpty(game.InstallDir);
        }

        /// <summary>
        /// Delete files from game install directory
        /// </summary>
        /// <param name="gameInstallDir">Game install folder</param>
        /// <param name="fixFiles">Files to delete</param>
        private static void DeleteFiles(string gameInstallDir, List<string>? fixFiles)
        {
            if (fixFiles is null) return;

            //checking if files can be opened before deleting them
            foreach (var file in fixFiles)
            {
                var fullPath = Path.Combine(gameInstallDir, file);

                if (!file.EndsWith('/') &&
                    File.Exists(fullPath))
                {
                    var stream = File.Open(fullPath, FileMode.Open);
                    stream.Dispose();
                }
            }

            foreach (var file in fixFiles)
            {
                var fullPath = Path.Combine(gameInstallDir, file);

                if (!file.EndsWith('/') &&
                    File.Exists(fullPath))
                {
                    File.Delete(fullPath);
                }
                else if (Directory.Exists(fullPath))
                {
                    Directory.Delete(fullPath, true);
                }
            }
        }

        /// <summary>
        /// Restore backed up files
        /// </summary>
        /// <param name="gameDir">Game install folder</param>
        /// <param name="fix">Installed fix</param>
        private static void RestoreBackup(
            string gameDir,
            FileInstalledFixEntity fix,
            string? fixUrl)
        {
            string backupFolder;

            if (fix.BackupFolder is not null)
            {
                backupFolder = Path.Combine(gameDir, Consts.BackupFolder, fix.BackupFolder);
            }
            //TODO: Added for backwards compatibility, need to remove some time later
            else
            {
                if (fixUrl is null) ThrowHelper.BackwardsCompatibilityException("Can't get backup folder.");

                backupFolder = Path.Combine(gameDir, Consts.BackupFolder, Path.GetFileNameWithoutExtension(fixUrl));
            }

            if (!Directory.Exists(backupFolder))
            {
                return;
            }

            var files = Directory.GetFiles(backupFolder, "*.*", SearchOption.AllDirectories);

            foreach (var file in files)
            {
                var relativePath = Path.GetRelativePath(backupFolder, file);

                var pathTo = Path.Combine(gameDir, relativePath);

                File.Move(file, pathTo, true);
            }

            Directory.Delete(backupFolder, true);
        }

        /// <summary>
        /// Delete backup folder if it's empty
        /// </summary>
        /// <param name="gameInstallDir">Game install folder</param>
        private static void DeleteBackupFolderIfEmpty(string gameInstallDir)
        {
            var backupFolder = Path.Combine(gameInstallDir, Consts.BackupFolder);

            if (Directory.Exists(backupFolder) &&
                Directory.GetFiles(backupFolder).Length == 0 &&
                Directory.GetDirectories(backupFolder).Length == 0)
            {
                Directory.Delete(backupFolder);
            }
        }
    }
}