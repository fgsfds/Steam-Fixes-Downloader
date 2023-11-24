﻿using Common.Config;
using Common.Entities.Fixes;
using Common.Entities.Fixes.FileFix;
using Common.Entities.Fixes.HostsFix;
using Common.Entities.Fixes.RegistryFix;
using Common.Entities.Fixes.XML;
using Common.Helpers;
using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Xml.Serialization;

namespace Common.Providers
{
    public sealed class FixesProvider(
        ConfigProvider config,
        CommonProperties properties)
    {
        private string? _fixesCachedString;
        private readonly ConfigEntity _config = config.Config;
        private readonly CommonProperties _properties = properties;
        private readonly SemaphoreSlim _locker = new(1);

        /// <summary>
        /// Get cached fixes list from online or local repo or create new cache if it wasn't created yet
        /// </summary>
        public async Task<ImmutableList<FixesList>> GetCachedListAsync()
        {
            Logger.Info("Requesting cached fixes list");

            await _locker.WaitAsync();

            if (_fixesCachedString is null)
            {
                await CreateCacheAsync();
            }

            _locker.Release();

            if (_fixesCachedString is null)
            {
                ThrowHelper.Exception("Can't create fixes cache");
            }

            using (StringReader fs = new(_fixesCachedString))
            {
                return DeserializeCachedString(fs.ReadToEnd()).ToImmutableList();
            }
        }

        /// <summary>
        /// Remove current cache, then create new one and return fixes list
        /// </summary>
        public async Task<ImmutableList<FixesList>> GetNewListAsync()
        {
            Logger.Info("Requesting new fixes list");

            _fixesCachedString = null;

            return await GetCachedListAsync();
        }

        /// <summary>
        /// Get cached fixes list from online repo or create new cache if it wasn't created yet
        /// </summary>
        /// <returns></returns>
        /// <exception cref="NullReferenceException"></exception>
        public async Task<ImmutableList<FixesList>> GetOnlineFixesListAsync()
        {
            Logger.Info("Requesting online fixes");

            if (_config.UseLocalRepo)
            {
                var xmlString = await DownloadFixesXMLAsync();

                return DeserializeCachedString(xmlString).ToImmutableList();
            }
            else
            {
                return await GetCachedListAsync();
            }
        }

        /// <summary>
        /// Save list of fixes to XML
        /// </summary>
        /// <param name="fixesList"></param>
        /// <returns></returns>
        public async Task<Result> SaveFixesAsync(List<FixesList> fixesList)
        {
            Logger.Info("Saving fixes list");

            using HttpClient client = new();

            List<FixesListXml> result = new();

            foreach (var fixes in fixesList)
            {
                foreach (var fix in fixes.Fixes)
                {
                    if (fix.Tags is not null)
                    {
                        fix.Tags = fix.Tags.Where(x => !string.IsNullOrWhiteSpace(x)).ToList();

                        if (fix.Tags.Count == 0)
                        {
                            fix.Tags = null;
                        }
                        else
                        {
                            var tags = fix.Tags
                                .Where(x => !string.IsNullOrWhiteSpace(x))
                                .OrderBy(x => x)
                                .ToList();

                            fix.Tags = tags;
                        }
                    }

                    if (fix.Dependencies is not null && fix.Dependencies.Count == 0)
                    {
                        fix.Dependencies = null;
                    }

                    var fileFixResult = await PrepareFileFix(client, fix);
                    if (fileFixResult is not null)
                    {
                        return (Result)fileFixResult;
                    }
                }

                result.Add(new FixesListXml(fixes));
            }

            XmlSerializer xmlSerializer = new(typeof(List<FixesListXml>));

            if (!Directory.Exists(_properties.LocalRepoPath))
            {
                Directory.CreateDirectory(_properties.LocalRepoPath);
            }

            try
            {
                using FileStream fs = new(Path.Combine(_properties.LocalRepoPath, Consts.FixesFile), FileMode.Create);
                xmlSerializer.Serialize(fs, result);
            }
            catch (Exception ex) when (ex is FileNotFoundException || ex is DirectoryNotFoundException)
            {
                Logger.Error(ex.Message);
                return new Result(ResultEnum.NotFound, ex.Message);
            }

            Logger.Info("XML saved successfully!");
            return new(ResultEnum.Ok, "XML saved successfully!");
        }

        private async Task<Result?> PrepareFileFix(HttpClient client, BaseFixEntity fix)
        {
            if (fix is FileFixEntity fileFix)
            {
                if (!string.IsNullOrEmpty(fileFix.Url))
                {
                    if (!fileFix.Url.StartsWith("http"))
                    {
                        fileFix.Url = Consts.MainFixesRepo + "/raw/master/fixes/" + fileFix.Url;
                    }

                    if (fileFix.MD5 is null)
                    {
                        try
                        {
                            fileFix.MD5 = await GetMD5(client, fileFix);
                        }
                        catch (Exception ex)
                        {
                            Logger.Error(ex.Message);
                            return new Result(ResultEnum.ConnectionError, ex.Message);
                        }
                    }
                }

                if (fileFix.FilesToDelete is not null)
                {
                    fileFix.FilesToDelete = fileFix.FilesToDelete.Where(x => !string.IsNullOrWhiteSpace(x)).ToList();

                    if (fileFix.FilesToDelete.Count == 0)
                    {
                        fileFix.FilesToDelete = null;
                    }
                }

                if (fileFix.FilesToBackup is not null)
                {
                    fileFix.FilesToBackup = fileFix.FilesToBackup.Where(x => !string.IsNullOrWhiteSpace(x)).ToList();

                    if (fileFix.FilesToBackup.Count == 0)
                    {
                        fileFix.FilesToBackup = null;
                    }
                }

                if (fileFix.Variants is not null)
                {
                    fileFix.Variants = fileFix.Variants.Where(x => !string.IsNullOrWhiteSpace(x)).ToList();

                    if (fileFix.Variants.Count == 0)
                    {
                        fileFix.Variants = null;
                    }
                }

                if (fileFix.Url is not null && string.IsNullOrEmpty(fileFix.Url))
                {
                    fileFix.Url = null;
                }

                if (fileFix.Description is not null && string.IsNullOrEmpty(fileFix.Description))
                {
                    fileFix.Description = null;
                }

                if (fileFix.InstallFolder is not null && string.IsNullOrEmpty(fileFix.InstallFolder))
                {
                    fileFix.InstallFolder = null;
                }
            }

            return null;
        }

        /// <summary>
        /// Get MD5 of the local or online file
        /// </summary>
        /// <param name="client">Http client</param>
        /// <param name="fix">Fix entity</param>
        /// <returns>MD5 of the fix file</returns>
        /// <exception cref="Exception">Http response error</exception>
        private async Task<string> GetMD5(HttpClient client, FileFixEntity fix)
        {
            if (fix.Url is null)
            {
                ThrowHelper.NullReferenceException(nameof(fix.Url));
            };

            if (fix.Url.StartsWith(Consts.MainFixesRepo + "/raw"))
            {
                var currentDir = Path.Combine(_properties.LocalRepoPath, "fixes");
                var fileName = Path.GetFileName(fix.Url.ToString());
                var pathToFile = Path.Combine(currentDir, fileName);

                using (var md5 = MD5.Create())
                {
                    using (var stream = File.OpenRead(pathToFile))
                    {
                        return Convert.ToHexString(md5.ComputeHash(stream));
                    }
                }
            }
            else
            {
                using var response = await client.GetAsync(fix.Url, HttpCompletionOption.ResponseHeadersRead);

                if (!response.IsSuccessStatusCode)
                {
                    return ThrowHelper.Exception<string>($"Error while getting response for {fix.Url}: {response.StatusCode}");
                }
                else if (response.Content.Headers.ContentMD5 is not null)
                {
                    return BitConverter.ToString(response.Content.Headers.ContentMD5).Replace("-", string.Empty);
                }
                else
                {
                    //if can't get md5 from the response, download zip
                    var currentDir = Directory.GetCurrentDirectory();
                    var fileName = Path.GetFileName(fix.Url.ToString());
                    var pathToFile = Path.Combine(currentDir, fileName);

                    using (FileStream file = new(pathToFile, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        using var source = await response.Content.ReadAsStreamAsync();

                        await source.CopyToAsync(file);
                    }

                    response.Dispose();

                    string hash;

                    using (var md5 = MD5.Create())
                    {
                        using var stream = File.OpenRead(pathToFile);

                        hash = Convert.ToHexString(md5.ComputeHash(stream));
                    }

                    File.Delete(pathToFile);
                    return hash;
                }
            }
        }

        /// <summary>
        /// Create new cache of fixes from online or local repository
        /// </summary>
        private async Task CreateCacheAsync()
        {
            Logger.Info("Creating fixes cache");

            if (_config.UseLocalRepo)
            {
                var file = Path.Combine(_properties.LocalRepoPath, Consts.FixesFile);

                if (!File.Exists(file))
                {
                    ThrowHelper.FileNotFoundException(file);
                }

                _fixesCachedString = File.ReadAllText(file);
            }
            else
            {
                _fixesCachedString = await DownloadFixesXMLAsync();
            }
        }

        /// <summary>
        /// Deserialize string
        /// </summary>
        /// <param name="fixes">String to deserialize</param>
        /// <returns>List of fixes</returns>
        private static List<FixesList> DeserializeCachedString(string fixes)
        {
            List<FixesListXml>? fixesListXml;
            List<FixesList>? fixesListResult = new();

            XmlSerializer xmlSerializer = new(typeof(List<FixesListXml>));

            using (TextReader reader = new StringReader(fixes))
            {
                fixesListXml = xmlSerializer.Deserialize(reader) as List<FixesListXml>;
            }

            if (fixesListXml is null)
            {
                ThrowHelper.NullReferenceException(nameof(fixesListXml));
            }

            foreach (var fix in fixesListXml)
            {
                List<BaseFixEntity> fixesList = new();

                foreach (var f in fix.Fixes)
                {
                    if (f is FileFixEntity fileFix)
                    {
                        fixesList.Add(fileFix);
                    }
                    else if (f is RegistryFixEntity regFix)
                    {
                        fixesList.Add(regFix);
                    }
                    else if (f is HostsFixEntity hostsFix)
                    {
                        fixesList.Add(hostsFix);
                    }
                }

                fixesListResult.Add(new FixesList(fix.GameId, fix.GameName, fixesList));
            }

            return fixesListResult;
        }

        /// <summary>
        /// Download fixes xml from online repository
        /// </summary>
        /// <returns></returns>
        private async Task<string> DownloadFixesXMLAsync()
        {
            Logger.Info("Downloading fixes xml from online repository");

            using (HttpClient client = new())
            {
                client.Timeout = TimeSpan.FromSeconds(10);
                using var stream = await client.GetStreamAsync(_properties.CurrentFixesRepo + Consts.FixesFile);
                using StreamReader file = new(stream);
                var fixesXml = await file.ReadToEndAsync();

                return fixesXml;
            }
        }
    }
}
