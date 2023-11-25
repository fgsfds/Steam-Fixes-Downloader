﻿using Common.Config;
using Common.Entities;
using Common.Entities.Fixes;
using Common.Entities.Fixes.FileFix;
using Common.Helpers;
using System.Diagnostics;
using System.Security.Cryptography;

namespace Common.FixTools.FileFix
{
    public sealed class FileFixInstaller(ConfigProvider config)
    {
        private readonly ConfigEntity _configEntity = config.Config ?? ThrowHelper.ArgumentNullException<ConfigEntity>(nameof(config));

        /// <summary>
        /// Install file fix: download ZIP, backup and delete files if needed, run post install events
        /// </summary>
        /// <param name="game">Game entity</param>
        /// <param name="fix">Fix entity</param>
        /// <param name="skipMD5Check">Don't check file against fix's MD5 hash</param>
        /// <exception cref="Exception">Error while downloading file</exception>
        /// <exception cref="HashCheckFailedException">MD5 of the downloaded file doesn't match provided MD5</exception>
        public async Task<BaseInstalledFixEntity> InstallFixAsync(GameEntity game, FileFixEntity fix, string? variant, bool skipMD5Check)
        {
            await CheckAndDownloadFileAsync(fix.Url, skipMD5Check ? null : fix.MD5);

            var backupFolderPath = CreateAndGetBackupFolder(game.InstallDir, fix.Name);

            BackupFiles(fix.FilesToDelete, game.InstallDir, backupFolderPath, true);
            BackupFiles(fix.FilesToBackup, game.InstallDir, backupFolderPath, false);

            var filesInArchive = await BackupFilesAndUnpackZIPAsync(game.InstallDir, fix.InstallFolder, fix.Url, backupFolderPath, variant);

            RunAfterInstall(game.InstallDir, fix.RunAfterInstall);

            if (!Directory.Exists(backupFolderPath))
            {
                backupFolderPath = null;
            }

            FileInstalledFixEntity installedFix = new()
            {
                GameId = game.Id,
                Guid = fix.Guid,
                Version = fix.Version,
                BackupFolder = backupFolderPath is null ? null : new DirectoryInfo(backupFolderPath).Name,
                FilesList = filesInArchive
            };

            return installedFix;
        }

        /// <summary>
        /// Check file's MD5 and download if MD5 is correct
        /// </summary>
        /// <param name="fixUrl">Url to the file</param>
        /// <param name="fixMD5">MD5 of the file</param>
        /// <exception cref="Exception">Error while downloading file</exception>
        /// <exception cref="HashCheckFailedException">MD5 of the downloaded file doesn't match provided MD5</exception>
        private async Task CheckAndDownloadFileAsync(string? fixUrl, string? fixMD5)
        {
            if (fixUrl is null)
            {
                return;
            }

            var zipFullPath = _configEntity.UseLocalRepo
                ? Path.Combine(_configEntity.LocalRepoPath, "fixes", Path.GetFileName(fixUrl))
                : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, Path.GetFileName(fixUrl));

            //checking md5 of the existing file
            if (File.Exists(zipFullPath))
            {
                Logger.Info($"Using local file {zipFullPath}");

                var result = CheckFileMD5(zipFullPath, fixMD5);

                if (!result)
                {
                    Logger.Info("MD5 of the local file doesn't match, removing it");
                    File.Delete(zipFullPath);
                }
            }

            if (!File.Exists(zipFullPath))
            {
                Logger.Info($"Local file {zipFullPath} not found");

                var url = fixUrl;

                if (_configEntity.UseTestRepoBranch)
                {
                    url = url.Replace("/master/", "/test/");
                }

                await FileTools.CheckAndDownloadFileAsync(new Uri(url), zipFullPath, fixMD5);
            }
        }

        /// <summary>
        /// Get path to backup folder and create if it doesn't exist
        /// </summary>
        /// <param name="gameDir">Game install directory</param>
        /// <param name="fixName">Name of the fix</param>
        /// <returns>Absolute path to the backup folder</returns>
        private static string CreateAndGetBackupFolder(string gameDir, string fixName)
        {
            var backupFolderPath = Path.Combine(gameDir, Consts.BackupFolder, fixName.Replace(' ', '_'));
            backupFolderPath = string.Join(string.Empty, backupFolderPath.Split(Path.GetInvalidPathChars()));

            if (Directory.Exists(backupFolderPath))
            {
                Directory.Delete(backupFolderPath, true);
            }

            return backupFolderPath;
        }

        /// <summary>
        /// Copy or move files to the backup folder
        /// </summary>
        /// <param name="files">List of files to backup</param>
        /// <param name="gameDir">Game install folder</param>
        /// <param name="backupFolderPath">Absolute path to the backup folder</param>
        /// <param name="deleteOriginal">Will original file be deleted</param>
        private static void BackupFiles(
            IEnumerable<string>? files,
            string gameDir,
            string backupFolderPath,
            bool deleteOriginal
            )
        {
            if (files is null || !files.Any())
            {
                return;
            }

            foreach (var file in files)
            {
                var fullFilePath = Path.Combine(gameDir, file);

                if (File.Exists(fullFilePath))
                {
                    var from = fullFilePath;
                    var to = Path.Combine(backupFolderPath, file);

                    var dir = Path.GetDirectoryName(to);

                    if (dir is null)
                    {
                        ThrowHelper.NullReferenceException(nameof(dir));
                    }

                    if (!Directory.Exists(dir))
                    {
                        Directory.CreateDirectory(dir);
                    }

                    if (deleteOriginal)
                    {
                        File.Move(from, to);
                    }
                    else
                    {
                        File.Copy(from, to);
                    }
                }
            }
        }

        /// <summary>
        /// Check MD5 of the local file
        /// </summary>
        /// <param name="filePath">Full path to the file</param>
        /// <param name="fixMD5">MD5 that the file's hash will be compared to</param>
        /// <returns>true if check is passed</returns>
        private static bool CheckFileMD5(string filePath, string? fixMD5)
        {
            if (fixMD5 is null) { return true; }

            string? hash;

            using (var md5 = MD5.Create())
            {
                using (var stream = File.OpenRead(filePath))
                {
                    hash = Convert.ToHexString(md5.ComputeHash(stream));
                }
            }

            if (!fixMD5.Equals(hash))
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Backup files that will be replaced and unpack Zip
        /// </summary>
        /// <param name="gameDir">Game install folder</param>
        /// <param name="fixInstallFolder">Fix install folder</param>
        /// <param name="fixUrl">Url to fix file</param>
        /// <param name="backupFolderPath">Path to backup folder</param>
        /// <param name="variant"></param>
        /// <returns>List of files in the archive</returns>
        private async Task<List<string>?> BackupFilesAndUnpackZIPAsync(
            string gameDir,
            string? fixInstallFolder,
            string? fixUrl,
            string backupFolderPath,
            string? variant)
        {
            if (fixUrl is null)
            {
                return null;
            }

            var unpackToPath = fixInstallFolder is null
                ? gameDir
                : Path.Combine(gameDir, fixInstallFolder) + Path.DirectorySeparatorChar;

            var zipFullPath = _configEntity.UseLocalRepo
                ? Path.Combine(_configEntity.LocalRepoPath, "fixes", Path.GetFileName(fixUrl))
                : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, Path.GetFileName(fixUrl));

            var filesInArchive = FileTools.GetListOfFilesInArchive(zipFullPath, unpackToPath, fixInstallFolder, variant);

            BackupFiles(filesInArchive, gameDir, backupFolderPath, true);

            await FileTools.UnpackArchiveAsync(zipFullPath, unpackToPath, variant);

            if (_configEntity.DeleteZipsAfterInstall &&
                !_configEntity.UseLocalRepo)
            {
                File.Delete(zipFullPath);
            }

            return filesInArchive;
        }

        /// <summary>
        /// Run or open whatever is in RunAfterInstall parameter
        /// </summary>
        /// <param name="gameInstallPath">Path to the game folder</param>
        /// <param name="runAfterInstall">File to open</param>
        private static void RunAfterInstall(
            string gameInstallPath,
            string? runAfterInstall
            )
        {
            if (runAfterInstall is null)
            {
                return;
            }

            var path = Path.Combine(gameInstallPath, runAfterInstall);

            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true,
                WorkingDirectory = gameInstallPath
            });
        }
    }
}
