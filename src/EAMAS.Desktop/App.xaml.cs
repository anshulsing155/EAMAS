using EAMAS.Core.Data;
using EAMAS.Core.Models;
using EAMAS.Core.Services;
using EAMAS.Desktop.Services;
using EAMAS.Desktop.ViewModels;
using EAMAS.Desktop.Views;
using Microsoft.Extensions.DependencyInjection;
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

            var services = new ServiceCollection();
            ConfigureServices(services);
            Services = services.BuildServiceProvider();

            var initializer = Services.GetRequiredService<DatabaseInitializerService>();
            initializer.Initialize();

            var login = Services.GetRequiredService<LoginWindow>();
            login.Show();
        }

        private static void ConfigureServices(IServiceCollection services)
        {
            // ── MongoDB ──────────────────────────────────────────────────────────
            // Connection string can be overridden via EAMAS_MONGO_URI env variable.
            var connectionString = Environment.GetEnvironmentVariable("mongodb://tech:eYant1234@cluster0-shard-00-00.7fpyy.mongodb.net:27017,cluster0-shard-00-01.7fpyy.mongodb.net:27017,cluster0-shard-00-02.7fpyy.mongodb.net:27017/?ssl=true&authSource=admin&retryWrites=true&w=majority")
                ?? "mongodb://localhost:27017";
            var databaseName = Environment.GetEnvironmentVariable("EAMAS_MONGO_DB")
                ?? "eamas";

            services.AddSingleton(new MongoDbContext(connectionString, databaseName));

            // ── Core services ────────────────────────────────────────────────────
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

            // ── Desktop services ─────────────────────────────────────────────────
            services.AddSingleton<NavigationService>();
            services.AddSingleton<MonitoringBackgroundService>();

            // ── ViewModels ───────────────────────────────────────────────────────
            services.AddTransient<LoginViewModel>();
            services.AddTransient<MainViewModel>();
            services.AddTransient<DashboardViewModel>();
            services.AddTransient<ActivityLogsViewModel>();
            services.AddTransient<ScreenshotsViewModel>();
            services.AddTransient<ReportsViewModel>();
            services.AddTransient<SettingsViewModel>();
            services.AddTransient<EmployeesViewModel>();

            // ── Windows / Views ──────────────────────────────────────────────────
            services.AddTransient<LoginWindow>();
            services.AddTransient<MainWindow>();
            services.AddTransient<Views.DashboardView>();
            services.AddTransient<Views.ActivityLogsView>();
            services.AddTransient<Views.ScreenshotsView>();
            services.AddTransient<Views.ReportsView>();
            services.AddTransient<Views.SettingsView>();
            services.AddTransient<Views.EmployeesView>();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            var monitoring = Services?.GetService<MonitoringBackgroundService>();
            monitoring?.Stop();
            base.OnExit(e);
        }
    }
}
