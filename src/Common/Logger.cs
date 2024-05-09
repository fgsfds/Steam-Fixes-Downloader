﻿using Common.Helpers;

namespace Common
{
    public class Logger
    {
        private readonly object _lock = new();
        private readonly List<string> _buffer = [];

        private string LogFile => Path.Combine(Directory.GetCurrentDirectory(), "superheater.log");

        public Logger()
        {
            try
            {
                File.Delete(LogFile);

                Info(Environment.OSVersion.ToString());
                Info(CommonProperties.CurrentVersion.ToString());
            }
            catch
            {
                ThrowHelper.Exception("Error while creating log file");
            }
        }

        public void Info(string message) => Log(message, "Info");

        public void Error(string message) => Log(message, "Error");

        /// <summary>
        /// Add message to the log file
        /// </summary>
        /// <param name="message"></param>
        /// <param name="type">Type of log message</param>
        private void Log(string message, string type)
        {
            lock (_lock)
            {
                message = $"[{DateTime.Now:dd.MM.yyyy HH.mm.ss}] [{type}] {message}";
                _buffer.Add(message);

                try
                {
                    File.WriteAllLines(LogFile, _buffer);
                    _buffer.Clear();
                }
                catch
                {
                }
            }
        }

        /// <summary>
        /// Upload log file to ftp
        /// </summary>
        public async Task UploadLogAsync()
        {
            //await _filesUploader.UploadFilesToFtpAsync(Consts.CrashlogsFolder, LogFile, DateTime.Now.ToString("dd.MM.yyyy_HH.mm.ss") + ".log").ConfigureAwait(false);
        }
    }
}
