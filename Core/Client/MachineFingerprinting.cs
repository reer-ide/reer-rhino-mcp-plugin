using System;
using System.Security.Cryptography;
using System.Text;
using System.IO;
using System.Diagnostics;
using Rhino;

#if WINDOWS
using System.Management;
using Microsoft.Win32;
#endif

namespace ReerRhinoMCPPlugin.Core.Client
{
    /// <summary>
    /// Handles machine fingerprinting for hardware-bound licensing
    /// </summary>
    public static class MachineFingerprinting
    {
        /// <summary>
        /// Generate a unique machine fingerprint for hardware-bound licensing
        /// </summary>
        /// <returns>SHA-256 hash representing the machine fingerprint</returns>
        public static string GenerateMachineFingerprint()
        {
            try
            {
                var fingerprintData = new StringBuilder();
                
                // System information
                fingerprintData.Append(Environment.MachineName);
                fingerprintData.Append(Environment.OSVersion.Platform);
                fingerprintData.Append(Environment.OSVersion.Version);
                fingerprintData.Append(Environment.ProcessorCount);
                
                // Try to get additional hardware info based on platform
                if (Environment.OSVersion.Platform == PlatformID.Win32NT)
                {
                    fingerprintData.Append(GetWindowsHardwareInfo());
                }
                else if (Environment.OSVersion.Platform == PlatformID.Unix)
                {
                    fingerprintData.Append(GetUnixHardwareInfo());
                }
                
                // Create SHA-256 hash of the fingerprint data
                using (var sha256 = SHA256.Create())
                {
                    var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(fingerprintData.ToString()));
                    return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
                }
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine($"Error generating machine fingerprint: {ex.Message}");
                
                // Fallback to basic system info
                var fallbackData = $"{Environment.MachineName}-{Environment.OSVersion}-{Environment.ProcessorCount}";
                using (var sha256 = SHA256.Create())
                {
                    var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(fallbackData));
                    return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
                }
            }
        }
        
        /// <summary>
        /// Get a shortened version of the machine fingerprint for display purposes
        /// </summary>
        /// <returns>First 16 characters of the machine fingerprint</returns>
        public static string GetDisplayFingerprint()
        {
            var fullFingerprint = GenerateMachineFingerprint();
            return fullFingerprint.Substring(0, Math.Min(16, fullFingerprint.Length)) + "...";
        }
        
        /// <summary>
        /// Validate that the current machine matches the expected fingerprint
        /// </summary>
        /// <param name="expectedFingerprint">The expected machine fingerprint</param>
        /// <returns>True if fingerprints match, false otherwise</returns>
        public static bool ValidateFingerprint(string expectedFingerprint)
        {
            if (string.IsNullOrEmpty(expectedFingerprint))
                return false;
                
            var currentFingerprint = GenerateMachineFingerprint();
            return string.Equals(currentFingerprint, expectedFingerprint, StringComparison.OrdinalIgnoreCase);
        }
        
        /// <summary>
        /// Get Windows-specific hardware information
        /// </summary>
        private static string GetWindowsHardwareInfo()
        {
            var hardwareInfo = new StringBuilder();
            
#if WINDOWS
            try
            {
                // CPU Information
                using (var searcher = new ManagementObjectSearcher("SELECT ProcessorId FROM Win32_Processor"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        hardwareInfo.Append(obj["ProcessorId"]?.ToString() ?? "unknown");
                        break; // Just get the first one
                    }
                }
                
                // Motherboard Information
                using (var searcher = new ManagementObjectSearcher("SELECT SerialNumber FROM Win32_BaseBoard"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        hardwareInfo.Append(obj["SerialNumber"]?.ToString() ?? "unknown");
                        break;
                    }
                }
                
                // System UUID
                using (var searcher = new ManagementObjectSearcher("SELECT UUID FROM Win32_ComputerSystemProduct"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        hardwareInfo.Append(obj["UUID"]?.ToString() ?? "unknown");
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine($"Warning: Could not get Windows hardware info: {ex.Message}");
                hardwareInfo.Append("windows-fallback");
            }
#else
            hardwareInfo.Append("windows-not-available");
#endif
            return hardwareInfo.ToString();
        }
        
        /// <summary>
        /// Get Unix (macOS/Linux) specific hardware information
        /// </summary>
        private static string GetUnixHardwareInfo()
        {
            var hardwareInfo = new StringBuilder();
            
            try
            {
                // Check if it's macOS or Linux
                if (Directory.Exists("/Applications") && Directory.Exists("/System"))
                {
                    // macOS specific hardware info
                    hardwareInfo.Append(GetMacOSHardwareInfo());
                }
                else
                {
                    // Linux specific hardware info
                    hardwareInfo.Append(GetLinuxHardwareInfo());
                }
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine($"Warning: Could not get Unix hardware info: {ex.Message}");
                hardwareInfo.Append("unix-fallback");
            }
            
            return hardwareInfo.ToString();
        }
        
        /// <summary>
        /// Get macOS specific hardware information
        /// </summary>
        private static string GetMacOSHardwareInfo()
        {
            var hardwareInfo = new StringBuilder();
            
            try
            {
                // Get system profiler hardware info
                var commands = new[]
                {
                    ("system_profiler SPHardwareDataType | grep 'Serial Number'", "serial"),
                    ("system_profiler SPHardwareDataType | grep 'Hardware UUID'", "uuid"),
                    ("sysctl -n machdep.cpu.brand_string", "cpu")
                };
                
                foreach (var (command, type) in commands)
                {
                    try
                    {
                        var output = ExecuteCommand("/bin/bash", $"-c \"{command}\"");
                        if (!string.IsNullOrEmpty(output))
                        {
                            hardwareInfo.Append($"{type}:{output.Trim()};");
                        }
                    }
                    catch
                    {
                        hardwareInfo.Append($"{type}:unknown;");
                    }
                }
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine($"Warning: Could not get macOS hardware info: {ex.Message}");
                hardwareInfo.Append("macos-fallback");
            }
            
            return hardwareInfo.ToString();
        }
        
        /// <summary>
        /// Get Linux specific hardware information
        /// </summary>
        private static string GetLinuxHardwareInfo()
        {
            var hardwareInfo = new StringBuilder();
            
            try
            {
                // Try to get hardware info from /proc and /sys
                var files = new[]
                {
                    ("/proc/cpuinfo", "cpu"),
                    ("/sys/class/dmi/id/product_uuid", "uuid"),
                    ("/sys/class/dmi/id/board_serial", "serial")
                };
                
                foreach (var (filePath, type) in files)
                {
                    try
                    {
                        if (File.Exists(filePath))
                        {
                            var content = File.ReadAllText(filePath).Trim();
                            if (!string.IsNullOrEmpty(content))
                            {
                                // For cpuinfo, just take first few lines to avoid too much data
                                if (type == "cpu" && content.Length > 100)
                                {
                                    content = content.Substring(0, 100);
                                }
                                hardwareInfo.Append($"{type}:{content};");
                            }
                        }
                    }
                    catch
                    {
                        hardwareInfo.Append($"{type}:unknown;");
                    }
                }
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine($"Warning: Could not get Linux hardware info: {ex.Message}");
                hardwareInfo.Append("linux-fallback");
            }
            
            return hardwareInfo.ToString();
        }
        
        /// <summary>
        /// Execute a command and return its output
        /// </summary>
        private static string ExecuteCommand(string command, string arguments)
        {
            try
            {
                using (var process = new Process())
                {
                    process.StartInfo.FileName = command;
                    process.StartInfo.Arguments = arguments;
                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.RedirectStandardOutput = true;
                    process.StartInfo.CreateNoWindow = true;
                    
                    process.Start();
                    var output = process.StandardOutput.ReadToEnd();
                    process.WaitForExit(5000); // 5 second timeout
                    
                    return output;
                }
            }
            catch
            {
                return string.Empty;
            }
        }
    }
} 