using System.Diagnostics;

namespace UpdateUtility
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("Update Utility Started.");

            string currentDirectory = Directory.GetCurrentDirectory();
            // Default launcher URL if no arguments are provided
            string defaultLauncherUrl = "https://storage.googleapis.com/storage/v1/b/pd2-launcher-update/o/PD2Launcher.exe?alt=media";
            string betaLauncherUrl = "https://storage.googleapis.com/storage/v1/b/pd2-beta-launcher-update/o/PD2Launcher.exe?alt=media";

            // Check if the utility is in the Beta
            bool isBetaDirectory = currentDirectory.Contains("ProjectD2Beta");

            // Choose the appropriate URL
            string launcherUrl = isBetaDirectory ? betaLauncherUrl : defaultLauncherUrl;

            // Paths for the PD2Launcher and TempLauncher
            string pd2LauncherPath = args.Length >= 1 ? args[0] : "PD2Launcher.exe";
            string tempLauncherPath = args.Length >= 2 ? args[1] : "TempLauncher.exe";

            if (args.Length == 0 && File.Exists(pd2LauncherPath))
            {
                return;
            }

            if (args.Length == 0 && !File.Exists(pd2LauncherPath))
            {
                await DownloadFileAsync(launcherUrl, pd2LauncherPath);

                try
                {
                    Console.WriteLine("Attempting to start PD2Launcher.exe...");
                    Process.Start(pd2LauncherPath);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to start PD2Launcher: {ex.Message}");
                }

                return; // Exit after attempting to start or download the launcher
            }

            // Check if PD2Launcher.exe is still running
            var pd2LauncherProcesses = Process.GetProcessesByName("PD2Launcher");
            if (pd2LauncherProcesses.Length > 0)
            {
                Console.WriteLine("Waiting for PD2Launcher to close...");
                foreach (var process in pd2LauncherProcesses)
                {
                    try
                    {
                        process.WaitForExit(10000); // Wait up to 10 seconds
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error waiting for PD2Launcher to close: {ex.Message}");
                    }
                }


                // Continue with the update process if PD2Launcher.exe and TempLauncher.exe are specified and exist
                try
                {
                    Console.WriteLine("Attempting to update the launcher...");

                    if (File.Exists(pd2LauncherPath))
                    {
                        File.Delete(pd2LauncherPath);
                        Console.WriteLine("Previous version of PD2Launcher removed.");
                    }

                    File.Move(tempLauncherPath, pd2LauncherPath);
                    Console.WriteLine("Launcher updated successfully.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Update failed: {ex.Message}");
                    return;
                }
            }

            //restart the launcher
            try
            {
                Console.WriteLine("Restarting PD2Launcher...");
                Process.Start(pd2LauncherPath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to restart PD2Launcher: {ex.Message}");
            }
        }

        static async Task DownloadFileAsync(string mediaLink, string path)
        {
            using (var httpClient = new HttpClient())
            {
                var response = await httpClient.GetAsync(mediaLink, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();

                await using (var fileStream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
                await using (var stream = await response.Content.ReadAsStreamAsync())
                {
                    var buffer = new byte[8192];
                    var bytesRead = 0;
                    while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                    {
                        await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead));
                    }
                }
            }
        }
    }
}