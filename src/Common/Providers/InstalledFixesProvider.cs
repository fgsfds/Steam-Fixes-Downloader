﻿using Common.Entities.CombinedEntities;
using Common.Entities.Fixes;
using Common.Entities.Fixes.FileFix;
using Common.Entities.Fixes.RegistryFix;
using Common.Entities.Fixes.XML;
using Common.Helpers;
using System.Collections.Immutable;
using System.Xml.Serialization;

namespace Common.Providers
{
    public static class InstalledFixesProvider
    {
        /// <summary>
        /// Remove current cache, then create new one and return installed fixes list
        /// </summary>
        /// <returns></returns>
        public static ImmutableList<BaseInstalledFixEntity> GetInstalledFixes()
        {
            if (!File.Exists(Consts.InstalledFile))
            {
                return [];
            }

            List<BaseInstalledFixEntity>? fixesDatabase;

            try
            {
                fixesDatabase = GetNewInstalledFixes();
            }
            catch
            {
                fixesDatabase = GetOldInstalledFixes();
            }

            return fixesDatabase.ToImmutableList();
        }

        private static List<BaseInstalledFixEntity> GetNewInstalledFixes()
        {
            XmlSerializer xmlSerializer = new(typeof(InstalledFixesXml));

            using (FileStream fs = new(Consts.InstalledFile, FileMode.Open))
            {
                var fixesDatabase = xmlSerializer.Deserialize(fs) as InstalledFixesXml;

                if (fixesDatabase is null) { ThrowHelper.NullReferenceException(nameof(fixesDatabase)); }

                List<BaseInstalledFixEntity> result = new();

                foreach (var fix in fixesDatabase.InstalledFixes)
                {
                    if (fix is FileInstalledFixEntity fileFix)
                    {
                        result.Add(fileFix);
                    }
                    else if (fix is RegistryInstalledFixEntity regFix)
                    {
                        result.Add(regFix);
                    }
                }

                return result;
            }
        }

        [Obsolete("Remove in version 1.0")]
        private static List<BaseInstalledFixEntity> GetOldInstalledFixes()
        {
            XmlSerializer xmlSerializer = new(typeof(List<InstalledFixEntity_Obsolete>));

            using (FileStream fs = new(Consts.InstalledFile, FileMode.Open))
            {
                var fixesDatabase = xmlSerializer.Deserialize(fs) as List<InstalledFixEntity_Obsolete>;

                if (fixesDatabase is null) { ThrowHelper.NullReferenceException(nameof(fixesDatabase)); }

                return fixesDatabase.ConvertAll(x =>
                    (BaseInstalledFixEntity)new FileInstalledFixEntity(
                        x.GameId,
                        x.Guid,
                        x.Version,
                        x.BackupFolder,
                        x.FilesList
                        ));
            }
        }

        /// <summary>
        /// Save list of installed fixes from combined entities list
        /// </summary>
        /// <param name="combinedEntitiesList">List of combined entities</param>
        /// <returns>result, error message</returns>
        public static Result SaveInstalledFixes(List<FixFirstCombinedEntity> combinedEntitiesList)
        {
            var installedFixes = CombinedEntitiesProvider.GetInstalledFixesFromCombined(combinedEntitiesList);

            var result = SaveInstalledFixes(installedFixes);

            return result;
        }

        /// <summary>
        /// Save installed fixes to XML
        /// </summary>
        /// <param name="fixesList">List of installed fix entities</param>
        /// <returns>result, error message</returns>
        public static Result SaveInstalledFixes(List<BaseInstalledFixEntity> fixesList)
        {
            try
            {
                XmlSerializer xmlSerializer = new(typeof(InstalledFixesXml));
                using FileStream fs = new(Consts.InstalledFile, FileMode.Create);
                xmlSerializer.Serialize(fs, new InstalledFixesXml(fixesList));

                return new(ResultEnum.Ok, string.Empty);
            }
            catch (Exception e) when (e is FileNotFoundException || e is DirectoryNotFoundException)
            {
                return new Result(ResultEnum.NotFound, e.Message);
            }
        }
    }
}
