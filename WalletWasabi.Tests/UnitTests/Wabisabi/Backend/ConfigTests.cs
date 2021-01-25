using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.WabiSabi;
using WalletWasabi.WabiSabi.Backend;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.WabiSabi.Backend
{
	public class ConfigTests
	{
		[Fact]
		public async Task CreatesConfigAsync()
		{
			var workDir = Common.GetWorkDir();
			await IoHelpers.TryDeleteDirectoryAsync(workDir);
			CoordinatorParameters coordinatorParameters = new(workDir);
			using WabiSabiCoordinator coordinator = new(coordinatorParameters);
			await coordinator.StartAsync(CancellationToken.None);

			Assert.True(File.Exists(Path.Combine(workDir, "WabiSabiConfig.json")));

			await coordinator.StopAsync(CancellationToken.None);
		}

		[Fact]
		public async Task LoadsConfigAsync()
		{
			var workDir = Common.GetWorkDir();
			await IoHelpers.TryDeleteDirectoryAsync(workDir);
			CoordinatorParameters coordinatorParameters = new(workDir);

			// Create the config first with default value.
			using WabiSabiCoordinator coordinator = new(coordinatorParameters);
			await coordinator.StartAsync(CancellationToken.None);
			await coordinator.StopAsync(CancellationToken.None);

			// Change the file.
			var configPath = Path.Combine(workDir, "WabiSabiConfig.json");
			WabiSabiConfig configChanger = new(configPath);
			configChanger.LoadOrCreateDefaultFile();
			var newTarget = 729;
			configChanger.ConfirmationTarget = newTarget;
			Assert.NotEqual(newTarget, coordinator.Config.ConfirmationTarget);
			configChanger.ToFile();

			// Assert the new value is loaded and not the default one.
			using WabiSabiCoordinator coordinator2 = new(coordinatorParameters);
			await coordinator2.StartAsync(CancellationToken.None);
			Assert.Equal(newTarget, coordinator2.Config.ConfirmationTarget);
			await coordinator2.StopAsync(CancellationToken.None);
		}

		[Fact]
		public async Task ChecksConfigChangesAsync()
		{
			var workDir = Common.GetWorkDir();
			await IoHelpers.TryDeleteDirectoryAsync(workDir);

			var configChangeMonitoringPeriod = TimeSpan.FromMilliseconds(10);
			var configChangeAwaitDuration = TimeSpan.FromMilliseconds(200);

			CoordinatorParameters coordinatorParameters = new(workDir) { ConfigChangeMonitoringPeriod = configChangeMonitoringPeriod };
			using WabiSabiCoordinator coordinator = new(coordinatorParameters);
			await coordinator.StartAsync(CancellationToken.None);

			var configPath = Path.Combine(workDir, "WabiSabiConfig.json");
			WabiSabiConfig configChanger = new(configPath);
			configChanger.LoadOrCreateDefaultFile();
			var newTarget = 729;
			configChanger.ConfirmationTarget = newTarget;
			Assert.NotEqual(newTarget, coordinator.Config.ConfirmationTarget);
			configChanger.ToFile();

			// Leave time for the config change to be picked up.
			await Task.Delay(configChangeAwaitDuration);
			Assert.Equal(newTarget, coordinator.Config.ConfirmationTarget);

			// Do it one more time.
			newTarget = 372;
			configChanger.ConfirmationTarget = newTarget;
			Assert.NotEqual(newTarget, coordinator.Config.ConfirmationTarget);
			configChanger.ToFile();
			await Task.Delay(configChangeAwaitDuration);
			Assert.Equal(newTarget, coordinator.Config.ConfirmationTarget);

			await coordinator.StopAsync(CancellationToken.None);
		}
	}
}