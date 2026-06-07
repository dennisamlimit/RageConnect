using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms; // OpenFileDialog

namespace RageConnect
{
    internal class Program
    {
        private const string AppName = "RageConnect";
        private const string DefaultPort = "22005";

        private const string RageInstallerUrl = "https://cdn.rgsvc.io/public/files/RAGEMultiplayer_Setup.exe";

        private static readonly string AppDataDir =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppName);

        private static readonly string SavedPathFile =
            Path.Combine(AppDataDir, "rage-updater-path.txt");

        private static readonly string CustomPresetsFile =
            Path.Combine(AppDataDir, "custom-presets.txt");

        [STAThread]
        private static void Main()
        {
            Console.OutputEncoding = Encoding.UTF8;
            Console.Title = "RageConnect";

            DrawHeader();

            Status("START", "Starting RageConnect...", ConsoleColor.Cyan);

            string updaterPath = ResolveRageUpdater();

            if (string.IsNullOrWhiteSpace(updaterPath))
            {
                Status("ERROR", "RAGE:MP was not found.", ConsoleColor.Red);
                Pause();
                return;
            }

            Status("OK", "RAGE:MP found: " + updaterPath, ConsoleColor.Green);

            while (true)
            {
                ServerPreset preset = SelectPreset();

                if (preset == null)
                {
                    Status("EXIT", "RageConnect has been closed.", ConsoleColor.DarkGray);
                    return;
                }

                DrawHeader();
                Status("SERVER", preset.Name + " -> " + preset.Address + ":" + preset.Port, ConsoleColor.Cyan);

                Status("REGISTRY", "Applying settings...", ConsoleColor.Yellow);

                if (!WriteRageRegistry(preset.Address, preset.Port))
                {
                    Status("ERROR", "Could not write to the registry.", ConsoleColor.Red);
                    Pause();
                    continue;
                }

                Status("OK", "Registry updated: launch.ip / launch.port / launch2.ip / launch2.port", ConsoleColor.Green);

                Console.WriteLine();
                Console.Write("Start RAGE:MP as administrator? [y/N]: ");
                bool asAdmin = Console.ReadLine().Trim().Equals("y", StringComparison.OrdinalIgnoreCase);

                StartRageUpdater(updaterPath, asAdmin);

                Console.WriteLine();
                Console.Write("Connect again? [Y/n]: ");
                string again = Console.ReadLine();

                if (again.Trim().Equals("n", StringComparison.OrdinalIgnoreCase))
                    break;
            }
        }

        private static string ResolveRageUpdater()
        {
            Status("SCAN", "Looking for saved RAGE:MP path...", ConsoleColor.Yellow);

            string savedPath = LoadSavedUpdaterPath();

            if (IsValidUpdater(savedPath))
            {
                Status("OK", "Saved path is valid.", ConsoleColor.Green);
                return savedPath;
            }

            Status("SCAN", "Searching for RAGE:MP in known install locations...", ConsoleColor.Yellow);

            string found = FindRageUpdater();

            if (IsValidUpdater(found))
            {
                SaveUpdaterPath(found);
                return found;
            }

            while (true)
            {
                Console.WriteLine();
                Status("MISSING", "RAGE:MP was not found automatically.", ConsoleColor.Red);
                Console.WriteLine();
                Console.WriteLine("What do you want to do?");
                Console.WriteLine("  [1] Download RAGE:MP installer");
                Console.WriteLine("  [2] Select updater.exe manually");
                Console.WriteLine("  [3] Enter path manually");
                Console.WriteLine("  [0] Exit");
                Console.WriteLine();
                Console.Write("Selection: ");

                string choice = Console.ReadLine();

                if (choice == "1")
                {
                    bool installed = DownloadAndRunRageInstaller();

                    if (installed)
                    {
                        found = FindRageUpdater();

                        if (IsValidUpdater(found))
                        {
                            SaveUpdaterPath(found);
                            return found;
                        }
                    }

                    Status("WARN", "updater.exe was still not found after installation.", ConsoleColor.Yellow);
                    Status("INFO", "Please select the path manually now.", ConsoleColor.Cyan);

                    found = SelectUpdaterWithDialog();

                    if (IsValidUpdater(found))
                    {
                        SaveUpdaterPath(found);
                        return found;
                    }
                }
                else if (choice == "2")
                {
                    found = SelectUpdaterWithDialog();

                    if (IsValidUpdater(found))
                    {
                        SaveUpdaterPath(found);
                        return found;
                    }

                    Status("ERROR", "Invalid path or no updater.exe selected.", ConsoleColor.Red);
                }
                else if (choice == "3")
                {
                    Console.Write("Path to updater.exe: ");
                    found = Console.ReadLine();

                    found = CleanPath(found);

                    if (IsValidUpdater(found))
                    {
                        SaveUpdaterPath(found);
                        return found;
                    }

                    Status("ERROR", "Invalid path. It must point directly to updater.exe.", ConsoleColor.Red);
                }
                else if (choice == "0")
                {
                    return null;
                }
            }
        }

        private static ServerPreset SelectPreset()
        {
            while (true)
            {
                List<ServerPreset> presets = GetPresets();

                DrawHeader();

                Console.WriteLine("Server Presets");
                Console.WriteLine("──────────────────────────────────────────────");
                Console.WriteLine();

                for (int i = 0; i < presets.Count; i++)
                {
                    Console.WriteLine("  [" + (i + 1) + "] " + presets[i].Name.PadRight(14) + presets[i].Address + ":" + presets[i].Port);
                }

                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine("  [A] Add custom server preset");
                Console.WriteLine("  [D] Delete custom preset");
                Console.WriteLine("  [C] Direct Connect");
                Console.WriteLine("  [0] Exit");
                Console.ResetColor();

                Console.WriteLine();
                Console.Write("Selection: ");

                string input = Console.ReadLine();

                if (string.IsNullOrWhiteSpace(input))
                    continue;

                input = input.Trim();

                if (input.Equals("0", StringComparison.OrdinalIgnoreCase))
                    return null;

                if (input.Equals("A", StringComparison.OrdinalIgnoreCase))
                {
                    AddCustomPreset();
                    Pause();
                    continue;
                }

                if (input.Equals("D", StringComparison.OrdinalIgnoreCase))
                {
                    DeleteCustomPreset();
                    Pause();
                    continue;
                }

                if (input.Equals("C", StringComparison.OrdinalIgnoreCase))
                {
                    return ReadCustomPreset();
                }

                int number;

                if (!int.TryParse(input, out number))
                {
                    Status("ERROR", "Please enter a valid selection.", ConsoleColor.Red);
                    Pause();
                    continue;
                }

                if (number >= 1 && number <= presets.Count)
                    return presets[number - 1];

                Status("ERROR", "Invalid selection.", ConsoleColor.Red);
                Pause();
            }
        }

        private static List<ServerPreset> GetPresets()
        {
            List<ServerPreset> presets = new List<ServerPreset>
    {
        new ServerPreset("GrandRP DE01", "de1.gta5grand.com", DefaultPort),
        new ServerPreset("GrandRP DE02", "de2.gta5grand.com", DefaultPort),
        new ServerPreset("GrandRP DE03", "de3.gta5grand.com", DefaultPort),
        new ServerPreset("GrandRP DE04", "de4.gta5grand.com", DefaultPort),

        new ServerPreset("GrandRP EN01", "rage.gta5grand.com", DefaultPort),
        new ServerPreset("GrandRP EN02", "rage2.gta5grand.com", DefaultPort),
        new ServerPreset("GrandRP EN03", "rage3.gta5grand.com", DefaultPort)
    };

            List<ServerPreset> customPresets = LoadCustomPresets();

            foreach (ServerPreset preset in customPresets)
            {
                presets.Add(preset);
            }

            return presets;
        }

        private static ServerPreset ReadCustomPreset()
        {
            Console.WriteLine();
            Console.Write("Server address/IP: ");
            string address = Console.ReadLine();

            while (string.IsNullOrWhiteSpace(address))
            {
                Console.Write("Server address/IP cannot be empty: ");
                address = Console.ReadLine();
            }

            Console.Write("Port [" + DefaultPort + "]: ");
            string port = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(port))
                port = DefaultPort;

            int portNumber;

            while (!int.TryParse(port, out portNumber) || portNumber < 1 || portNumber > 65535)
            {
                Console.Write("Enter a valid port: ");
                port = Console.ReadLine();
            }

            return new ServerPreset("CUSTOM", address.Trim(), port.Trim());
        }

        private static void AddCustomPreset()
        {
            DrawHeader();

            Console.WriteLine("Add custom preset");
            Console.WriteLine("──────────────────────────────────────────────");
            Console.WriteLine();

            Console.Write("Preset name: ");
            string name = Console.ReadLine();

            while (string.IsNullOrWhiteSpace(name) || name.Contains("|"))
            {
                Console.Write("Enter a valid name without the | character: ");
                name = Console.ReadLine();
            }

            Console.Write("Server address/IP: ");
            string address = Console.ReadLine();

            while (string.IsNullOrWhiteSpace(address) || address.Contains("|"))
            {
                Console.Write("Enter a valid server address/IP without the | character: ");
                address = Console.ReadLine();
            }

            Console.Write("Port [" + DefaultPort + "]: ");
            string port = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(port))
                port = DefaultPort;

            int portNumber;

            while (!int.TryParse(port, out portNumber) || portNumber < 1 || portNumber > 65535)
            {
                Console.Write("Enter a valid port: ");
                port = Console.ReadLine();
            }

            ServerPreset preset = new ServerPreset(name.Trim(), address.Trim(), port.Trim());

            SaveCustomPreset(preset);

            Status("OK", "Preset saved: " + preset.Name + " -> " + preset.Address + ":" + preset.Port, ConsoleColor.Green);
        }

        private static void SaveCustomPreset(ServerPreset preset)
        {
            try
            {
                Directory.CreateDirectory(AppDataDir);

                string line = preset.Name + "|" + preset.Address + "|" + preset.Port;

                using (StreamWriter writer = new StreamWriter(CustomPresetsFile, true, Encoding.UTF8))
                {
                    writer.WriteLine(line);
                }
            }
            catch (Exception ex)
            {
                Status("ERROR", "Could not save preset: " + ex.Message, ConsoleColor.Red);
            }
        }

        private static List<ServerPreset> LoadCustomPresets()
        {
            List<ServerPreset> presets = new List<ServerPreset>();

            try
            {
                if (!File.Exists(CustomPresetsFile))
                    return presets;

                string[] lines = File.ReadAllLines(CustomPresetsFile, Encoding.UTF8);

                foreach (string line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    string[] parts = line.Split('|');

                    if (parts.Length != 3)
                        continue;

                    string name = parts[0].Trim();
                    string address = parts[1].Trim();
                    string port = parts[2].Trim();

                    int portNumber;

                    if (string.IsNullOrWhiteSpace(name))
                        continue;

                    if (string.IsNullOrWhiteSpace(address))
                        continue;

                    if (!int.TryParse(port, out portNumber))
                        continue;

                    presets.Add(new ServerPreset(name, address, port));
                }
            }
            catch
            {

            }

            return presets;
        }

        private static void DeleteCustomPreset()
        {
            DrawHeader();

            List<ServerPreset> customPresets = LoadCustomPresets();

            if (customPresets.Count == 0)
            {
                Status("INFO", "There are no custom presets yet.", ConsoleColor.Yellow);
                return;
            }

            Console.WriteLine("Delete custom preset");
            Console.WriteLine("──────────────────────────────────────────────");
            Console.WriteLine();

            for (int i = 0; i < customPresets.Count; i++)
            {
                Console.WriteLine("  [" + (i + 1) + "] " + customPresets[i].Name.PadRight(14) + customPresets[i].Address + ":" + customPresets[i].Port);
            }

            Console.WriteLine();
            Console.Write("Which preset should be deleted? [0 = Cancel]: ");

            string input = Console.ReadLine();

            int number;

            if (!int.TryParse(input, out number))
            {
                Status("ERROR", "Invalid selection.", ConsoleColor.Red);
                return;
            }

            if (number == 0)
            {
                Status("CANCEL", "Deletion cancelled.", ConsoleColor.DarkGray);
                return;
            }

            if (number < 1 || number > customPresets.Count)
            {
                Status("ERROR", "This preset does not exist.", ConsoleColor.Red);
                return;
            }

            ServerPreset removed = customPresets[number - 1];
            customPresets.RemoveAt(number - 1);

            SaveAllCustomPresets(customPresets);

            Status("OK", "Preset deleted: " + removed.Name, ConsoleColor.Green);
        }

        private static void SaveAllCustomPresets(List<ServerPreset> presets)
        {
            try
            {
                Directory.CreateDirectory(AppDataDir);

                using (StreamWriter writer = new StreamWriter(CustomPresetsFile, false, Encoding.UTF8))
                {
                    foreach (ServerPreset preset in presets)
                    {
                        writer.WriteLine(preset.Name + "|" + preset.Address + "|" + preset.Port);
                    }
                }
            }
            catch (Exception ex)
            {
                Status("ERROR", "Could not save presets: " + ex.Message, ConsoleColor.Red);
            }
        }

        private static bool WriteRageRegistry(string ip, string port)
        {
            try
            {
                WriteRageRegistryView(RegistryView.Registry64, ip, port);
                WriteRageRegistryView(RegistryView.Registry32, ip, port);
                return true;
            }
            catch (Exception ex)
            {
                Status("ERROR", ex.Message, ConsoleColor.Red);
                return false;
            }
        }

        private static void WriteRageRegistryView(RegistryView view, string ip, string port)
        {
            using (RegistryKey baseKey = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, view))
            using (RegistryKey key = baseKey.CreateSubKey(@"Software\RAGE-MP"))
            {
                if (key == null)
                    throw new Exception("Could not create registry key.");

                key.SetValue("launch.ip", ip, RegistryValueKind.String);
                key.SetValue("launch.port", port, RegistryValueKind.String);

                key.SetValue("launch2.ip", ip, RegistryValueKind.String);
                key.SetValue("launch2.port", port, RegistryValueKind.String);

                key.Flush();
            }
        }

        private static string FindRageUpdater()
        {
            List<string> possibleDirs = new List<string>();

            string baseDir = AppDomain.CurrentDomain.BaseDirectory;

            possibleDirs.Add(Path.Combine(baseDir, "RAGEMP"));
            possibleDirs.Add(@"C:\RAGEMP");
            possibleDirs.Add(@"C:\RageMP");
            possibleDirs.Add(@"C:\Games\RAGEMP");
            possibleDirs.Add(@"C:\Program Files\RAGE Multiplayer");
            possibleDirs.Add(@"C:\Program Files (x86)\RAGE Multiplayer");

            possibleDirs.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RAGE-MP"));
            possibleDirs.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RAGE Multiplayer"));
            possibleDirs.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RAGEMP"));

            possibleDirs.AddRange(GetInstallDirsFromRegistry());

            foreach (string dir in possibleDirs)
            {
                if (string.IsNullOrWhiteSpace(dir))
                    continue;

                string updaterPath = Path.Combine(dir, "updater.exe");

                Status("CHECK", updaterPath, ConsoleColor.DarkGray);

                if (File.Exists(updaterPath))
                    return updaterPath;
            }

            return null;
        }

        private static List<string> GetInstallDirsFromRegistry()
        {
            List<string> result = new List<string>();

            RegistryHive[] hives =
            {
                RegistryHive.CurrentUser,
                RegistryHive.LocalMachine
            };

            RegistryView[] views =
            {
                RegistryView.Registry64,
                RegistryView.Registry32
            };

            string[] uninstallKeys =
            {
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\RAGE Multiplayer",
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\RAGEMultiplayer"
            };

            foreach (RegistryHive hive in hives)
            {
                foreach (RegistryView view in views)
                {
                    foreach (string keyPath in uninstallKeys)
                    {
                        try
                        {
                            using (RegistryKey baseKey = RegistryKey.OpenBaseKey(hive, view))
                            using (RegistryKey key = baseKey.OpenSubKey(keyPath))
                            {
                                if (key == null)
                                    continue;

                                string installLocation = key.GetValue("InstallLocation") as string;

                                if (!string.IsNullOrWhiteSpace(installLocation))
                                    result.Add(installLocation);

                                string displayIcon = key.GetValue("DisplayIcon") as string;

                                if (!string.IsNullOrWhiteSpace(displayIcon))
                                {
                                    displayIcon = displayIcon.Replace("\"", "");

                                    if (displayIcon.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                                    {
                                        string dir = Path.GetDirectoryName(displayIcon);

                                        if (!string.IsNullOrWhiteSpace(dir))
                                            result.Add(dir);
                                    }
                                }
                            }
                        }
                        catch
                        {
                            
                        }
                    }
                }
            }

            return result;
        }

        private static bool DownloadAndRunRageInstaller()
        {
            try
            {
                Directory.CreateDirectory(AppDataDir);

                string installerPath = Path.Combine(AppDataDir, "RAGEMultiplayer_Setup.exe");

                Status("DOWNLOAD", "Downloading RAGE:MP installer...", ConsoleColor.Cyan);
                DownloadFileWithProgress(RageInstallerUrl, installerPath);

                Status("OK", "Download finished: " + installerPath, ConsoleColor.Green);
                Status("INSTALL", "Starting installer as administrator...", ConsoleColor.Yellow);

                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = installerPath;
                psi.WorkingDirectory = AppDataDir;
                psi.UseShellExecute = true;
                psi.Verb = "runas";

                Process process = Process.Start(psi);

                if (process != null)
                {
                    Status("WAIT", "Waiting for the installer to close...", ConsoleColor.Yellow);
                    process.WaitForExit();
                }

                Status("SCAN", "Searching for RAGE:MP again after installation...", ConsoleColor.Cyan);

                string updater = FindRageUpdater();
                return IsValidUpdater(updater);
            }
            catch (Win32Exception ex)
            {
                if (ex.NativeErrorCode == 1223)
                    Status("CANCEL", "Request was cancelled.", ConsoleColor.Yellow);
                else
                    Status("ERROR", ex.Message, ConsoleColor.Red);

                return false;
            }
            catch (Exception ex)
            {
                Status("ERROR", "Download/installer error: " + ex.Message, ConsoleColor.Red);
                return false;
            }
        }

        private static void DownloadFileWithProgress(string url, string outputPath)
        {
            using (WebClient client = new WebClient())
            {
                client.Headers.Add("User-Agent", "Mozilla/5.0 RageConnect");

                client.DownloadProgressChanged += delegate (object sender, DownloadProgressChangedEventArgs e)
                {
                    DrawProgressBar(e.BytesReceived, e.TotalBytesToReceive);
                };

                Task task = client.DownloadFileTaskAsync(new Uri(url), outputPath);
                task.GetAwaiter().GetResult();

                Console.WriteLine();
            }
        }

        private static void DrawProgressBar(long received, long total)
        {
            int width = 34;

            if (total <= 0)
            {
                Console.Write("\r   Download: " + FormatBytes(received) + " downloaded...");
                return;
            }

            double percent = received / (double)total;
            int filled = (int)(percent * width);

            if (filled < 0)
                filled = 0;

            if (filled > width)
                filled = width;

            string bar = new string('█', filled) + new string('░', width - filled);

            string text =
                "\r   [" + bar + "] " +
                percent.ToString("P0").PadLeft(4) + "  " +
                FormatBytes(received) + " / " + FormatBytes(total) + "   ";

            Console.Write(text);
        }

        private static string FormatBytes(long bytes)
        {
            double value = bytes;
            string[] units = { "B", "KB", "MB", "GB" };
            int unit = 0;

            while (value >= 1024 && unit < units.Length - 1)
            {
                value /= 1024;
                unit++;
            }

            return value.ToString("0.0") + " " + units[unit];
        }

        private static string SelectUpdaterWithDialog()
        {
            try
            {
                using (OpenFileDialog dialog = new OpenFileDialog())
                {
                    dialog.Title = "Select RAGE:MP updater.exe";
                    dialog.Filter = "RAGE:MP updater.exe|updater.exe|EXE files|*.exe|All files|*.*";
                    dialog.CheckFileExists = true;
                    dialog.Multiselect = false;

                    DialogResult result = dialog.ShowDialog();

                    if (result == DialogResult.OK)
                        return dialog.FileName;
                }
            }
            catch
            {
                Status("WARN", "File dialog could not be opened. Please enter the path manually.", ConsoleColor.Yellow);
            }

            return null;
        }

        private static void StartRageUpdater(string updaterPath, bool asAdmin)
        {
            try
            {
                string rageDir = Path.GetDirectoryName(updaterPath);

                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = updaterPath;
                psi.WorkingDirectory = rageDir;
                psi.UseShellExecute = true;

                if (asAdmin)
                    psi.Verb = "runas";

                Status("START", "Starting RAGE:MP...", ConsoleColor.Cyan);
                Process.Start(psi);

                if (asAdmin)
                    Status("OK", "RAGE:MP was started as administrator.", ConsoleColor.Green);
                else
                    Status("OK", "RAGE:MP was started.", ConsoleColor.Green);
            }
            catch (Win32Exception ex)
            {
                if (ex.NativeErrorCode == 740)
                {
                    Status("ADMIN", "RAGE:MP requires administrator privileges...", ConsoleColor.Yellow);

                    try
                    {
                        ProcessStartInfo adminPsi = new ProcessStartInfo();
                        adminPsi.FileName = updaterPath;
                        adminPsi.WorkingDirectory = Path.GetDirectoryName(updaterPath);
                        adminPsi.UseShellExecute = true;
                        adminPsi.Verb = "runas";

                        Process.Start(adminPsi);

                        Status("OK", "RAGE:MP was started as administrator.", ConsoleColor.Green);
                    }
                    catch (Exception adminEx)
                    {
                        Status("ERROR", "Admin start failed: " + adminEx.Message, ConsoleColor.Red);
                    }
                }
                else if (ex.NativeErrorCode == 1223)
                {
                    Status("CANCEL", "Admin-Request was cancelled.", ConsoleColor.Yellow);
                }
                else
                {
                    Status("ERROR", "Startup error: " + ex.Message, ConsoleColor.Red);
                }
            }
            catch (Exception ex)
            {
                Status("ERROR", "Startup error: " + ex.Message, ConsoleColor.Red);
            }
        }

        private static bool IsValidUpdater(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return false;

            path = CleanPath(path);

            if (!File.Exists(path))
                return false;

            return Path.GetFileName(path).Equals("updater.exe", StringComparison.OrdinalIgnoreCase);
        }

        private static string CleanPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return null;

            return path.Trim().Trim('"');
        }

        private static string LoadSavedUpdaterPath()
        {
            try
            {
                if (!File.Exists(SavedPathFile))
                    return null;

                return CleanPath(File.ReadAllText(SavedPathFile));
            }
            catch
            {
                return null;
            }
        }

        private static void SaveUpdaterPath(string path)
        {
            try
            {
                Directory.CreateDirectory(AppDataDir);
                File.WriteAllText(SavedPathFile, CleanPath(path));
                Status("SAVE", "RAGE:MP path saved.", ConsoleColor.DarkGray);
            }
            catch
            {
                Status("WARN", "Path could not be saved.", ConsoleColor.Yellow);
            }
        }

        private static void DrawHeader()
        {
            Console.Clear();

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("╔══════════════════════════════════════════════╗");
            Console.WriteLine("║                  RageConnect                 ║");
            Console.WriteLine("║              RAGE:MP Direct Connect          ║");
            Console.WriteLine("╚══════════════════════════════════════════════╝");
            Console.ResetColor();

            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine("Preset Launcher • RAGE:MP Finder • Auto Connect");
            Console.WriteLine("────────────────────────────────────────────────");
            Console.ResetColor();

            Console.WriteLine();
        }

        private static void Status(string tag, string message, ConsoleColor color)
        {
            Console.ForegroundColor = color;
            Console.Write("[" + DateTime.Now.ToString("HH:mm:ss") + "] ");
            Console.Write("[" + tag.PadRight(8) + "] ");
            Console.ResetColor();
            Console.WriteLine(message);
        }

        private static void Pause()
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write("Press any key...");
            Console.ResetColor();
            Console.ReadKey(true);
        }
    }

    internal class ServerPreset
    {
        public string Name { get; private set; }
        public string Address { get; private set; }
        public string Port { get; private set; }

        public ServerPreset(string name, string address, string port)
        {
            Name = name;
            Address = address;
            Port = port;
        }
    }
}
