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

        /// <summary>The currently authenticated user for this session.</summary>
        public static User? CurrentUser { get; set; }

        /// <summary>
        /// The organisation the current user belongs to.
        /// Null only for SuperAdmin (OrganizationId = "SYSTEM").
        /// </summary>
        public static Organization? CurrentOrganization { get; set; }

        /// <summary>
        /// Convenience accessor: returns the current effective OrganizationId.
        /// SuperAdmin uses "SYSTEM".
        /// </summary>
        public static string CurrentOrgId =>
            CurrentUser?.OrganizationId ?? "SYSTEM";

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            DispatcherUnhandledException += (s, ex) =>
            {
                System.Windows.MessageBox.Show(
                    $"Unexpected error: {ex.Exception.Message}", "EAMAS Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                ex.Handled = true;
            };

            // Show connection setup only when no file-based or environment-based connection is available.
            if (!ConfigFileExists() && !HasEnvironmentConnectionString())
            {
                var setup = new ConnectionSetupWindow();
                setup.ShowDialog();
                if (!setup.Saved)
                {
                    // User cancelled - cannot continue without a database.
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

            var login = Services.GetRequiredService<LoginWindow>();
            login.Show();
        }

        private static string ConfigFilePath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "EAMAS", "config.json");

        private static bool ConfigFileExists() => File.Exists(ConfigFilePath);

        private static bool HasEnvironmentConnectionString() =>
            !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DATABASE_URL"));

        private static void DeleteConfigFile()
        {
            if (File.Exists(ConfigFilePath))
                File.Delete(ConfigFilePath);
        }

        private static void ConfigureServices(IServiceCollection services)
        {
            // MongoDB connection
            // Priority order:
            //   1. %LocalAppData%\EAMAS\config.json  (preferred - never commit this file)
            //   2. DATABASE_URL environment variable
            //   3. localhost fallback (development only)
            var (connectionString, databaseName) = LoadMongoConfig();

            services.AddSingleton(new MongoDbContext(connectionString, databaseName));

            // Core services
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

            // Windows / Views
            services.AddTransient<LoginWindow>();
            services.AddTransient<MainWindow>();
            services.AddTransient<DashboardView>();
            services.AddTransient<ActivityLogsView>();
            services.AddTransient<ScreenshotsView>();
            services.AddTransient<ReportsView>();
            services.AddTransient<SettingsView>();
            services.AddTransient<EmployeesView>();
        }

        /// <summary>
        /// Loads MongoDB connection settings from (in priority order):
        /// 1. %LocalAppData%\EAMAS\config.json
        /// 2. DATABASE_URL environment variable
        /// 3. localhost default
        /// </summary>
        private static (string connectionString, string databaseName) LoadMongoConfig()
        {
            // 1 - config file
            if (File.Exists(ConfigFilePath))
            {
                try
                {
                    var json = File.ReadAllText(ConfigFilePath);
                    var cfg = JsonSerializer.Deserialize<MongoConfig>(json);
                    if (cfg != null && !string.IsNullOrWhiteSpace(cfg.ConnectionString))
                    {
                        return (
                            cfg.ConnectionString,
                            string.IsNullOrWhiteSpace(cfg.DatabaseName) ? "eamas" : cfg.DatabaseName);
                    }
                }
                catch
                {
                    // Fall through to the next resolution strategy.
                }
            }

            // 2 - environment variable
            var envUri = Environment.GetEnvironmentVariable("DATABASE_URL");
            if (!string.IsNullOrWhiteSpace(envUri))
                return (envUri, "eamas");

            // 3 - localhost fallback (development)
            // Use IPv4 loopback to avoid resolving to IPv6 ::1 when mongod is bound to 127.0.0.1 only.
            return ("mongodb://127.0.0.1:27017", "eamas");
        }

        protected override void OnExit(ExitEventArgs e)
        {
            var monitoring = Services?.GetService<MonitoringBackgroundService>();
            monitoring?.Stop();
            base.OnExit(e);
        }

        private sealed class MongoConfig
        {
            public string ConnectionString { get; set; } = string.Empty;
            public string DatabaseName { get; set; } = string.Empty;
        }
    }
}
