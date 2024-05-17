﻿using Common;
using System.Web;

namespace ClientCommon
{
    public class FilesUploader
    {
        private readonly Logger _logger;
        private readonly ProgressReport _progressReport;

        public FilesUploader(
            Logger logger,
            ProgressReport progressReport)
        {
            _logger = logger;
            _progressReport = progressReport;
        }

        /// <summary>
        /// Upload single file to S3
        /// </summary>
        /// <param name="folder">Destination folder in the bucket</param>
        /// <param name="filePath">Path to file to upload</param>
        /// <param name="remoteFileName">File name on the s3 server</param>
        /// <returns>True if successfully uploaded</returns>
        public async Task<Result> UploadFilesToFtpAsync(string folder, string filePath, string remoteFileName, CancellationToken cancellationToken)
        {
            return await UploadFilesToFtpAsync(folder, [filePath], cancellationToken, remoteFileName).ConfigureAwait(false);
        }

        /// <summary>
        /// Upload multiple files to S3
        /// </summary>
        /// <param name="folder">Destination folder in the bucket</param>
        /// <param name="files">List of paths to files</param>
        /// <param name="remoteFileName">File name on the s3 server</param>
        /// <returns>True if successfully uploaded</returns>
        public async Task<Result> UploadFilesToFtpAsync(string folder, List<string> files, CancellationToken cancellationToken, string? remoteFileName = null)
        {
            _logger.Info($"Uploading {files.Count} file(s)");

            _progressReport.OperationMessage = "Uploading...";
            IProgress<float> progress = _progressReport.Progress;

            using HttpClient httpClient = new() { Timeout = Timeout.InfiniteTimeSpan };

            try
            {
                foreach (var file in files)
                {
                    var fileName = remoteFileName ?? Path.GetFileName(file);
                    var path = "superheater_uploads/" + folder + "/" + fileName;
                    var encodedPath = HttpUtility.UrlEncode(path);

                    var signedUrl = await httpClient.GetStringAsync($"{ApiProperties.ApiUrl}/storage/url/{encodedPath}").ConfigureAwait(false);

                    using var fileStream = File.OpenRead(file);
                    using StreamContent content = new(fileStream);

                    new Task(() => { TrackProgress(fileStream, progress); }).Start();

                    using var response = await httpClient.PutAsync(signedUrl, content, cancellationToken).ConfigureAwait(false);

                    if (!response.IsSuccessStatusCode)
                    {
                        var result = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                        return new(ResultEnum.Error, result);
                    }
                }
            }
            catch (TaskCanceledException)
            {
                _logger.Info("Uploading cancelled");
                return new(ResultEnum.Error, "Uploading cancelled");
            }
            catch (Exception ex)
            {
                _logger.Error(ex.Message);
                return new(ResultEnum.Error, ex.Message);
            }
            finally
            {
                _progressReport.OperationMessage = string.Empty;
            }

            return new(ResultEnum.Success, string.Empty);
        }

        private static void TrackProgress(FileStream streamToTrack, IProgress<float> progress)
        {
            while (streamToTrack.CanSeek)
            {
                var pos = ((float)streamToTrack.Position / (float)streamToTrack.Length) * 100;
                progress.Report(pos);

                Thread.Sleep(50);
            }
        }
    }
}