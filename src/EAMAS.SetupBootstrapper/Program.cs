using System.Diagnostics;
using System.Reflection;
using System.Windows.Forms;

namespace EAMAS.SetupBootstrapper;

internal static class Program
{
    [STAThread]
    private static int Main()
    {
        try
        {
            var tempRoot = Path.Combine(Path.GetTempPath(), "EAMAS-Setup", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempRoot);

            ExtractResource("EAMAS.SetupBootstrapper.Payload.EAMAS-win-x64.zip", Path.Combine(tempRoot, "EAMAS-win-x64.zip"));
            ExtractResource("EAMAS.SetupBootstrapper.install.cmd", Path.Combine(tempRoot, "install.cmd"));
            ExtractResource("EAMAS.SetupBootstrapper.uninstall.cmd", Path.Combine(tempRoot, "uninstall.cmd"));

            var installCmd = Path.Combine(tempRoot, "install.cmd");
            var process = Process.Start(new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c \"{installCmd}\"",
                WorkingDirectory = tempRoot,
                UseShellExecute = false,
                CreateNoWindow = true
            });

            if (process == null)
            {
                ShowError("Failed to start the EAMAS installer.");
                return 1;
            }

            process.WaitForExit();
            if (process.ExitCode != 0)
            {
                ShowError($"EAMAS installation failed with exit code {process.ExitCode}.");
                return process.ExitCode;
            }

            MessageBox.Show(
                "EAMAS was installed successfully.",
                "EAMAS Setup",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);

            return 0;
        }
        catch (Exception ex)
        {
            ShowError($"EAMAS setup failed: {ex.Message}");
            return 1;
        }
    }

    private static void ExtractResource(string resourceName, string outputPath)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName);
        if (stream == null)
            throw new InvalidOperationException($"Missing installer resource: {resourceName}");

        using var file = File.Create(outputPath);
        stream.CopyTo(file);
    }

    private static void ShowError(string message)
    {
        MessageBox.Show(
            message,
            "EAMAS Setup",
            MessageBoxButtons.OK,
            MessageBoxIcon.Error);
    }
}
