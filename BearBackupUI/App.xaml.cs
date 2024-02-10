using System.IO;
using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using BearBackupUI.Windows;
using BearBackupUI.Services;
using BearBackupUI.Core;
using BearBackupUI.Stores;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace BearBackupUI;

public partial class App : Application
{
    private IHost _host;

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool ShutdownBlockReasonCreate(IntPtr hWnd, [MarshalAs(UnmanagedType.LPWStr)] string reason);

    public T? GetService<T>() where T : class
    {
        return _host.Services.GetService(typeof(T)) as T;
    }

#pragma warning disable CS8618
    public App() { }
#pragma warning restore CS8618

    private async void OnStartup(object sender, StartupEventArgs e)
    {
        var proc = Process.GetCurrentProcess();
        if (Process.GetProcessesByName(proc.ProcessName).Length > 1)
        {
            MessageBox.Show("This application is already running.", "Information",
                MessageBoxButton.OK, MessageBoxImage.Information);
            Environment.Exit(0);
            return;
        }

        var basePath = Path.GetDirectoryName(Assembly.GetEntryAssembly()?.Location) ??
            throw new Exception("Failed to get the executing path of the application.");

        _host = Host.CreateDefaultBuilder(e.Args)
                .ConfigureAppConfiguration(c =>
                {
                    c.SetBasePath(basePath);
                })
                .ConfigureServices(ConfigureServices)
                .Build();

        await _host.StartAsync();
    }

    private void ConfigureServices(HostBuilderContext context, IServiceCollection services)
    {
        services.AddHostedService<ApplicationHostService>();

        services.AddSingleton(_ => new ConfigService(context.HostingEnvironment.ContentRootPath));
        services.AddSingleton<LogService>();
        services.AddSingleton<BackupService>();
        services.AddSingleton<TaskService>();

        services.AddSingleton<DispatchCenter>();

        services.AddSingleton<NotifyIconWindow>();

        services.AddTransient<MainStore>();
        services.AddTransient<MainWindow>();
        services.AddTransient<AddRepoStore>();
        services.AddTransient<AddRepoWindow>();
        services.AddTransient<CreateRepoStore>();
        services.AddTransient<CreateRepoWindow>();
        services.AddTransient<LogStore>();
        services.AddTransient<LogWindow>();
        services.AddTransient<RepoInfoStore>();
        services.AddTransient<RepoInfoWindow>();
        services.AddTransient<RestoreStore>();
        services.AddTransient<RestoreWindow>();
        services.AddTransient<AddRecordStore>();
        services.AddTransient<AddRecordWindow>();
        services.AddTransient<SettingStore>();
        services.AddTransient<SettingWindow>();
        services.AddTransient<TaskStore>();
        services.AddTransient<TaskWindow>();
    }

    private async void OnExit(object sender, ExitEventArgs e)
    {
        if (_host is null) return;

        await _host.StopAsync();
        _host.Dispose();
#pragma warning disable CS8625
        _host = null;
#pragma warning restore CS8625
    }

    private void OnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        Logging.Error(e.Exception.Message);
        MessageBox.Show(e.Exception.Message + "\n\nStack trace:\n" + e.Exception.StackTrace,
            "Error occurred", MessageBoxButton.OK, MessageBoxImage.Error);

        Environment.Exit(-1);
    }

    private async void Application_SessionEnding(object sender, SessionEndingCancelEventArgs e)
    {
        var taskService = _host.Services.GetRequiredService<TaskService>();

        if (taskService.IsRunning || taskService.TaskQueue.Length != 0)
        {
            ShutdownBlockReasonCreate(new IntPtr(0), "Wait for all tasks to complete.");
            e.Cancel = true;

            await Task.Run(() =>
            {
                while (true)
                {
                    Task.Delay(100).Wait();

                    if (!taskService.IsRunning && taskService.TaskQueue.Length == 0)
                    {
                        e.Cancel = false;
                        Environment.Exit(0);
                    }
                }
            });
        }
    }
}