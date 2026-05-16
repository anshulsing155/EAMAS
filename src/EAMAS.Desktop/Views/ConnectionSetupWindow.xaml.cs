using MongoDB.Driver;
using System.Windows;

namespace EAMAS.Desktop.Views
{
    public partial class ConnectionSetupWindow : Window
    {
        public bool Saved { get; private set; }

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
            var db  = TxtDatabaseName.Text.Trim();

            if (string.IsNullOrWhiteSpace(uri))
            {
                ShowError("Please enter a connection string.");
                return;
            }

            if (string.IsNullOrWhiteSpace(db))
                db = "eamas";

            try
            {
                // Save encrypted via DPAPI — no plaintext credentials on disk
                App.SaveConfigEncrypted(uri, db);
                Saved = true;
                DialogResult = true;
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
