using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using BearBackupUI.Windows;
using BearBackupUI.Helpers;
using Wpf.Ui.Markup;
using System.Windows;
using Wpf.Ui.Appearance;

namespace BearBackupUI.Services;

public class ApplicationHostService : IHostedService
{
	private readonly IServiceProvider _serviceProvider;
	private readonly ConfigService _configService;

	public ApplicationHostService(IServiceProvider serviceProvider, ConfigService configService, LogService _)
	{
		_serviceProvider = serviceProvider;
		_configService = configService;
	}

	public async Task StartAsync(CancellationToken cancellationToken)
	{
		await HandleActivationAsync();
	}

	public async Task StopAsync(CancellationToken cancellationToken)
	{
		await Task.CompletedTask;
	}

	private async Task HandleActivationAsync()
	{
		await Task.CompletedTask;

		if (!Application.Current.Windows.OfType<Window>().Any())
		{
			if (_configService.AutoStartup)
				StartupRegister.AddStartup();
			else
				StartupRegister.RemoveStartup();

			var theme = new ThemesDictionary
			{
				Theme = _configService.ThemeType is ApplicationTheme.HighContrast ?  // HighContrast needs a special treatment.
					ApplicationTheme.Light : _configService.ThemeType
			};
			Application.Current.Resources.MergedDictionaries.Add(theme);

			if (_configService.ThemeType is ApplicationTheme.HighContrast)
				ApplicationThemeManager.Apply(ApplicationTheme.HighContrast);

			var notifyIcon = _serviceProvider.GetRequiredService<NotifyIconWindow>();
			notifyIcon.Show();

			_ = _serviceProvider.GetRequiredService<TaskService>();
		}
	}
}
