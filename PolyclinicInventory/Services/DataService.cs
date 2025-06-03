using PolyclinicInventory.Models;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Win32; // Для проверки Office через реестр

namespace PolyclinicInventory.Services
{
    public class DataService
    {
        private readonly string _logPath = @"C:\Inventory\log.txt"; // Локальный путь
        private readonly Encoding? _cmdEncoding;

        public DataService()
        {
            try
            {
                Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
                _cmdEncoding = Encoding.GetEncoding(866); // CP866 для русской Windows
            }
            catch (Exception ex)
            {
                Log($"Error initializing CP866 encoding: {ex.Message}");
                _cmdEncoding = Encoding.UTF8; // Запасной вариант
            }
        }

        public Computer CollectData()
        {
            var computer = new Computer
            {
                Name = Environment.MachineName ?? "Unknown",
                LastChecked = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
            };

            Log("Starting data collection...");

            // Версия Windows
            string systemInfo = RunCommand("systeminfo");
            Log("systeminfo output: " + Truncate(systemInfo, 2000));
            computer.WindowsVersion = ParseSystemInfo(systemInfo);

            // Статус и срок активации Windows
            string slmgrOutput = RunCommand("cscript //nologo %windir%\\system32\\slmgr.vbs /xpr");
            Log("slmgr output: " + Truncate(slmgrOutput, 500));
            computer.ActivationStatus = ParseActivationStatus(slmgrOutput);
            computer.LicenseExpiry = ParseLicenseExpiry(slmgrOutput);

            // IP и MAC через ipconfig
            string ipconfigOutput = RunCommand("ipconfig /all");
            Log("ipconfig output: " + Truncate(ipconfigOutput, 2000));
            computer.IPAddress = ParseIPAddress(ipconfigOutput);
            computer.MACAddress = ParseMACAddress(ipconfigOutput);

            // IP и MAC через PowerShell (резервный)
            if (computer.IPAddress == "Unknown" || computer.MACAddress == "Unknown")
            {
                string psOutput = RunPowerShellCommand("Get-NetAdapter | Where-Object {$_.Status -eq 'Up'} | Select-Object Name, MacAddress, @{Name='IPAddress';Expression={(Get-NetIPAddress -InterfaceAlias $_.Name -AddressFamily IPv4).IPAddress}}");
                Log("powershell netadapter output: " + Truncate(psOutput, 1000));
                if (computer.IPAddress == "Unknown")
                    computer.IPAddress = ParsePowerShellIPAddress(psOutput);
                if (computer.MACAddress == "Unknown")
                    computer.MACAddress = ParsePowerShellMACAddress(psOutput);
            }

            // Процессор
            string cpuOutput = RunCommand("wmic cpu get Name");
            Log("wmic cpu output: " + Truncate(cpuOutput, 500));
            computer.Processor = ParseProcessor(cpuOutput);

            // Монитор
            string monitorOutput = RunCommand("wmic path Win32_PnPEntity where \"Service='monitor'\" get Name");
            Log("wmic monitor output: " + Truncate(monitorOutput, 500));
            computer.Monitor = ParseMonitor(monitorOutput);

            // Мышь
            string mouseOutput = RunCommand("wmic path Win32_PointingDevice get Name");
            Log("wmic mouse output: " + Truncate(mouseOutput, 500));
            computer.Mouse = ParseMouse(mouseOutput);

            // Клавиатура
            string keyboardOutput = RunCommand("wmic path Win32_Keyboard get Name");
            Log("wmic keyboard output: " + Truncate(keyboardOutput, 500));
            computer.Keyboard = ParseKeyboard(keyboardOutput);

            // Microsoft Office
            string officeOutput = RunOfficeCommand();
            Log("office output: " + Truncate(officeOutput, 2000));
            computer.OfficeStatus = ParseOfficeStatus(officeOutput);

            // Принтеры
            string printerOutput = RunCommand("wmic printer get Name");
            Log("wmic printer output: " + Truncate(printerOutput, 500));
            computer.Printer = ParsePrinter(printerOutput);

            // Оперативная память
            string memoryOutput = RunCommand("wmic memorychip get Capacity");
            Log("wmic memory output: " + Truncate(memoryOutput, 500));
            computer.Memory = ParseMemory(memoryOutput);

            return computer;
        }

        private string RunCommand(string command)
        {
            try
            {
                Process process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        Arguments = $"/c {command}",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        StandardOutputEncoding = _cmdEncoding ?? Encoding.UTF8,
                        StandardErrorEncoding = _cmdEncoding ?? Encoding.UTF8
                    }
                };
                process.Start();
                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();
                process.WaitForExit();
                if (!string.IsNullOrEmpty(error))
                {
                    Log($"Command error: {error}");
                    return $"Error: {error}";
                }
                return output;
            }
            catch (Exception ex)
            {
                Log($"Error running command '{command}': {ex.Message}");
                return $"Error: {ex.Message}";
            }
        }

        private string RunPowerShellCommand(string script)
        {
            try
            {
                Process process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "powershell.exe",
                        Arguments = $"-Command \"{script}\"",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        StandardOutputEncoding = Encoding.UTF8,
                        StandardErrorEncoding = Encoding.UTF8
                    }
                };
                process.Start();
                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();
                process.WaitForExit();
                if (!string.IsNullOrEmpty(error))
                {
                    Log($"PowerShell error: {error}");
                    return $"Error: {error}";
                }
                return output;
            }
            catch (Exception ex)
            {
                Log($"Error running PowerShell command '{script}': {ex.Message}");
                return $"Error: {ex.Message}";
            }
        }

        private string RunOfficeCommand()
        {
            // Проверяем традиционные версии Office
            string[] officePaths = new[]
            {
                "%ProgramFiles%\\Microsoft Office\\Office16\\ospp.vbs",
                "%ProgramFiles(x86)%\\Microsoft Office\\Office16\\ospp.vbs",
                "%ProgramFiles%\\Microsoft Office\\Office15\\ospp.vbs",
                "%ProgramFiles(x86)%\\Microsoft Office\\Office15\\ospp.vbs",
                "%ProgramFiles%\\Microsoft Office\\root\\Office16\\ospp.vbs",
                "%ProgramFiles(x86)%\\Microsoft Office\\root\\Office16\\ospp.vbs"
            };

            foreach (var path in officePaths)
            {
                string command = $"cscript //nologo \"{path}\" /dstatus 2>nul";
                string output = RunCommand(command);
                if (!output.Contains("ERROR") && !output.Contains("не найдено") && !string.IsNullOrEmpty(output))
                {
                    return output;
                }
            }

            // Проверяем UWP-версии Office через реестр
            try
            {
                using (RegistryKey? key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"))
                {
                    if (key != null)
                    {
                        foreach (string subKeyName in key.GetSubKeyNames())
                        {
                            using (RegistryKey? subKey = key.OpenSubKey(subKeyName))
                            {
                                if (subKey?.GetValue("DisplayName")?.ToString()?.Contains("Microsoft Office") == true ||
                                    subKey?.GetValue("DisplayName")?.ToString()?.Contains("Microsoft 365") == true)
                                {
                                    string displayName = subKey.GetValue("DisplayName")?.ToString() ?? "Microsoft Office UWP";
                                    return $"UWP Version: {displayName}";
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"Error checking Office via registry: {ex.Message}");
            }

            return "Office Not Installed";
        }

        private string ParseSystemInfo(string output)
        {
            // Упрощённый regex для Windows
            Regex regex = new Regex(@"Имя ОС:\s*(Майкрософт Windows [^\r\n]+)(?:\r\n|$)", RegexOptions.IgnoreCase);
            var match = regex.Match(output);
            if (match.Success)
                return match.Groups[1].Value.Trim();
            // Запасной вариант для английского вывода
            regex = new Regex(@"OS Name:\s*(Microsoft Windows [^\r\n]+)(?:\r\n|$)", RegexOptions.IgnoreCase);
            match = regex.Match(output);
            return match.Success ? match.Groups[1].Value.Trim() : "Unknown";
        }

        private string ParseActivationStatus(string output)
        {
            if (Regex.IsMatch(output, @"активирована|is activated|лицензия действительна|activated|permanent|Windows\(R\)", RegexOptions.IgnoreCase))
                return "Activated";
            if (Regex.IsMatch(output, @"режим уведомления|Notification mode|не активирована|not activated", RegexOptions.IgnoreCase))
                return "Not Activated";
            return "Unknown";
        }

        private string ParseLicenseExpiry(string output)
        {
            Regex regex = new Regex(@"(\d{2}\.\d{2}\.\d{4}\s+\d{2}:\d{2}:\d{2})", RegexOptions.IgnoreCase);
            var match = regex.Match(output);
            return match.Success ? match.Groups[1].Value.Trim() : "Permanent";
        }

        private string ParseIPAddress(string output)
        {
            Regex regex = new Regex(@"IPv4-адрес.*?:\s*([\d\.]+)", RegexOptions.IgnoreCase);
            var match = regex.Match(output);
            while (match.Success)
            {
                string ip = match.Groups[1].Value.Trim();
                if (!ip.StartsWith("169.254"))
                    return ip;
                match = match.NextMatch();
            }
            regex = new Regex(@"IPv4 Address.*?:\s*([\d\.]+)", RegexOptions.IgnoreCase);
            match = regex.Match(output);
            while (match.Success)
            {
                string ip = match.Groups[1].Value.Trim();
                if (!ip.StartsWith("169.254"))
                    return ip;
                match = match.NextMatch();
            }
            return "Unknown";
        }

        private string ParseMACAddress(string output)
        {
            Regex regex = new Regex(@"Физический адрес.*?:\s*([\w\-]+)", RegexOptions.IgnoreCase);
            var match = regex.Match(output);
            if (match.Success)
                return match.Groups[1].Value.Trim();
            regex = new Regex(@"Physical Address.*?:\s*([\w\-]+)", RegexOptions.IgnoreCase);
            match = regex.Match(output);
            return match.Success ? match.Groups[1].Value.Trim() : "Unknown";
        }

        private string ParsePowerShellIPAddress(string output)
        {
            Regex regex = new Regex(@"IPAddress\s*:\s*([\d\.]+)");
            var match = regex.Match(output);
            while (match.Success)
            {
                string ip = match.Groups[1].Value.Trim();
                if (!ip.StartsWith("169.254"))
                    return ip;
                match = match.NextMatch();
            }
            return "Unknown";
        }

        private string ParsePowerShellMACAddress(string output)
        {
            Regex regex = new Regex(@"MacAddress\s*:\s*([\w\-]+)");
            var match = regex.Match(output);
            return match.Success ? match.Groups[1].Value.Trim() : "Unknown";
        }

        private string ParseProcessor(string output)
        {
            Regex regex = new Regex(@"Name\s+(.+)", RegexOptions.Multiline);
            var match = regex.Match(output);
            return match.Success ? match.Groups[1].Value.Trim() : "Unknown";
        }

        private string ParseMonitor(string output)
        {
            Regex regex = new Regex(@"Name\s+(.+)", RegexOptions.Multiline);
            var matches = regex.Matches(output);
            if (matches.Count > 0)
            {
                return matches[0].Groups[1].Value.Trim();
            }
            return "Unknown";
        }

        private string ParseMouse(string output)
        {
            Regex regex = new Regex(@"Name\s+(.+)", RegexOptions.Multiline);
            var match = regex.Match(output);
            return match.Success ? match.Groups[1].Value.Trim() : "Unknown";
        }

        private string ParseKeyboard(string output)
        {
            Regex regex = new Regex(@"Name\s+(.+)", RegexOptions.Multiline);
            var match = regex.Match(output);
            return match.Success ? match.Groups[1].Value.Trim() : "Unknown";
        }

        private string ParseOfficeStatus(string output)
        {
            if (output.StartsWith("UWP Version:"))
                return output; // Для UWP-версий Office
            if (Regex.IsMatch(output, @"LICENSE STATUS:.*LICENSED", RegexOptions.IgnoreCase))
                return "Licensed";
            if (Regex.IsMatch(output, @"LICENSE STATUS:.*OOB_GRACE", RegexOptions.IgnoreCase))
                return "Grace Period (Cracked)";
            if (Regex.IsMatch(output, @"LICENSE STATUS:.*NOT LICENSED", RegexOptions.IgnoreCase))
                return "Not Licensed";
            if (string.IsNullOrEmpty(output) || Regex.IsMatch(output, @"ERROR|не найдено|file not found|Office Not Installed", RegexOptions.IgnoreCase))
                return "Office Not Installed";
            return "Unknown";
        }

        private string ParsePrinter(string output)
        {
            Regex regex = new Regex(@"Name\s+(.+)", RegexOptions.Multiline);
            var match = regex.Match(output);
            return match.Success ? match.Groups[1].Value.Trim() : "No Printer";
        }

        private string ParseMemory(string output)
        {
            Regex regex = new Regex(@"Capacity\s+(\d+)", RegexOptions.Multiline);
            var matches = regex.Matches(output);
            long totalMemory = 0;
            foreach (Match match in matches)
            {
                if (long.TryParse(match.Groups[1].Value, out long capacity))
                {
                    totalMemory += capacity;
                }
            }
            return totalMemory > 0 ? $"{totalMemory / (1024 * 1024 * 1024)} GB" : "Unknown";
        }

        private void Log(string message)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_logPath)!);
                File.AppendAllText(_logPath, $"{DateTime.Now}: {message}\n", Encoding.UTF8);
            }
            catch (Exception ex)
            {
                File.AppendAllText(@"C:\Inventory\local_log.txt", $"{DateTime.Now}: Log error: {ex.Message}\n", Encoding.UTF8);
            }
        }

        private string Truncate(string text, int maxLength)
        {
            return text.Length > maxLength ? text.Substring(0, maxLength) + "..." : text;
        }
    }
}