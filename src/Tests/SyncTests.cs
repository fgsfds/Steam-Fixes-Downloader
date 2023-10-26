using Common.DI;
using Common.Entities;
using Common.FixTools;
using Common.Helpers;
using Common.Providers;
using System.IO.Compression;
using System.Security.Cryptography;

namespace Tests
{
    /// <summary>
    /// Tests that use instance data and should be run in a single thread
    /// </summary>
    [TestClass]
    public sealed class SyncTests
    {
        private const string TestTempFolder = "test_temp";
        private static readonly object LockObject = new();
        private string _currentDirectory;

        #region Test Preparations

        [TestInitialize]
        public void Init()
        {
            Monitor.Enter(LockObject);
            BindingsManager.Reset();

            _currentDirectory = Directory.GetCurrentDirectory();

            var container = BindingsManager.Instance;
            container.Options.EnableAutoVerification = false;
            container.Options.ResolveUnregisteredConcreteTypes = true;

            CommonBindings.Load(container);
            ProvidersBindings.Load(container);

            if (Directory.Exists(TestTempFolder))
            {
                Directory.Delete(TestTempFolder, true);
            }

            Directory.CreateDirectory(TestTempFolder);

            Directory.SetCurrentDirectory(Path.Combine(Directory.GetCurrentDirectory(), TestTempFolder));
        }

        [TestCleanup]
        public void Cleanup()
        {
            Directory.SetCurrentDirectory(_currentDirectory);

            Directory.Delete(TestTempFolder, true);

            if (File.Exists("test_fix.zip"))
            {
                File.Delete("test_fix.zip");
            }

            Monitor.Exit(LockObject);
        }

        #endregion Test Preparations

        #region Tests

        [TestMethod]
        public async Task GetFixesFromGithubTest()
        {
            var fixesProvider = BindingsManager.Instance.GetInstance<FixesProvider>();
            var fixes = await fixesProvider.GetNewListAsync();

            //Looking for Alan Wake fixes list
            var result = fixes.Any(x => x.GameId == 108710);
        }

        [TestMethod]
        public async Task InstallUninstallFixTest() => await InstallUninstallFixAsync(variant: null, update: false);

        [TestMethod]
        public async Task InstallUninstallFixVariantTest() => await InstallUninstallFixAsync(variant: "variant2", update: false);

        [TestMethod]
        public async Task UpdateFixTest() => await InstallUninstallFixAsync(variant: null, update: true);

        [TestMethod]
        public async Task InstallCompromisedFixTest()
        {
            GameEntity gameEntity = new(
                1,
                "test game",
                "game folder"
            );

            FixEntity fixEntity = new()
            {
                Name = "test fix",
                Version = 1,
                Guid = Guid.Parse("C0650F19-F670-4F8A-8545-70F6C5171FA5"),
                Url = "https://github.com/fgsfds/SteamFD-Fixes-Repo/raw/master/fixes/bsp_nointro_v1.zip",
                MD5 = "badMD5"
            };

            var fixInstaller = BindingsManager.Instance.GetInstance<FixInstaller>();

            try
            {
                await fixInstaller.InstallFix(gameEntity, fixEntity, null, false);
            }
            catch (HashCheckFailedException)
            {
                //method failed successfully
                return;
            }

            Assert.Fail();
        }

        [TestMethod]
        public async Task InstallFixToANewFolder()
        {
            string gameFolder = PrepareGameFolder();

            File.Copy($"..\\Resources\\test_fix.zip", Path.Combine(Directory.GetCurrentDirectory(), "..\\test_fix.zip"), true);

            GameEntity gameEntity = new(
                1,
                "test game",
                gameFolder
            );

            FixEntity fixEntity = new()
            {
                Name = "test fix",
                Version = 1,
                Guid = Guid.Parse("C0650F19-F670-4F8A-8545-70F6C5171FA5"),
                Url = "test_fix.zip",
                InstallFolder = "new folder"
            };

            var fixInstaller = BindingsManager.Instance.GetInstance<FixInstaller>();

            var installedFix = await fixInstaller.InstallFix(gameEntity, fixEntity, null, true);

            InstalledFixesProvider.SaveInstalledFixes(new List<InstalledFixEntity>() { installedFix });

            var exeExists = File.Exists("game\\new folder\\start game.exe");
            Assert.IsTrue(exeExists);

            fixEntity.InstalledFix = installedFix;

            FixUninstaller.UninstallFix(gameEntity, installedFix, fixEntity);

            var newDirExists = Directory.Exists("game\\new folder");
            Assert.IsFalse(newDirExists);
        }

        #endregion Tests

        #region Private Methods

        private static async Task InstallUninstallFixAsync(string? variant, bool update)
        {
            string fixArchive = variant is null ? "test_fix.zip" : "test_fix_variant.zip";

            string fixArchiveMD5 = variant is null ? "4E9DE15FC40592B26421E05882C2F6F7" : "DA2D7701D2EB5BC9A35FB58B3B04C5B9";

            string gameFolder = PrepareGameFolder();

            File.Copy($"..\\Resources\\{fixArchive}", Path.Combine(Directory.GetCurrentDirectory(), "..\\test_fix.zip"), true);

            GameEntity gameEntity = new(
                1,
                "test game",
                gameFolder
            );

            FixEntity fixEntity = new()
            {
                Name = "test fix",
                Version = 1,
                Guid = Guid.Parse("C0650F19-F670-4F8A-8545-70F6C5171FA5"),
                Url = "test_fix.zip",
                InstallFolder = "install folder",
                FilesToDelete = new() { "install folder\\file to delete.txt", "install folder\\subfolder\\file to delete in subfolder.txt", "file to delete in parent folder.txt" },
                FilesToBackup = new() { "install folder\\file to backup.txt" },
                MD5 = fixArchiveMD5
            };

            var fixInstaller = BindingsManager.Instance.GetInstance<FixInstaller>();

            var installedFix = await fixInstaller.InstallFix(gameEntity, fixEntity, variant, true);

            InstalledFixesProvider.SaveInstalledFixes(new List<InstalledFixEntity>() { installedFix });

            CheckNewFiles();

            fixEntity.InstalledFix = installedFix;

            //modify backed up file
            File.WriteAllText("game\\install folder\\file to backup.txt", "modified");

            if (update)
            {
                fixEntity.InstalledFix = await UpdateFixAsync(gameEntity, fixEntity.InstalledFix);
            }

            FixUninstaller.UninstallFix(gameEntity, fixEntity.InstalledFix, fixEntity);

            CheckOriginalFiles();
        }

        private static async Task<InstalledFixEntity> UpdateFixAsync(GameEntity gameEntity, InstalledFixEntity installedFix)
        {
            File.Copy($"..\\Resources\\test_fix_v2.zip", Path.Combine(Directory.GetCurrentDirectory(), "..\\test_fix_v2.zip"), true);

            FixEntity fixEntity = new()
            {
                Name = "test fix",
                Version = 2,
                Guid = Guid.Parse("C0650F19-F670-4F8A-8545-70F6C5171FA5"),
                Url = "test_fix_v2.zip",
                InstallFolder = "install folder",
                FilesToDelete = new() { "install folder\\file to delete.txt", "install folder\\subfolder\\file to delete in subfolder.txt", "file to delete in parent folder.txt" },
                FilesToBackup = new() { "install folder\\file to backup.txt" },
                InstalledFix = installedFix
            };

            var fixUpdater = BindingsManager.Instance.GetInstance<FixUpdater>();

            var newInstalledFix = await fixUpdater.UpdateFixAsync(gameEntity, fixEntity, null, true);

            InstalledFixesProvider.SaveInstalledFixes(new List<InstalledFixEntity>() { newInstalledFix });

            CheckUpdatedFiles();

            return newInstalledFix;
        }

        private static void CheckOriginalFiles()
        {
            //check original files
            var exeExists = File.Exists("game\\install folder\\start game.exe");
            Assert.IsTrue(exeExists);

            var fi = File.ReadAllTextAsync("game\\install folder\\start game.exe").Result;
            Assert.IsTrue(fi.Equals("original"));

            var fileExists = File.Exists("game\\install folder\\subfolder\\file.txt");
            Assert.IsTrue(fileExists);

            var fi2 = File.ReadAllTextAsync("game\\install folder\\subfolder\\file.txt").Result;
            Assert.IsTrue(fi2.Equals("original"));

            //check deleted files
            var fileToDeleteExists = File.Exists("game\\install folder\\file to delete.txt");
            Assert.IsTrue(fileToDeleteExists);

            var fileToDeleteSubExists = File.Exists("game\\install folder\\subfolder\\file to delete in subfolder.txt");
            Assert.IsTrue(fileToDeleteSubExists);

            var fileToDeleteParentExists = File.Exists("game\\file to delete in parent folder.txt");
            Assert.IsTrue(fileToDeleteParentExists);

            //check backed up files
            var fileToBackupExists = File.Exists("game\\install folder\\file to backup.txt");
            Assert.IsTrue(fileToBackupExists);

            var fi3 = File.ReadAllTextAsync("game\\install folder\\file to backup.txt").Result;
            Assert.IsTrue(fi3.Equals("original"));
        }

        private static void CheckNewFiles()
        {
            //check replaced files
            var exeExists = File.Exists("game\\install folder\\start game.exe");
            Assert.IsTrue(exeExists);

            var fi = File.ReadAllTextAsync("game\\install folder\\start game.exe").Result;
            Assert.IsTrue(fi.Equals("fix_v1"));

            var fileExists = File.Exists("game\\install folder\\subfolder\\file.txt");
            Assert.IsTrue(fileExists);

            var fi2 = File.ReadAllTextAsync("game\\install folder\\subfolder\\file.txt").Result;
            Assert.IsTrue(fi2.Equals("fix_v1"));

            //check deleted files
            var fileToDeleteExists = File.Exists("game\\install folder\\file to delete.txt");
            Assert.IsFalse(fileToDeleteExists);

            var fileToDeleteSubExists = File.Exists("game\\install folder\\subfolder\\file to delete in subfolder.txt");
            Assert.IsFalse(fileToDeleteSubExists);

            var fileToDeleteParentExists = File.Exists("game\\file to delete in parent folder.txt");
            Assert.IsFalse(fileToDeleteParentExists);

            //check backed up files
            var fileToBackupExists = File.Exists("game\\install folder\\file to backup.txt");
            Assert.IsTrue(fileToBackupExists);

            var backedUpFileExists = File.Exists("game\\.sfd\\test_fix\\install folder\\file to backup.txt");
            Assert.IsTrue(backedUpFileExists);

            //check installed.xml
            using (var md5 = MD5.Create())
            {
                using (var stream = File.OpenRead("installed.xml"))
                {
                    var hash = Convert.ToHexString(md5.ComputeHash(stream));

                    Assert.IsTrue(hash.Equals("720FA0310861D613AAD2A4CFBAFCA80A"));
                }
            }
        }

        private static void CheckUpdatedFiles()
        {
            //check replaced files
            var exeExists = File.Exists("game\\install folder\\start game.exe");
            Assert.IsTrue(exeExists);

            var fi = File.ReadAllTextAsync("game\\install folder\\start game.exe").Result;
            Assert.IsTrue(fi.Equals("fix_v2"));

            var fileExists = File.Exists("game\\install folder\\subfolder\\file.txt");
            Assert.IsTrue(fileExists);

            var fi2 = File.ReadAllTextAsync("game\\install folder\\subfolder\\file.txt").Result;
            Assert.IsTrue(fi2.Equals("original"));

            //check deleted files
            var fileToDeleteExists = File.Exists("game\\install folder\\file to delete.txt");
            Assert.IsFalse(fileToDeleteExists);

            var fileToDeleteSubExists = File.Exists("game\\install folder\\subfolder\\file to delete in subfolder.txt");
            Assert.IsFalse(fileToDeleteSubExists);

            var fileToDeleteParentExists = File.Exists("game\\file to delete in parent folder.txt");
            Assert.IsFalse(fileToDeleteParentExists);

            //check backed up files
            var fileToBackupExists = File.Exists("game\\install folder\\file to backup.txt");
            Assert.IsTrue(fileToBackupExists);

            var backedUpFileExists = File.Exists("game\\.sfd\\test_fix\\install folder\\file to backup.txt");
            Assert.IsTrue(backedUpFileExists);

            //check installed.xml
            //using (var md5 = MD5.Create())
            //{
            //    using (var stream = File.OpenRead("installed.xml"))
            //    {
            //        var hash = Convert.ToHexString(md5.ComputeHash(stream));

            //        Assert.IsTrue(hash.Equals("720FA0310861D613AAD2A4CFBAFCA80A"));
            //    }
            //}
        }

        private static string PrepareGameFolder()
        {
            var gameFolder = Path.Combine(Directory.GetCurrentDirectory(), "game");

            ZipFile.ExtractToDirectory("..\\Resources\\test_game.zip", gameFolder);

            return gameFolder;
        }

        #endregion Private Methods
    }
}