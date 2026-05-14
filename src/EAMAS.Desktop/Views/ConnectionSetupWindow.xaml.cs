using MongoDB.Driver;
using System.IO;
using System.Text.Json;
using System.Windows;

namespace EAMAS.Desktop.Views
{
    public partial class ConnectionSetupWindow : Window
    {
        public bool Saved { get; private set; }

        private static readonly string ConfigPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "EAMAS", "config.json");

        public ConnectionSetupWindow()
        {
            InitializeComponent();
        }

        private void TestConnection_Click(object sender, RoutedEventArgs e)
        {
            var uri = TxtConnectionString.Text.Trim();
            if (string.IsNullOrWhiteSpace(uri))
            {
                ShowError("Please enter a connection string.");
                return;
            }

            BtnTest.IsEnabled = false;
            BtnTest.Content = "Testing...";
            TxtStatus.Visibility = Visibility.Collapsed;

            Task.Run(() =>
            {
                string result;
                try
                {
                    var settings = MongoClientSettings.FromConnectionString(uri);
                    settings.ServerSelectionTimeout = TimeSpan.FromSeconds(8);
                    var client = new MongoClient(settings);
                    client.GetDatabase("admin")
                          .RunCommand<MongoDB.Bson.BsonDocument>(
                              new MongoDB.Bson.BsonDocument("ping", 1));
                    result = "ok";
                }
                catch (Exception ex)
                {
                    result = ex.Message;
                }

                Dispatcher.Invoke(() =>
                {
                    BtnTest.IsEnabled = true;
                    BtnTest.Content = "Test Connection";

                    if (result == "ok")
                    {
                        TxtStatus.Text = "Connection successful!";
                        TxtStatus.Foreground = System.Windows.Media.Brushes.Green;
                        TxtStatus.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        ShowError($"Connection failed: {result}");
                    }
                });
            });
        }

        private void SaveConnection_Click(object sender, RoutedEventArgs e)
        {
            var uri = TxtConnectionString.Text.Trim();
            var db = TxtDatabaseName.Text.Trim();

            if (string.IsNullOrWhiteSpace(uri))
            {
                ShowError("Please enter a connection string.");
                return;
            }

            if (string.IsNullOrWhiteSpace(db))
                db = "eamas";

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);
                var json = JsonSerializer.Serialize(
                    new { ConnectionString = uri, DatabaseName = db },
                    new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(ConfigPath, json);
                Saved = true;
                Close();
            }
            catch (Exception ex)
            {
                ShowError($"Could not save config: {ex.Message}");
            }
        }

        private void ShowError(string msg)
        {
            TxtStatus.Text = msg;
            TxtStatus.Foreground = FindResource("ErrorBrush") as System.Windows.Media.Brush
                                   ?? System.Windows.Media.Brushes.Red;
            TxtStatus.Visibility = Visibility.Visible;
        }
    }
}
