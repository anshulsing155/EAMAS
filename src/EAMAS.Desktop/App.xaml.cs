using EAMAS.Core.Data;
using EAMAS.Core.Models;
using EAMAS.Core.Services;
using EAMAS.Desktop.Services;
using EAMAS.Desktop.ViewModels;
using EAMAS.Desktop.Views;
using Microsoft.Extensions.DependencyInjection;
using System.IO;
using System.Text.Json;
using System.Windows;

namespace EAMAS.Desktop
{
    public partial class App : System.Windows.Application
    {
        public static IServiceProvider Services { get; private set; } = null!;

        public static User? CurrentUser { get; set; }

        /// <summary>Organisation the current user belongs to. Null for SuperAdmin.</summary>
        public static Organization? CurrentOrganization { get; set; }

        public static string CurrentOrgId =>
            CurrentUser?.OrganizationId ?? "SYSTEM";

        /// <summary>Set to true before Shutdown() so MainWindow doesn't cancel the close event.</summary>
        public static bool IsExiting { get; private set; }

        /// <summary>Token written to MongoDB when a user logs in; cleared on logout/exit.</summary>
        public static string? CurrentSessionToken { get; set; }

        // Single-instance enforcement
        private static System.Threading.Mutex? _instanceMutex;

        private System.Windows.Forms.NotifyIcon? _trayIcon;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // ── Single-instance guard ────────────────────────────────
            _instanceMutex = new System.Threading.Mutex(
                true, "Global\\EAMAS_SingleInstance_3F8A2B1C", out bool isNewInstance);

            if (!isNewInstance)
            {
                System.Windows.MessageBox.Show(
                    "EAMAS is already running on this computer.\n\n" +
                    "Check the system tray (bottom-right of the taskbar).",
                    "EAMAS Already Running",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                _instanceMutex.Dispose();
                Shutdown(0);
                return;
            }

            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            DispatcherUnhandledException += (s, ex) =>
            {
                System.Windows.MessageBox.Show(
                    $"Unexpected error: {ex.Exception.Message}", "EAMAS Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                ex.Handled = true;
            };

            if (!ConfigFileExists() && !HasEnvironmentConnectionString())
            {
                var setup = new ConnectionSetupWindow();
                setup.ShowDialog();
                if (!setup.Saved)
                {
                    Shutdown(0);
                    return;
                }
            }

            var services = new ServiceCollection();
            ConfigureServices(services);
            Services = services.BuildServiceProvider();

            try
            {
                var initializer = Services.GetRequiredService<DatabaseInitializerService>();
                initializer.Initialize();
            }
            catch (Exception ex)
            {
                var msg = ex.InnerException?.Message ?? ex.Message;
                var answer = System.Windows.MessageBox.Show(
                    $"Could not connect to MongoDB:\n\n{msg}\n\n" +
                    "Would you like to reconfigure the database connection?",
                    "Database Connection Failed",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Error);

                if (answer == MessageBoxResult.Yes)
                {
                    DeleteConfigFile();
                    System.Windows.MessageBox.Show(
                        "Configuration cleared. Please restart EAMAS to set up the connection again.",
                        "EAMAS", MessageBoxButton.OK, MessageBoxImage.Information);
                }

                Shutdown(1);
                return;
            }

            SetupTrayIcon();

            var login = Services.GetRequiredService<LoginWindow>();
            login.Show();
        }

        // ── System Tray ──────────────────────────────────────────────────────────

        private void SetupTrayIcon()
        {
            System.Drawing.Icon icon;
            try
            {
                var exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName
                              ?? string.Empty;
                icon = System.Drawing.Icon.ExtractAssociatedIcon(exePath)
                       ?? System.Drawing.SystemIcons.Application;
            }
            catch
            {
                icon = System.Drawing.SystemIcons.Application;
            }

            _trayIcon = new System.Windows.Forms.NotifyIcon
            {
                Text = "EAMAS — Employee Activity Monitor",
                Icon = icon,
                Visible = true
            };

            var menu = new System.Windows.Forms.ContextMenuStrip();
            menu.Items.Add("Show Dashboard", null, (_, _) => ShowMainWindow());
            menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
            menu.Items.Add("Exit EAMAS", null, (_, _) => ExitApp());

            _trayIcon.ContextMenuStrip = menu;
            _trayIcon.DoubleClick += (_, _) => ShowMainWindow();
        }

        private void ShowMainWindow()
        {
            Dispatcher.Invoke(() =>
            {
                var win = Windows.OfType<MainWindow>().FirstOrDefault();
                if (win != null)
                {
                    win.Show();
                    win.WindowState = WindowState.Normal;
                    win.Activate();
                }
                else
                {
                    // Re-create the main window if it was GC'd
                    var mw = Services.GetRequiredService<MainWindow>();
                    mw.Show();
                }
            });
        }

        public static void ExitApp()
        {
            IsExiting = true;
            Current.Dispatcher.Invoke(() =>
            {
                try
                {
                    var monitoring = Services?.GetService<MonitoringBackgroundService>();
                    monitoring?.Stop();
                }
                catch { }

                // Clear the active session in MongoDB before shutting down
                TryCloseCurrentSession();

                (Current as App)?._trayIcon?.Dispose();
                _instanceMutex?.ReleaseMutex();
                _instanceMutex?.Dispose();
                Current.Shutdown();
            });
        }

        /// <summary>Clears the current user's session token in MongoDB if one is held.</summary>
        public static void TryCloseCurrentSession()
        {
            try
            {
                if (CurrentUser != null && CurrentSessionToken != null)
                {
                    Services?.GetService<UserService>()
                             ?.CloseSession(CurrentUser.Id, CurrentSessionToken);
                    CurrentSessionToken = null;
                }
            }
            catch { /* best-effort — don't crash on exit */ }
        }

        // ── DI Configuration ─────────────────────────────────────────────────────

        private static void ConfigureServices(IServiceCollection services)
        {
            var (connectionString, databaseName) = LoadMongoConfig();

            services.AddSingleton(new MongoDbContext(connectionString, databaseName));

            // Core services (singletons – shared state)
            services.AddSingleton<DatabaseInitializerService>();
            services.AddSingleton<OrganizationService>();
            services.AddSingleton<UserService>();
            services.AddSingleton<AppCategorizationService>();
            services.AddSingleton<ActivityMonitorService>();
            services.AddSingleton<ScreenshotService>();
            services.AddSingleton<ReportService>();
            services.AddSingleton<AnalyticsService>();
            services.AddSingleton<AlertService>();
            services.AddSingleton<SettingsService>();

            // Desktop services
            services.AddSingleton<NavigationService>();
            services.AddSingleton<MonitoringBackgroundService>();

            // ViewModels
            services.AddTransient<LoginViewModel>();
            services.AddTransient<MainViewModel>();
            services.AddTransient<DashboardViewModel>();
            services.AddTransient<ActivityLogsViewModel>();
            services.AddTransient<ScreenshotsViewModel>();
            services.AddTransient<ReportsViewModel>();
            services.AddTransient<SettingsViewModel>();
            services.AddTransient<EmployeesViewModel>();
            services.AddTransient<AlertsViewModel>();
            services.AddTransient<OrganizationsViewModel>();

            // Windows / Views
            services.AddTransient<LoginWindow>();
            services.AddTransient<MainWindow>();
            services.AddTransient<DashboardView>();
            services.AddTransient<ActivityLogsView>();
            services.AddTransient<ScreenshotsView>();
            services.AddTransient<ReportsView>();
            services.AddTransient<SettingsView>();
            services.AddTransient<EmployeesView>();
            services.AddTransient<AlertsView>();
            services.AddTransient<OrganizationsView>();
        }

        // ── MongoDB config resolution ─────────────────────────────────────────────

        private static string ConfigFilePath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "EAMAS", "config.json");

        private static bool ConfigFileExists() => File.Exists(ConfigFilePath);

        private static bool HasEnvironmentConnectionString() =>
            !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DATABASE_URL"));

        private static void DeleteConfigFile()
        {
            if (File.Exists(ConfigFilePath)) File.Delete(ConfigFilePath);
        }

        private static (string connectionString, string databaseName) LoadMongoConfig()
        {
            if (File.Exists(ConfigFilePath))
            {
                try
                {
                    var json = File.ReadAllText(ConfigFilePath);
                    var cfg = JsonSerializer.Deserialize<MongoConfig>(json);
                    if (cfg != null && !string.IsNullOrWhiteSpace(cfg.ConnectionString))
                        return (cfg.ConnectionString,
                            string.IsNullOrWhiteSpace(cfg.DatabaseName) ? "eamas" : cfg.DatabaseName);
                }
                catch { }
            }

            var envUri = Environment.GetEnvironmentVariable("DATABASE_URL");
            if (!string.IsNullOrWhiteSpace(envUri)) return (envUri, "eamas");

            return ("mongodb://127.0.0.1:27017", "eamas");
        }

        protected override void OnExit(ExitEventArgs e)
        {
            TryCloseCurrentSession();
            _trayIcon?.Dispose();
            Services?.GetService<MonitoringBackgroundService>()?.Stop();
            _instanceMutex?.ReleaseMutex();
            _instanceMutex?.Dispose();
            base.OnExit(e);
        }

        private sealed class MongoConfig
        {
            public string ConnectionString { get; set; } = string.Empty;
            public string DatabaseName { get; set; } = string.Empty;
        }
    }
}
