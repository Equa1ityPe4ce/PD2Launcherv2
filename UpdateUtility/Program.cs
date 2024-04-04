using System.Diagnostics;

namespace UpdateUtility
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("Update Utility Started.");

            // Define the paths for PD2Launcher.exe and TempLauncher.exe
            string currentDirectory = Directory.GetCurrentDirectory();
            string pd2LauncherPath = Path.Combine(currentDirectory, "PD2Launcher.exe");
            string tempLauncherPath = Path.Combine(currentDirectory, "TempLauncher.exe");

            //no arguments are passed and PD2Launcher.exe exists
            if (args.Length == 0 && File.Exists(pd2LauncherPath))
            {
                return;
            }

            //file bucket for launchers
            string defaultLauncherUrl = "https://storage.googleapis.com/storage/v1/b/pd2-launcher-update/o/PD2Launcher.exe?alt=media";
            string betaLauncherUrl = "https://storage.googleapis.com/storage/v1/b/pd2-beta-launcher-update/o/PD2Launcher.exe?alt=media";

            //is a Beta directory?
            bool isBetaDirectory = currentDirectory.Contains("ProjectD2Beta");
            string launcherUrl = isBetaDirectory ? betaLauncherUrl : defaultLauncherUrl;

            //no arguments are passed and PD2Launcher.exe does not exist
            if (args.Length == 0 && !File.Exists(pd2LauncherPath))
            {
                Console.WriteLine("PD2Launcher.exe not found. Downloading...");
                await DownloadFileAsync(launcherUrl, pd2LauncherPath);
                StartLauncher(pd2LauncherPath);
                return; // Exit after attempting to start or download the launcher
            }

            // Wait for PD2Launcher.exe to close if it's running
            await WaitForLauncherToClose();

            // Update PD2Launcher.exe using TempLauncher.exe
            if (File.Exists(tempLauncherPath))
            {
                try
                {
                    Console.WriteLine("Attempting to update the launcher...");
                    File.Move(tempLauncherPath, pd2LauncherPath, overwrite: true);
                    Console.WriteLine("Launcher updated successfully.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Update failed: {ex.Message}");
                    return;
                }
            }

            // Finally, start the updated launcher
            StartLauncher(pd2LauncherPath);
        }

        static async Task DownloadFileAsync(string mediaLink, string path)
        {
            using var httpClient = new HttpClient();
            var response = await httpClient.GetAsync(mediaLink);
            response.EnsureSuccessStatusCode();

            await using var fileStream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
            await response.Content.CopyToAsync(fileStream);

            Console.WriteLine("Download complete.");
        }

        static void StartLauncher(string launcherPath)
        {
            try
            {
                Process.Start(launcherPath);
                Console.WriteLine("Launcher started successfully.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to start PD2Launcher: {ex.Message}");
            }
        }

        static async Task WaitForLauncherToClose()
        {
            while (Process.GetProcessesByName("PD2Launcher").Length > 0)
            {
                Console.WriteLine("Waiting for PD2Launcher to close...");
                await Task.Delay(1000); // Wait for 1 second before checking again
            }
        }
    }
}