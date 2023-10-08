﻿using Common.DI;
using Common.Entities;
using Common.Helpers;
using Common.Providers;
using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using System.Xml.Serialization;

namespace Common.Models
{
    public sealed class EditorModel
    {
        private readonly SortedList<string, FixesList> _fixesList;

        private readonly FixesProvider _fixesProvider;
        private readonly CombinedEntitiesProvider _combinedEntitiesProvider;
        private readonly GamesProvider _gamesProvider;

        public EditorModel(
            FixesProvider fixesProvider,
            CombinedEntitiesProvider combinedEntitiesProvider,
            GamesProvider gamesProvider
            )
        {
            _fixesList = new();
            _fixesProvider = fixesProvider ?? throw new NullReferenceException(nameof(fixesProvider));
            _combinedEntitiesProvider = combinedEntitiesProvider ?? throw new NullReferenceException(nameof(combinedEntitiesProvider));
            _gamesProvider = gamesProvider ?? throw new NullReferenceException(nameof(gamesProvider));
        }

        /// <summary>
        /// Update list of fixes either from cache or by downloading fixes.xml from repo
        /// </summary>
        /// <param name="useCache">Is cache used</param>
        public async Task UpdateFixesListAsync(bool useCache)
        {
            _fixesList.Clear();

            var fixes = await _combinedEntitiesProvider.GetFixesListAsync(useCache);

            fixes = fixes.OrderBy(x => x.GameName).ToList();

            foreach (var fix in fixes)
            {
                _fixesList.Add(fix.GameName, fix);
            }
        }

        /// <summary>
        /// Get list of fixes optionally filtered by a search string
        /// </summary>
        /// <param name="search">Search string</param>
        public List<FixesList> GetFilteredFixesList(string? search = null)
        {
            List<FixesList> result = new();

            result = _fixesList.Select(x => x.Value).Where(x => x.Fixes.Count > 0).ToList();

            if (!string.IsNullOrEmpty(search))
            {
                result = result.Where(x => x.GameName.ToLower().Contains(search.ToLower())).ToList();
            }

            return result;
        }

        /// <summary>
        /// Get list of games that can be added to the fixes list
        /// </summary>
        public async Task<List<GameEntity>> GetListOfGamesAvailableToAddAsync()
        {
            List<GameEntity> result = new();

            var installedGames = await _gamesProvider.GetCachedListAsync();

            foreach (var game in installedGames)
            {
                if (_fixesList.Any(x => x.Value.GameId == game.Id))
                {
                    continue;
                }

                result.Add(game);
            }

            return result;
        }

        /// <summary>
        /// Save current fixes list to XML
        /// </summary>
        /// <returns>Result message</returns>
        public Tuple<bool, string> SaveFixesListAsync()
        {
            var result = _fixesProvider.SaveFixes(_fixesList.Select(x => x.Value).ToList());

            CreateReadme();

            return result;
        }

        private void CreateReadme()
        {
            string result = "**CURRENTLY AVAILABLE FIXES**" + Environment.NewLine + Environment.NewLine;

            foreach (var fix in _fixesList)
            {
                result += fix.Value.GameName + Environment.NewLine;

                foreach (var f in fix.Value.Fixes)
                {
                    result += "- " + f.Name + Environment.NewLine;
                }

                result += Environment.NewLine;
            }

            File.WriteAllText(Path.Combine(CommonProperties.LocalRepoPath, "README.md"), result);
        }

        /// <summary>
        /// Get list of dependencies for a fix
        /// </summary>
        /// <param name="fixesList">Fixes list</param>
        /// <param name="fixEntity">Fix</param>
        /// <returns>List of dependencies</returns>
        public List<FixEntity> GetDependenciesForAFix(FixesList fixesList, FixEntity fixEntity)
        {
            if (fixEntity?.Dependencies is null)
            {
                return new List<FixEntity>();
            }

            var allGameFixes = _fixesList.Where(x => x.Value.GameId == fixesList.GameId).FirstOrDefault();

            if (allGameFixes.Value is null)
            {
                return new List<FixEntity>();
            }

            var allGameDeps = fixEntity.Dependencies;

            var deps = allGameFixes.Value.Fixes.Where(x => allGameDeps.Contains(x.Guid)).ToList();

            return deps;
        }

        /// <summary>
        /// Get list of fixes that can be added as dependencies
        /// </summary>
        /// <param name="fixesList">Fixes list</param>
        /// <param name="fixEntity">Fix</param>
        /// <returns>List of fixes</returns>
        public List<FixEntity> GetListOfDependenciesAvailableToAdd(FixesList fixesList, FixEntity fixEntity)
        {
            if (fixesList is null ||
                fixEntity is null)
            {
                return new List<FixEntity>();
            }

            List<FixEntity> result = new();

            var fixDependencies = GetDependenciesForAFix(fixesList, fixEntity);

            foreach (var fix in fixesList.Fixes)
            {

                if (
                    //don't add itself
                    fix.Guid != fixEntity.Guid &&
                    //don't add fixes that depend of it
                    !fix.Dependencies.Any(x => x == fixEntity.Guid) &&
                    //don't add fixes that are already dependencies
                    !fixDependencies.Where(x => x.Guid == fix.Guid).Any()
                    )
                {
                    result.Add(fix);
                }
            }

            return result;
        }

        public void AddDependencyForFix(FixEntity addTo, FixEntity dependency)
        {
            addTo.Dependencies.Add(dependency.Guid);
        }

        public void RemoveDependencyForFix(FixEntity addTo, FixEntity dependency)
        {
            addTo.Dependencies.Remove(dependency.Guid);
        }

        /// <summary>
        /// Add new fixes list for a game
        /// </summary>
        /// <param name="game">Game entity</param>
        public FixesList AddNewFix(GameEntity game)
        {
            var newFix = new FixesList(game.Id, game.Name, new ObservableCollection<FixEntity>());

            newFix.Fixes.Add(new FixEntity());

            _fixesList.Add(newFix.GameName, newFix);

            return newFix;
        }

        /// <summary>
        /// Upload fix to ftp
        /// </summary>
        /// <param name="fixesList">Fixes list entity</param>
        /// <param name="fix">New fix</param>
        /// <returns>true if uploaded successfully</returns>
        public bool UploadFix(FixesList fixesList, FixEntity fix)
        {
            var newFix = new FixesList(
                fixesList.GameId,
                fixesList.GameName,
                new(new List<FixEntity>() { fix })
                );

            var guid = newFix.Fixes.First().Guid;

            string? fileToUpload = null;

            if (!newFix.Fixes[0].Url.StartsWith("http"))
            {
                fileToUpload = fix.Url;

                newFix.Fixes[0].Url = Path.GetFileName(fileToUpload);
            }

            XmlSerializer xmlSerializer = new(typeof(FixesList));

            List<object> filesToUpload = new();

            using (MemoryStream fs = new())
            {
                xmlSerializer.Serialize(fs, newFix);

                filesToUpload.Add(new Tuple<string, MemoryStream>("fix.xml", fs));

                if (fileToUpload is not null)
                {
                    filesToUpload.Add(fileToUpload);
                }

                return FilesUploader.UploadFilesToFtp(guid.ToString(), filesToUpload);
            }
        }
    }
}
