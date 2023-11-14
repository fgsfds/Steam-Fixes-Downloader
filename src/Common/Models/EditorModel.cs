﻿using Common.Entities;
using Common.Helpers;
using Common.Providers;
using System.Collections.Immutable;
using System.Xml.Serialization;

namespace Common.Models
{
    public sealed class EditorModel
    {
        public EditorModel(
            FixesProvider fixesProvider,
            CombinedEntitiesProvider combinedEntitiesProvider,
            GamesProvider gamesProvider
            )
        {
            _fixesProvider = fixesProvider ?? ThrowHelper.ArgumentNullException<FixesProvider>(nameof(fixesProvider));
            _combinedEntitiesProvider = combinedEntitiesProvider ?? ThrowHelper.ArgumentNullException<CombinedEntitiesProvider>(nameof(combinedEntitiesProvider));
            _gamesProvider = gamesProvider ?? ThrowHelper.ArgumentNullException<GamesProvider>(nameof(gamesProvider));

            _fixesList = new();
            _availableGamesList = new();
        }

        private readonly FixesProvider _fixesProvider;
        private readonly CombinedEntitiesProvider _combinedEntitiesProvider;
        private readonly GamesProvider _gamesProvider;

        private readonly List<FixesList> _fixesList;
        private readonly List<GameEntity> _availableGamesList;

        /// <summary>
        /// Update list of fixes either from cache or by downloading fixes.xml from repo
        /// </summary>
        /// <param name="useCache">Is cache used</param>
        public async Task<Result> UpdateListsAsync(bool useCache)
        {
            try
            {
                await GetListOfFixesAsync(useCache);
                await GetListOfAvailableGamesAsync(useCache);

                return new(ResultEnum.Ok, string.Empty);
            }
            catch (Exception ex) when (ex is FileNotFoundException || ex is DirectoryNotFoundException)
            {
                return new(ResultEnum.NotFound, $"File not found: {ex.Message}");
            }
            catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException)
            {
                return new(ResultEnum.ConnectionError, "Can't connect to GitHub repository");
            }
        }

        /// <summary>
        /// Get list of fixes optionally filtered by a search string
        /// </summary>
        /// <param name="search">Search string</param>
        public ImmutableList<FixesList> GetFilteredGamesList(string? search = null)
        {
            if (!string.IsNullOrEmpty(search))
            {
                return _fixesList.Where(x => x.GameName.Contains(search, StringComparison.CurrentCultureIgnoreCase)).ToImmutableList();
            }
            else
            {
                return _fixesList.ToImmutableList();
            }
        }

        /// <summary>
        /// Get list of fixes optionally filtered by a search string
        /// </summary>
        /// <param name="search">Search string</param>
        public ImmutableList<GameEntity> GetAvailableGamesList() => _availableGamesList.ToImmutableList();

        /// <summary>
        /// Add new game with empty fix
        /// </summary>
        /// <param name="game">Game entity</param>
        /// <returns>New fixes list</returns>
        public FixesList AddNewGame(GameEntity game)
        {
            var newFix = new FixesList(game.Id, game.Name, new() { new() });

            _fixesList.Add(newFix);
            var newFixesList = _fixesList.OrderBy(x => x.GameName).ToList();
            _fixesList.Clear();
            _fixesList.AddRange(newFixesList);

            _availableGamesList.Remove(game);

            return newFix;
        }

        /// <summary>
        /// Add new fix for a game
        /// </summary>
        /// <param name="game">Game entity</param>
        /// <returns>New fix entity</returns>
        public static FixEntity AddNewFix(FixesList game)
        {
            FixEntity newFix = new();

            game.Fixes.Add(newFix);

            return newFix;
        }

        /// <summary>
        /// Remove fix from a game
        /// </summary>
        /// <param name="game">Game entity</param>
        /// <param name="fix">Fix entity</param>
        public static void RemoveFix(FixesList game, FixEntity fix)
        {
            game.Fixes.Remove(fix);

            if (game.Fixes.Count == 0)
            {

            }
        }

        /// <summary>
        /// Save current fixes list to XML
        /// </summary>
        /// <returns>Result message</returns>
        public async Task<Result> SaveFixesListAsync()
        {
            var result = await FixesProvider.SaveFixesAsync(_fixesList);

            CreateReadme();

            return result;
        }

        /// <summary>
        /// Get list of dependencies for a fix
        /// </summary>
        /// <param name="fixesList">Fixes list</param>
        /// <param name="fixEntity">Fix</param>
        /// <returns>List of dependencies</returns>
        public ImmutableList<FixEntity> GetDependenciesForAFix(FixesList? fixesList, FixEntity? fixEntity)
        {
            if (fixEntity?.Dependencies is null ||
                fixesList is null)
            {
                return [];
            }

            var allGameFixes = _fixesList.Where(x => x.GameId == fixesList.GameId).FirstOrDefault();

            if (allGameFixes is null)
            {
                return [];
            }

            var allGameDeps = fixEntity.Dependencies;

            var deps = allGameFixes.Fixes.Where(x => allGameDeps.Contains(x.Guid)).ToList();

            return deps.ToImmutableList();
        }

        /// <summary>
        /// Get list of fixes that can be added as dependencies
        /// </summary>
        /// <param name="fixesList">Fixes list</param>
        /// <param name="fixEntity">Fix</param>
        /// <returns>List of fixes</returns>
        public ImmutableList<FixEntity> GetListOfAvailableDependencies(FixesList? fixesList, FixEntity? fixEntity)
        {
            if (fixesList is null ||
                fixEntity is null)
            {
                return [];
            }

            List<FixEntity> result = new();

            var fixDependencies = GetDependenciesForAFix(fixesList, fixEntity);

            foreach (var fix in fixesList.Fixes)
            {
                if (
                    //don't add itself
                    fix.Guid != fixEntity.Guid &&
                    //don't add fixes that depend on it
                    (fix.Dependencies is not null && !fix.Dependencies.Any(x => x == fixEntity.Guid)) &&
                    //don't add fixes that are already dependencies
                    !fixDependencies.Where(x => x.Guid == fix.Guid).Any()
                    )
                {
                    result.Add(fix);
                }
            }

            return result.ToImmutableList();
        }

        /// <summary>
        /// Upload fix to ftp
        /// </summary>
        /// <param name="fixesList">Fixes list entity</param>
        /// <param name="fix">New fix</param>
        /// <returns>true if uploaded successfully</returns>
        public static Result UploadFix(FixesList fixesList, FixEntity fix)
        {
            var newFix = new FixesList(
                fixesList.GameId,
                fixesList.GameName,
                new List<FixEntity>() { fix }
                );

            var guid = newFix.Fixes.First().Guid;

            string? fileToUpload = null;

            var url = newFix.Fixes[0].Url;

            if (!string.IsNullOrEmpty(url) &&
                !url.StartsWith("http"))
            {
                fileToUpload = fix.Url;

                newFix.Fixes[0].Url = Path.GetFileName(fileToUpload);
            }

            XmlSerializer xmlSerializer = new(typeof(FixesList));

            List<string> filesToUpload = new();

            var fixFilePath = Path.Combine(Directory.GetCurrentDirectory(), "fix.xml");

            using (FileStream fs = new(fixFilePath, FileMode.Create))
            {
                xmlSerializer.Serialize(fs, newFix);
            }

            filesToUpload.Add(fixFilePath);

            if (fileToUpload is not null)
            {
                filesToUpload.Add(fileToUpload);
            }

            var result = FilesUploader.UploadFilesToFtp(guid.ToString(), filesToUpload);

            File.Delete(fixFilePath);

            if (result.ResultEnum is ResultEnum.Ok)
            {
                return new(ResultEnum.Ok, @$"Fix successfully uploaded.
It will be added to the database after developer's review.

Thank you.");
            }
            else
            {
                return new(ResultEnum.Error, result.Message);
            }
        }

        /// <summary>
        /// Check if the file can be uploaded
        /// </summary>
        public async Task<Result> CheckFixBeforeUploadAsync(FixEntity fix)
        {
            if (string.IsNullOrEmpty(fix?.Name) ||
                fix.Version < 1)
            {
                return new(ResultEnum.Error, "Name and Version are required to upload a fix.");
            }

            if (!string.IsNullOrEmpty(fix.Url) &&
                !fix.Url.StartsWith("http"))
            {
                if (!File.Exists(fix.Url))
                {
                    return new(ResultEnum.Error, $"{fix.Url} doesn't exist.");
                }

                else if (new FileInfo(fix.Url).Length > 1e+8)
                {
                    return new(ResultEnum.Error, $"Can't upload file larger than 100Mb.{Environment.NewLine}{Environment.NewLine}Please, upload it to file hosting.");
                }
            }

            var onlineFixes = await _fixesProvider.GetOnlineFixesListAsync();

            foreach (var onlineFix in onlineFixes)
            {
                //if fix already exists in the repo, don't upload it
                if (onlineFix.Fixes.Any(x => x.Guid == fix.Guid))
                {
                    return new(ResultEnum.Error, $"Can't upload fix.{Environment.NewLine}{Environment.NewLine}This fix already exists in the database.");
                }
            }

            return new(ResultEnum.Ok, string.Empty);
        }

        public static void AddDependencyForFix(FixEntity addTo, FixEntity dependency)
        {
            addTo.Dependencies ??= new();
            addTo.Dependencies.Add(dependency.Guid);
        }

        public static void RemoveDependencyForFix(FixEntity addTo, FixEntity dependency)
        {
            addTo.Dependencies ??= new();
            addTo.Dependencies.Remove(dependency.Guid);
        }

        public static void MoveFixUp(List<FixEntity> fixesList, int index) => fixesList.Move(index, index - 1);

        public static void MoveFixDown(List<FixEntity> fixesList, int index) => fixesList.Move(index, index + 1);

        /// <summary>
        /// Get sorted list of fixes
        /// </summary>
        /// <param name="useCache">Use cached list</param>
        private async Task GetListOfFixesAsync(bool useCache)
        {
            _fixesList.Clear();

            var fixes = await _combinedEntitiesProvider.GetFixesListAsync(useCache);

            fixes = fixes.OrderBy(x => x.GameName).ToList();

            foreach (var fix in fixes)
            {
                _fixesList.Add(fix);
            }
        }

        /// <summary>
        /// Get list of games that can be added to the fixes list
        /// </summary>
        private async Task GetListOfAvailableGamesAsync(bool useCache)
        {
            _availableGamesList.Clear();

            var installedGames = useCache
                ? await _gamesProvider.GetCachedListAsync()
                : await _gamesProvider.GetNewListAsync();

            foreach (var game in installedGames)
            {
                if (!_fixesList.Any(x => x.GameId == game.Id))
                {
                    _availableGamesList.Add(game);
                }
            }
        }

        /// <summary>
        /// Create readme file containing list of fixes
        /// </summary>
        private void CreateReadme()
        {
            string result = "**CURRENTLY AVAILABLE FIXES**" + Environment.NewLine + Environment.NewLine;

            foreach (var fix in _fixesList)
            {
                result += fix.GameName + Environment.NewLine;

                foreach (var f in fix.Fixes)
                {
                    result += "- " + f.Name + Environment.NewLine;
                }

                result += Environment.NewLine;
            }

            File.WriteAllText(Path.Combine(CommonProperties.LocalRepoPath, "README.md"), result);
        }
    }
}
