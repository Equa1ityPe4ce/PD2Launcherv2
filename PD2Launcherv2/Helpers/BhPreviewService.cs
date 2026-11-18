using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PD2Launcherv2.Models;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace PD2Launcherv2.Helpers
{
    /// <summary>
    /// Runs BHPreviewHost.exe, which loads the installed BH.dll into a simulated game and
    /// reports exactly what BH would display for an item. One host process serves one
    /// filter at one filter level; start a new one when either changes.
    /// </summary>
    public sealed class BhPreviewService : IDisposable
    {
        public const string HostExecutableName = "BHPreviewHost.exe";
        private static readonly TimeSpan StartTimeout = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(30);

        private readonly SemaphoreSlim _lock = new(1, 1);
        private Process? _process;
        private string? _sandbox;
        private int _nextId = 1;

        public string? FilterPath { get; private set; }
        public int? FilterLevel { get; private set; }
        public bool IsRunning => _process is { HasExited: false };

        /// <summary>Looks next to the launcher, then in the install folder.</summary>
        public static string? FindHost(string installDir)
        {
            foreach (var dir in new[] { AppContext.BaseDirectory, installDir })
            {
                var path = Path.Combine(dir, HostExecutableName);
                if (File.Exists(path)) return path;
            }
            return null;
        }

        /// <summary>Starts the host for a filter. Returns null on success, otherwise the reason it failed.</summary>
        public async Task<string?> StartAsync(string installDir, string filterPath, int? filterLevel, BhPreviewWorld world)
        {
            Stop();
            var host = FindHost(installDir);
            if (host == null) return $"{HostExecutableName} was not found next to the launcher.";

            _sandbox = Path.Combine(Path.GetTempPath(), "PD2BHPreview", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_sandbox);
            var statePath = Path.Combine(_sandbox, "state.json");
            await File.WriteAllTextAsync(statePath, world.ToJson().ToString());

            var psi = new ProcessStartInfo(host)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = new UTF8Encoding(false),
                StandardErrorEncoding = new UTF8Encoding(false),
                WorkingDirectory = _sandbox,
            };
            psi.ArgumentList.Add("--install"); psi.ArgumentList.Add(installDir);
            psi.ArgumentList.Add("--sandbox"); psi.ArgumentList.Add(_sandbox);
            psi.ArgumentList.Add("--filter"); psi.ArgumentList.Add(filterPath);
            psi.ArgumentList.Add("--state"); psi.ArgumentList.Add(statePath);
            psi.ArgumentList.Add("--log"); psi.ArgumentList.Add(Path.Combine(_sandbox, "host.log"));
            if (filterLevel.HasValue)
            {
                psi.ArgumentList.Add("--filter-level");
                psi.ArgumentList.Add(filterLevel.Value.ToString());
            }

            try
            {
                _process = Process.Start(psi);
            }
            catch (Exception ex)
            {
                return $"Could not start {HostExecutableName}: {ex.Message}";
            }
            if (_process == null) return $"Could not start {HostExecutableName}.";
            _process.ErrorDataReceived += (_, e) => { if (e.Data != null) Debug.WriteLine($"[BHPreviewHost] {e.Data}"); };
            _process.BeginErrorReadLine();
            _process.StandardInput.AutoFlush = true;

            var readyLine = await ReadLineAsync(StartTimeout);
            if (readyLine == null) { Stop(); return "The BH preview did not start in time."; }
            var ready = JObject.Parse(readyLine);
            if (ready.Value<bool?>("ready") != true)
            {
                var error = ready.Value<string>("error") ?? "unknown error";
                Stop();
                return $"The BH preview could not start: {error}";
            }
            FilterPath = filterPath;
            FilterLevel = filterLevel;
            return null;
        }

        public async Task<BhPreviewResponse> EvaluateAsync(IEnumerable<BhPreviewItemSpec> items, BhPreviewWorld? world = null)
        {
            var request = new JObject
            {
                ["op"] = "evaluate",
                ["items"] = JArray.FromObject(items),
            };
            if (world != null) request["state"] = world.ToJson();
            var response = await SendAsync(request);
            return response.ToObject<BhPreviewResponse>() ?? new BhPreviewResponse { Error = "empty response" };
        }

        public async Task<BhCatalog> CatalogAsync()
        {
            var response = await SendAsync(new JObject { ["op"] = "catalog" });
            return response.ToObject<BhCatalog>() ?? new BhCatalog();
        }

        private async Task<JObject> SendAsync(JObject request)
        {
            await _lock.WaitAsync();
            try
            {
                if (!IsRunning) return new JObject { ["error"] = "The BH preview is not running." };
                int id = _nextId++;
                request["id"] = id;
                await _process!.StandardInput.WriteLineAsync(request.ToString(Formatting.None));
                while (true)
                {
                    var line = await ReadLineAsync(RequestTimeout);
                    if (line == null)
                    {
                        // A read may still be pending on the dead or hung host; start over next time.
                        Stop();
                        return new JObject { ["error"] = "The BH preview stopped responding." };
                    }
                    var response = JObject.Parse(line);
                    if (response.Value<int?>("id") == id) return response;
                }
            }
            finally
            {
                _lock.Release();
            }
        }

        private async Task<string?> ReadLineAsync(TimeSpan timeout)
        {
            if (_process == null) return null;
            var read = _process.StandardOutput.ReadLineAsync();
            var finished = await Task.WhenAny(read, Task.Delay(timeout));
            return finished == read ? await read : null;
        }

        public void Stop()
        {
            if (_process != null)
            {
                try
                {
                    if (!_process.HasExited)
                    {
                        _process.StandardInput.Close(); // the host exits at end of input
                        if (!_process.WaitForExit(2000)) _process.Kill();
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Stopping BHPreviewHost: {ex.Message}");
                }
                _process.Dispose();
                _process = null;
            }
            if (_sandbox != null)
            {
                try { Directory.Delete(_sandbox, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
                _sandbox = null;
            }
            FilterPath = null;
            FilterLevel = null;
        }

        public void Dispose()
        {
            Stop();
            _lock.Dispose();
        }
    }
}
