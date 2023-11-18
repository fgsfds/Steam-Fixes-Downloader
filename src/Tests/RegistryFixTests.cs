using Common.DI;
using Common.Entities;
using Common.Entities.Fixes;
using Common.Entities.Fixes.RegistryFix;
using Common.FixTools;
using Common.Helpers;
using Common.Providers;
using Microsoft.Win32;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace Tests
{
    /// <summary>
    /// Tests that use instance data and should be run in a single thread
    /// </summary>
    [Collection("Sync")]
    public sealed class RegistryFixTests : IDisposable
    {
        private const string GameDir = "C:\\games\\test game\\";
        private const string GameExe = "game exe.exe";
        private const string RegKey = "SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\AppCompatFlags\\Layers_test";

        private readonly GameEntity _gameEntity = new(
            1,
            "test game",
            GameDir
            );

        private readonly RegistryFixEntity _fixEntity = new()
        {
            Name = "test fix",
            Version = 1,
            Guid = Guid.Parse("C0650F19-F670-4F8A-8545-70F6C5171FA5"),
            Key = "HKEY_CURRENT_USER\\" + RegKey,
            ValueName = "{installfolder}\\" + GameExe,
            NewValueData = "~ RUNASADMIN"
        };

        #region Test Preparations

        public RegistryFixTests()
        {
            BindingsManager.Reset();

            var container = BindingsManager.Instance;
            container.Options.EnableAutoVerification = false;
            container.Options.ResolveUnregisteredConcreteTypes = true;

            CommonBindings.Load(container);
            ProvidersBindings.Load(container);
        }

        public void Dispose()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) { return; }

            var dir = Directory.GetCurrentDirectory();

            using (var key = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\AppCompatFlags", true))
            {
                if (key is null) { return; }

                key.DeleteSubKey("Layers_test");
            }

            File.Delete(Path.Combine(dir, Consts.ConfigFile));
            File.Delete(Path.Combine(dir, Consts.InstalledFile));
        }

        #endregion Test Preparations

        #region Tests

        [Fact]
        public async Task InstallUninstallFixTest()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) { Assert.Fail(); return; }

            //Preparations
            GameEntity gameEntity = _gameEntity;
            RegistryFixEntity fixEntity = _fixEntity;
            var fixManager = BindingsManager.Instance.GetInstance<FixManager>();

            //Install Fix
            var installedFix = await fixManager.InstallFixAsync(gameEntity, fixEntity, null, true);

            InstalledFixesProvider.SaveInstalledFixes(new List<BaseInstalledFixEntity>() { installedFix });

            if (installedFix is not RegistryInstalledFixEntity installedRegFix)
            {
                Assert.Fail();
                return;
            }

            fixEntity.InstalledFix = installedFix;

            //Check installed.fix hash
            using (var md5 = MD5.Create())
            {
                using (var stream = File.OpenRead(Consts.InstalledFile))
                {
                    var hash = Convert.ToHexString(md5.ComputeHash(stream));

                    Assert.Equal("6DE97926DE75282A6898D3E1B4A6AE01", hash);
                }
            }

            //Check if registry value is created
            var newKey = installedRegFix.Key.Replace("HKEY_CURRENT_USER\\", string.Empty);
            using (var key = Registry.CurrentUser.OpenSubKey(newKey, true))
            {
                Assert.NotNull(key);

                var value = (string?)key.GetValue(installedRegFix.ValueName, null);

                Assert.Equal(fixEntity.NewValueData, value);
            }

            //Uninstall fix
            fixManager.UninstallFix(gameEntity, fixEntity);

            //Check if registry value is removed
            using (var key = Registry.CurrentUser.OpenSubKey(newKey, true))
            {
                Assert.NotNull(key);

                var value = key.GetValue(installedRegFix.ValueName, null);

                Assert.Null(value);
            }
        }

        [Fact]
        public async Task InstallUninstallReplaceFixTest()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) { Assert.Fail(); return; }

            const string OldValue = "OLD VALUE";

            //Preparations
            using (var key = Registry.CurrentUser.CreateSubKey(RegKey))
            {
                if (key is null) { Assert.Fail(); return; }

                key.SetValue(GameDir + GameExe, OldValue);
            }

            GameEntity gameEntity = _gameEntity;
            RegistryFixEntity fixEntity = _fixEntity;
            var fixManager = BindingsManager.Instance.GetInstance<FixManager>();

            //Install Fix
            var installedFix = await fixManager.InstallFixAsync(gameEntity, fixEntity, null, true);

            InstalledFixesProvider.SaveInstalledFixes(new List<BaseInstalledFixEntity>() { installedFix });

            if (installedFix is not RegistryInstalledFixEntity installedRegFix)
            {
                Assert.Fail();
                return;
            }

            fixEntity.InstalledFix = installedFix;

            //Check installed.fix hash
            using (var md5 = MD5.Create())
            {
                using (var stream = File.OpenRead(Consts.InstalledFile))
                {
                    var hash = Convert.ToHexString(md5.ComputeHash(stream));

                    Assert.Equal("DA6D032FA42FC15504537830007B45C4", hash);
                }
            }

            //Check if registry value is set
            var newKey = installedRegFix.Key.Replace("HKEY_CURRENT_USER\\", string.Empty);
            using (var key = Registry.CurrentUser.OpenSubKey(newKey, true))
            {
                Assert.NotNull(key);

                var value = (string?)key.GetValue(installedRegFix.ValueName, null);

                Assert.Equal(fixEntity.NewValueData, value);
            }

            //Uninstall fix
            fixManager.UninstallFix(gameEntity, fixEntity);

            //Check if registry value is reverted
            using (var key = Registry.CurrentUser.OpenSubKey(newKey, true))
            {
                Assert.NotNull(key);

                var value = (string?)key.GetValue(installedRegFix.ValueName, null);

                Assert.Equal(OldValue, value);
            }
        }

        #endregion Tests
    }
}