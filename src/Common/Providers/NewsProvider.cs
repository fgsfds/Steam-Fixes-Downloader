﻿using Common.Config;
using Common.Entities;
using Common.Helpers;
using System.IO;
using System.Net.Http;
using System.Xml.Serialization;

namespace Common.Providers
{
    public class NewsProvider
    {
        private bool _isCacheUpdating;

        private readonly ConfigEntity _config;

        private List<NewsEntity>? _newsCache;

        public NewsProvider(ConfigProvider config)
        {
            _config = config.Config;
        }

        /// <summary>
        /// Get cached installed fixes list or create new cache if it wasn't created yet
        /// </summary>
        /// <returns></returns>
        /// <exception cref="NullReferenceException"></exception>
        public async Task<List<NewsEntity>> GetCachedNewsList()
        {
            while (_isCacheUpdating)
            {
                await Task.Delay(100);
            }

            if (_newsCache is null)
            {
                try
                {
                    await CreateNewsCacheAsync();
                }
                catch
                {
                    throw;
                }
                finally
                {
                    _isCacheUpdating = false;
                }
            }

            return _newsCache ?? throw new NullReferenceException(nameof(_newsCache));
        }

        /// <summary>
        /// Remove current cache, then create new one and return installed fixes list
        /// </summary>
        /// <returns></returns>
        public async Task<List<NewsEntity>> GetNewNewsList()
        {
            _newsCache = null;

            return await GetCachedNewsList();
        }

        /// <summary>
        /// Create new cache of news from online or local repository
        /// </summary>
        /// <returns></returns>
        /// <exception cref="NullReferenceException"></exception>
        private async Task CreateNewsCacheAsync()
        {
            _isCacheUpdating = true;

            string? news;

            if (_config.UseLocalRepo)
            {
                var file = Path.Combine(CommonProperties.LocalRepo, Consts.NewsFile);

                if (!File.Exists(file))
                {
                    throw new FileNotFoundException(file);
                }

                news = File.ReadAllText(file);
            }
            else
            {
                news = await DownloadNewsXMLAsync();
            }

            XmlSerializer xmlSerializer = new(typeof(List<NewsEntity>));

            List<NewsEntity>? newsList;

            try
            {
                using (StringReader fs = new(news))
                {
                    newsList = xmlSerializer.Deserialize(fs) as List<NewsEntity>;
                }
            }
            catch
            {
                newsList = new();
            }

            if (newsList is null)
            {
                throw new NullReferenceException(nameof(newsList));
            }

            _newsCache = newsList.OrderByDescending(x => x.Version).ToList();

            _isCacheUpdating = false;
        }

        /// <summary>
        /// Get list of news from online repository
        /// </summary>
        /// <returns></returns>
        private async Task<string> DownloadNewsXMLAsync()
        {
            try
            {
                using (var client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromSeconds(10);
                    using var stream = await client.GetStreamAsync(Consts.MainFixesRepo + Consts.NewsFile);
                    using var file = new StreamReader(stream);
                    var newsXml = await file.ReadToEndAsync();

                    return newsXml;
                }
            }
            catch (Exception)
            {
                throw;
            }
        }
    }
}
