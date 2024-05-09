﻿using Common.Entities;
using Common.Helpers;
using System.Collections.Immutable;

namespace Common.Providers.Cached
{
    public sealed class GamesProvider : CachedProviderBase<GameEntity>
    {
        private readonly SteamTools _steamTools;

        public GamesProvider(
            SteamTools steamTools,
            Logger logger
            ) : base(logger)
        {
            _steamTools = steamTools;
        }

        /// <inheritdoc/>
        internal override async Task<ImmutableList<GameEntity>> CreateCacheAsync()
        {
            _logger.Info("Creating games cache list");

            _cache = await Task.Run(() =>
            {
                var files = _steamTools.GetAcfsList();

                List<GameEntity> result = new(files.Count);

                foreach (var file in files)
                {
                    var games = GetGameEntityFromAcf(file);

                    if (games is null)
                    {
                        continue;
                    }

                    result.Add(games);
                }

                var cache = result.OrderBy(static x => x.Name).ToImmutableList();

                return cache;
            }).ConfigureAwait(false);

            _logger.Info($"Added {_cache.Count} games to the cache");

            return _cache;
        }

        /// <summary>
        /// Parse ACF file to GameEntity
        /// </summary>
        /// <param name="file">Path to ACF file</param>
        private GameEntity? GetGameEntityFromAcf(string file)
        {
            var libraryFolder = Path.GetDirectoryName(file) ?? ThrowHelper.Exception<string>("Can't find install dir");

            var lines = File.ReadAllLines(file);

            var id = -1;
            string? name = null;
            string? dir = null;

            foreach (var line in lines)
            {
                if (line.Contains("\"appid\""))
                {
                    var l = line.Split('"');

                    var z = l.ElementAt(l.Length - 2).Trim();

                    _ = int.TryParse(z, out id);
                }
                else if (line.Contains("\"name\""))
                {
                    var l = line.Split('"');

                    name = l.ElementAt(l.Length - 2).Trim();
                }
                else if (line.Contains("\"installdir\""))
                {
                    var l = line.Split('"');

                    dir = Path.Combine(libraryFolder, "common", l.ElementAt(l.Length - 2).Trim());
                }
            }

            if (!string.IsNullOrEmpty(dir) && !string.IsNullOrEmpty(name))
            {
                if (!dir.EndsWith('\\') &&
                    !dir.EndsWith('/'))
                {
                    dir += Path.DirectorySeparatorChar;
                }

                var icon = _steamTools.SteamInstallPath is null
                    ? string.Empty
                    : Path.Combine(
                        _steamTools.SteamInstallPath,
                        Path.Combine("appcache", "librarycache", $"{id}_icon.jpg")
                        );

                return new GameEntity()
                {
                    Id = id,
                    Name = name,
                    InstallDir = dir,
                    Icon = icon
                };
            }

            return null;
        }
    }
}