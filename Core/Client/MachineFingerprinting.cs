using System;
using System.Management;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32;
using System.IO;
using Rhino;

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
                    try
                    {
                        // CPU Information
                        using (var searcher = new ManagementObjectSearcher("SELECT ProcessorId FROM Win32_Processor"))
                        {
                            foreach (ManagementObject obj in searcher.Get())
                            {
                                fingerprintData.Append(obj["ProcessorId"]?.ToString() ?? "unknown");
                                break; // Just get the first one
                            }
                        }
                        
                        // Motherboard Information
                        using (var searcher = new ManagementObjectSearcher("SELECT SerialNumber FROM Win32_BaseBoard"))
                        {
                            foreach (ManagementObject obj in searcher.Get())
                            {
                                fingerprintData.Append(obj["SerialNumber"]?.ToString() ?? "unknown");
                                break;
                            }
                        }
                        
                        // System UUID
                        using (var searcher = new ManagementObjectSearcher("SELECT UUID FROM Win32_ComputerSystemProduct"))
                        {
                            foreach (ManagementObject obj in searcher.Get())
                            {
                                fingerprintData.Append(obj["UUID"]?.ToString() ?? "unknown");
                                break;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        RhinoApp.WriteLine($"Warning: Could not get detailed hardware info: {ex.Message}");
                        // Continue with basic fingerprint
                    }
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
    }
} 