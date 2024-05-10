﻿using Common.Helpers;
using System.Web;

namespace Common
{
    public class FilesUploader
    {
        private readonly HttpClientInstance _httpClient;
        private readonly Logger _logger;

        public FilesUploader(
            HttpClientInstance httpClient,
            Logger logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }

        /// <summary>
        /// Upload single file to S3
        /// </summary>
        /// <param name="folder">Destination folder in the bucket</param>
        /// <param name="filePath">Path to file to upload</param>
        /// <param name="remoteFileName">File name on the s3 server</param>
        /// <returns>True if successfully uploaded</returns>
        public async Task<Result> UploadFilesToFtpAsync(string folder, string filePath, string remoteFileName)
        {
            return await UploadFilesToFtpAsync(folder, [filePath], remoteFileName).ConfigureAwait(false);
        }

        /// <summary>
        /// Upload multiple files to S3
        /// </summary>
        /// <param name="folder">Destination folder in the bucket</param>
        /// <param name="files">List of paths to files</param>
        /// <param name="remoteFileName">File name on the s3 server</param>
        /// <returns>True if successfully uploaded</returns>
        public async Task<Result> UploadFilesToFtpAsync(string folder, List<string> files, string? remoteFileName = null)
        {
            _logger.Info($"Uploading {files.Count} file(s)");

            try
            {
                foreach (var file in files)
                {
                    var fileName = remoteFileName ?? Path.GetFileName(file);
                    var path = "superheater_uploads/" + folder + "/" + fileName;
                    var encodedPath = HttpUtility.UrlEncode(path);

                    var signedUrl = await _httpClient.GetStringAsync($"{CommonProperties.ApiUrl}/storage/url/{encodedPath}").ConfigureAwait(false);

                    using var stream = File.OpenRead(file);
                    using StreamContent content = new(stream);

                    await _httpClient.PutAsync(signedUrl, content).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex.Message);
                return new(ResultEnum.Error, ex.Message);
            }

            return new(ResultEnum.Success, string.Empty);
        }

        /// <summary>
        /// Upload log file to S3
        /// </summary>
        public async Task UploadLogAsync()
        {
            await UploadFilesToFtpAsync(Consts.CrashlogsFolder, _logger.LogFile, DateTime.Now.ToString("dd.MM.yyyy_HH.mm.ss") + ".log").ConfigureAwait(false);
        }
    }
}
