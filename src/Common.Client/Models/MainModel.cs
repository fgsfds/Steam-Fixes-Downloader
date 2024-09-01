using Common.Client.API;
using Common.Client.Config;
using Common.Client.Providers;
using Common.Entities.Fixes;
using Common.Entities.Fixes.FileFix;
using Common.Enums;
using Common.Helpers;
using System.Collections.Immutable;

namespace Common.Client.Models;

public sealed class MainModel
{
    private readonly IConfigProvider _config;
    private readonly FixesProvider _fixesProvider;
    private readonly GamesProvider _gamesProvider;
    private readonly ApiInterface _apiInterface;

    private List<FixesList> _combinedEntitiesList = [];

    public int UpdateableGamesCount => _combinedEntitiesList.Count(static x => x.HasUpdates);

    public bool HasUpdateableGames => UpdateableGamesCount > 0;


    public MainModel(
        IConfigProvider configProvider,
        FixesProvider fixesProvider,
        GamesProvider gamesProvider,
        ApiInterface apiInterface
        )
    {
        _config = configProvider;
        _fixesProvider = fixesProvider;
        _gamesProvider = gamesProvider;
        _apiInterface = apiInterface;
    }


    /// <summary>
    /// Update list of games either from cache or by downloading fixes.xml from repo
    /// </summary>
    public async Task<Result> UpdateGamesListAsync()
    {
        var result = await _fixesProvider.GetPreparedFixesListAsync().ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            return new(result.ResultEnum, result.Message);
        }

        var games = result.ResultObject;

        _combinedEntitiesList = games;

        return new(ResultEnum.Success, "Games list updated successfully");
    }

    /// <summary>
    /// Get list of games optionally filtered by a search string
    /// </summary>
    /// <param name="search">Search string</param>
    /// <param name="tag">Selected tag</param>
    /// <param name="additionalFix">Additional fix to add to the list</param>
    public async Task<ImmutableList<FixesList>> GetFilteredGamesListAsync(
        string? search = null,
        string? tag = null,
        FixesList? additionalFix = null
        )
    {
        var fixesList = _combinedEntitiesList;

        foreach (var entity in fixesList)
        {
            foreach (var fix in entity.Fixes)
            {
                //remove fixes with hidden tags
                if (fix.Tags is not null &&
                    fix.Tags.Count != 0 &&
                    fix.Tags.All(_config.HiddenTags.Contains))
                {
                    fix.IsHidden = true;
                    continue;
                }

                //remove fixes for different OSes
                if (!_config.ShowUnsupportedFixes &&
                    !fix.SupportedOSes.HasFlag(OSEnumHelper.CurrentOSEnum))
                {
                    fix.IsHidden = true;
                    continue;
                }

                if (!entity.IsGameInstalled &&
                    !_config.ShowUninstalledGames)
                {
                    fix.IsHidden = true;
                    continue;
                }

                if (ClientProperties.IsDeveloperMode)
                {
                    fix.IsHidden = false;
                }
                else if (!fix.IsInstalled)
                {
                    fix.IsHidden = fix.IsDisabled;
                }
            }
        }

        if (additionalFix is not null)
        {
            var existingGame = fixesList.FirstOrDefault(x => x.GameId == additionalFix.GameId);

            if (existingGame is not null)
            {
                var existingFix = existingGame.Fixes.FirstOrDefault(x => x.Guid == additionalFix.Fixes[0].Guid);

                if (existingFix is not null)
                {
                    _ = existingGame.Fixes.Remove(existingFix);
                }

                existingGame.Fixes.Add(additionalFix.Fixes[0]);
            }
            else
            {
                var games = await _gamesProvider.GetGamesListAsync().ConfigureAwait(false);
                var game = games.FirstOrDefault(x => x.Id == additionalFix.GameId);

                additionalFix.Game = game;
                fixesList.Add(additionalFix);
            }

        }

        if (!string.IsNullOrEmpty(tag))
        {
            if (tag.Equals(Consts.All))
            {
            }
            else if (tag.Equals(Consts.UpdateAvailable))
            {
                foreach (var entity in fixesList)
                {
                    foreach (var fix in entity.Fixes)
                    {
                        if (!fix.IsOutdated)
                        {
                            fix.IsHidden = true;
                        }
                    }
                }
            }
            else
            {
                foreach (var entity in fixesList)
                {
                    foreach (var fix in entity.Fixes)
                    {
                        if (fix.Tags is null ||
                            !fix.Tags.Exists(x => x.Equals(tag))
                            )
                        {
                            fix.IsHidden = true;
                        }
                    }
                }
            }
        }

        if (string.IsNullOrWhiteSpace(search))
        {
            return [.. fixesList.Where(static x => !x.IsEmpty)];
        }

        return [.. fixesList.Where(x => x.GameName.Contains(search, StringComparison.OrdinalIgnoreCase) && !x.IsEmpty)];
    }

    /// <summary>
    /// Get link to current fix's file
    /// </summary>
    /// <param name="fix">Fix</param>
    public string? GetFileFixUrl(BaseFixEntity? fix)
    {
        if (fix is not FileFixEntity fileFix)
        {
            return null;
        }

        if (string.IsNullOrEmpty(fileFix.Url))
        {
            return string.Empty;
        }

        return fileFix.Url;
    }

    /// <summary>
    /// Get list of all tags
    /// </summary>
    public HashSet<string> GetListOfTags()
    {
        HashSet<string> list = [];

        foreach (var entity in _combinedEntitiesList)
        {
            foreach (var fix in entity.Fixes)
            {
                if (fix.Tags is null)
                {
                    continue;
                }

                foreach (var tag in fix.Tags)
                {
                    if (!_config.HiddenTags.Contains(tag))
                    {
                        _ = list.Add(tag);
                    }
                }
            }

            list = [.. list.OrderBy(static x => x)];
        }

        var updateAvailable = _combinedEntitiesList.Any(x => x.HasUpdates);

        return [Consts.All, updateAvailable ? Consts.UpdateAvailable : null, .. list];
    }

    /// <summary>
    /// Get list of dependencies for a fix
    /// </summary>
    /// <param name="entity">Combined entity</param>
    /// <param name="fix">Fix entity</param>
    /// <returns>List of dependencies</returns>
    public IEnumerable<BaseFixEntity>? GetDependenciesForAFix(
        FixesList entity,
        BaseFixEntity fix
        )
    {
        if (fix.Dependencies is null or [])
        {
            return null;
        }

        var deps = entity.Fixes.Where(x => fix.Dependencies.Contains(x.Guid));

        return deps.Any() ? deps : null;
    }

    /// <summary>
    /// Get a list of dependencies that are currently not installed
    /// </summary>
    /// <param name="entity">Combined entity</param>
    /// <param name="fix">Fix entity</param>
    /// <returns>List of not installed dependencies</returns>
    public IEnumerable<BaseFixEntity>? GetNotInstalledDependencies(
        FixesList entity,
        BaseFixEntity fix
        )
    {
        var deps = GetDependenciesForAFix(entity, fix);

        if (deps is null)
        {
            return null;
        }

        var notInstalled = deps.Where(static x => !x.IsInstalled);

        var result = notInstalled.ToList();

        foreach (var dep in notInstalled)
        {
            var level2 = GetDependenciesForAFix(entity, dep);
            var notInstalled2 = level2?.Where(static x => !x.IsInstalled);

            if (notInstalled2 is null)
            {
                continue;
            }

            var newDeps = notInstalled2.Except(result);

            result.AddRange(newDeps);
        }

        return result.Count > 0 ? result.OrderBy(static x => x.DependencyLevel) : null;
    }

    /// <summary>
    /// Get list of fixes that depend on a selected fix
    /// </summary>
    /// <param name="fixes">List of fix entities</param>
    /// <param name="guid">Guid of a fix</param>
    /// <returns>List of dependent fixes</returns>
    public IEnumerable<BaseFixEntity>? GetDependentFixes(
        IEnumerable<BaseFixEntity> fixes,
        Guid guid
        )
    {
        var dependant = fixes.Where(x => x.Dependencies is not null && x.Dependencies.Contains(guid));

        return dependant.Any() ? dependant : null;
    }

    /// <summary>
    /// Get list of fixes that depend on a selected fix
    /// </summary>
    /// <param name="fixes">List of fix entities</param>
    /// <param name="guid">Guid of a fix</param>
    /// <returns>List of dependent fixes</returns>
    public IEnumerable<BaseFixEntity>? GetInstalledDependentFixes(
        IEnumerable<BaseFixEntity> fixes,
        Guid guid
        )
    {
        var dependant = GetDependentFixes(fixes, guid);

        var installed = dependant?.Where(x => x.IsInstalled);

        if (installed is null || !installed.Any())
        {
            return null;
        }

        return installed.OrderByDescending(x => x.DependencyLevel);
    }

    public void HideTag(string tag)
    {
        _config.ChangeTagState(tag, true);
    }

    public async Task<Result<int?>> ChangeVoteAsync(BaseFixEntity fix, bool needTpUpvote)
    {
        sbyte increment = 0;

        var doesEntryExist = _config.Upvotes.TryGetValue(fix.Guid, out var isUpvote);

        if (doesEntryExist)
        {
            if (isUpvote && needTpUpvote)
            {
                increment = -1;
            }
            else if (isUpvote && !needTpUpvote)
            {
                increment = -2;
            }
            else if (!isUpvote && needTpUpvote)
            {
                increment = 2;
            }
            else if (!isUpvote && !needTpUpvote)
            {
                increment = 1;
            }
        }
        else
        {
            if (needTpUpvote)
            {
                increment = 1;
            }
            else
            {
                increment = -1;
            }
        }

        var response = await _apiInterface.ChangeScoreAsync(fix.Guid, increment).ConfigureAwait(false);

        if (response.IsSuccess)
        {
            fix.Score = response.ResultObject;

            _config.ChangeFixUpvoteState(fix.Guid, needTpUpvote);
        }

        return response;
    }

    public async Task IncreaseInstalls(BaseFixEntity fix)
    {
        if (ClientProperties.IsDeveloperMode)
        {
            return;
        }

        var result = await _apiInterface.AddNumberOfInstallsAsync(fix.Guid).ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            return;
        }

        fix.Installs = result.ResultObject;
    }
}

