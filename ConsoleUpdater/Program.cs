using System.Diagnostics;
using System.IO;
using System.Threading;

namespace UpdateUtility
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length < 2)
            {
                Console.WriteLine("Usage: UpdateUtility <path to main launcher> <path to temporary downloaded file>");
                return;
            }

            string mainLauncherPath = args[0];
            string tempDownloadPath = args[1];

            // Optional: Wait for the launcher to fully exit if necessary
            // This example simply waits a few seconds. You could also check for the process by name.
            Console.WriteLine("Waiting for the main launcher to close...");
            Thread.Sleep(5000); // Wait 5 seconds; adjust as necessary

            // Rename the downloaded update file
            try
            {
                Console.WriteLine("Updating the launcher...");
                if (File.Exists(mainLauncherPath))
                {
                    // Ensure the old executable is deleted
                    File.Delete(mainLauncherPath);
                }

                // Rename the temporary downloaded file to the main launcher's filename
                File.Move(tempDownloadPath, mainLauncherPath);

                Console.WriteLine("Update successful. Restarting the launcher...");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during update: {ex.Message}");
                return;
            }

            // Restart the main launcher
            try
            {
                Process.Start(mainLauncherPath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error restarting the launcher: {ex.Message}");
            }
        }
    }
}