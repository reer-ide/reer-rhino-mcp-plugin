using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Security.Cryptography;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Rhino;
using Microsoft.Win32;

namespace ReerRhinoMCPPlugin.Core.Client
{
    /// <summary>
    /// Handles license registration and management for the Rhino plugin
    /// </summary>
    public class LicenseManager
    {
        private const string REGISTRY_KEY = @"SOFTWARE\ReerRhinoMCPPlugin";
        private const string LICENSE_VALUE = "LicenseRegistration";
        
        private readonly HttpClient httpClient;
        
        public LicenseManager()
        {
            httpClient = new HttpClient();
        }
        
        /// <summary>
        /// Register a license with the remote server
        /// </summary>
        /// <param name="licenseKey">The license key provided by the user</param>
        /// <param name="userId">User identifier</param>
        /// <param name="serverUrl">URL of the remote MCP server</param>
        /// <returns>License registration result</returns>
        public async Task<LicenseRegistrationResult> RegisterLicenseAsync(string licenseKey, string userId, string serverUrl)
        {
            try
            {
                // Generate machine fingerprint
                var machineFingerprint = MachineFingerprinting.GenerateMachineFingerprint();
                
                RhinoApp.WriteLine($"Registering license with server...");
                RhinoApp.WriteLine($"Machine fingerprint: {MachineFingerprinting.GetDisplayFingerprint()}");
                
                // Prepare registration request
                var registrationRequest = new
                {
                    license_key = licenseKey,
                    user_id = userId,
                    machine_fingerprint = machineFingerprint
                };
                
                var jsonContent = JsonConvert.SerializeObject(registrationRequest);
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
                
                // Send registration request to server
                var response = await httpClient.PostAsync($"{serverUrl}/license/register", content);
                
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    throw new Exception($"License registration failed: {response.StatusCode} - {errorContent}");
                }
                
                var responseJson = await response.Content.ReadAsStringAsync();
                var registrationData = JsonConvert.DeserializeObject<JObject>(responseJson);
                
                // Extract registration details
                var licenseId = registrationData["license_id"]?.ToString();
                var tier = registrationData["tier"]?.ToString();
                var maxConcurrentFiles = registrationData["max_concurrent_files"]?.ToObject<int>() ?? 3;
                
                // Store license registration locally (encrypted)
                var licenseInfo = new StoredLicenseInfo
                {
                    LicenseId = licenseId,
                    LicenseKey = licenseKey,
                    UserId = userId,
                    MachineFingerprint = machineFingerprint,
                    Tier = tier,
                    MaxConcurrentFiles = maxConcurrentFiles,
                    RegisteredAt = DateTime.Now,
                    ServerUrl = serverUrl
                };
                
                await StoreLicenseInfoAsync(licenseInfo);
                
                RhinoApp.WriteLine($"License registered successfully!");
                RhinoApp.WriteLine($"License ID: {licenseId}");
                RhinoApp.WriteLine($"Tier: {tier}");
                RhinoApp.WriteLine($"Max concurrent files: {maxConcurrentFiles}");
                
                return new LicenseRegistrationResult
                {
                    Success = true,
                    LicenseId = licenseId,
                    Tier = tier,
                    MaxConcurrentFiles = maxConcurrentFiles,
                    Message = "License registered successfully"
                };
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine($"License registration failed: {ex.Message}");
                return new LicenseRegistrationResult
                {
                    Success = false,
                    Message = ex.Message
                };
            }
        }
        
        /// <summary>
        /// Validate the stored license with the remote server
        /// </summary>
        /// <returns>License validation result</returns>
        public async Task<LicenseValidationResult> ValidateLicenseAsync()
        {
            try
            {
                var storedLicense = await GetStoredLicenseInfoAsync();
                if (storedLicense == null)
                {
                    return new LicenseValidationResult
                    {
                        IsValid = false,
                        Message = "No license found. Please register a license first."
                    };
                }
                
                // Verify machine fingerprint hasn't changed
                var currentFingerprint = MachineFingerprinting.GenerateMachineFingerprint();
                if (currentFingerprint != storedLicense.MachineFingerprint)
                {
                    return new LicenseValidationResult
                    {
                        IsValid = false,
                        Message = "Machine fingerprint mismatch. License may have been moved to a different machine."
                    };
                }
                
                // Validate with server
                var validationRequest = new
                {
                    license_key = storedLicense.LicenseKey,
                    machine_fingerprint = currentFingerprint
                };
                
                var jsonContent = JsonConvert.SerializeObject(validationRequest);
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
                
                var response = await httpClient.PostAsync($"{storedLicense.ServerUrl}/license/validate", content);
                
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    return new LicenseValidationResult
                    {
                        IsValid = false,
                        Message = $"Server validation failed: {response.StatusCode} - {errorContent}"
                    };
                }
                
                var responseJson = await response.Content.ReadAsStringAsync();
                var validationData = JsonConvert.DeserializeObject<JObject>(responseJson);
                
                var status = validationData["status"]?.ToString();
                var isValid = status == "valid";
                
                return new LicenseValidationResult
                {
                    IsValid = isValid,
                    LicenseId = storedLicense.LicenseId,
                    UserId = storedLicense.UserId,
                    Tier = storedLicense.Tier,
                    MaxConcurrentFiles = storedLicense.MaxConcurrentFiles,
                    Message = isValid ? "License is valid" : validationData["message"]?.ToString() ?? "Invalid license"
                };
            }
            catch (Exception ex)
            {
                return new LicenseValidationResult
                {
                    IsValid = false,
                    Message = $"License validation error: {ex.Message}"
                };
            }
        }
        
        /// <summary>
        /// Get stored license information
        /// </summary>
        /// <returns>Stored license info or null if not found</returns>
        public Task<StoredLicenseInfo> GetStoredLicenseInfoAsync()
        {
            return Task.Run(() =>
            {
                try
                {
                    if (Environment.OSVersion.Platform != PlatformID.Win32NT)
                    {
                        RhinoApp.WriteLine("License storage is only supported on Windows");
                        return null;
                    }

                    using (var key = Registry.CurrentUser.OpenSubKey(REGISTRY_KEY))
                    {
                        if (key == null)
                            return null;
                        
                        var encryptedData = key.GetValue(LICENSE_VALUE) as string;
                        if (string.IsNullOrEmpty(encryptedData))
                            return null;
                        
                        // Decrypt and deserialize
                        var decryptedJson = DecryptString(encryptedData);
                        return JsonConvert.DeserializeObject<StoredLicenseInfo>(decryptedJson);
                    }
                }
                catch (Exception ex)
                {
                    RhinoApp.WriteLine($"Error reading stored license: {ex.Message}");
                    return null;
                }
            });
        }
        
        /// <summary>
        /// Store license information securely in the registry
        /// </summary>
        /// <param name="licenseInfo">License information to store</param>
        private Task StoreLicenseInfoAsync(StoredLicenseInfo licenseInfo)
        {
            return Task.Run(() =>
            {
                try
                {
                    if (Environment.OSVersion.Platform != PlatformID.Win32NT)
                    {
                        throw new PlatformNotSupportedException("License storage is only supported on Windows");
                    }

                    // Serialize and encrypt
                    var json = JsonConvert.SerializeObject(licenseInfo);
                    var encryptedData = EncryptString(json);
                    
                    // Store in registry
                    using (var key = Registry.CurrentUser.CreateSubKey(REGISTRY_KEY))
                    {
                        key.SetValue(LICENSE_VALUE, encryptedData);
                    }
                    
                    RhinoApp.WriteLine("License information stored securely");
                }
                catch (Exception ex)
                {
                    RhinoApp.WriteLine($"Error storing license: {ex.Message}");
                    throw;
                }
            });
        }
        
        /// <summary>
        /// Clear stored license information
        /// </summary>
        public void ClearStoredLicense()
        {
            try
            {
                if (Environment.OSVersion.Platform != PlatformID.Win32NT)
                {
                    RhinoApp.WriteLine("License storage is only supported on Windows");
                    return;
                }

                using (var key = Registry.CurrentUser.OpenSubKey(REGISTRY_KEY, true))
                {
                    if (key != null)
                    {
                        key.DeleteValue(LICENSE_VALUE, false);
                        RhinoApp.WriteLine("Stored license information cleared");
                    }
                }
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine($"Error clearing stored license: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Encrypt a string using DPAPI (Data Protection API)
        /// </summary>
        private string EncryptString(string plainText)
        {
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                throw new PlatformNotSupportedException("Encryption is only supported on Windows");
            }

            var data = Encoding.UTF8.GetBytes(plainText);
            var encryptedData = ProtectedData.Protect(data, null, DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(encryptedData);
        }
        
        /// <summary>
        /// Decrypt a string using DPAPI (Data Protection API)
        /// </summary>
        private string DecryptString(string encryptedText)
        {
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                throw new PlatformNotSupportedException("Decryption is only supported on Windows");
            }

            var encryptedData = Convert.FromBase64String(encryptedText);
            var decryptedData = ProtectedData.Unprotect(encryptedData, null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(decryptedData);
        }
        
        public void Dispose()
        {
            httpClient?.Dispose();
        }
    }
    
    /// <summary>
    /// Result of license registration operation
    /// </summary>
    public class LicenseRegistrationResult
    {
        public bool Success { get; set; }
        public string LicenseId { get; set; }
        public string Tier { get; set; }
        public int MaxConcurrentFiles { get; set; }
        public string Message { get; set; }
    }
    
    /// <summary>
    /// Result of license validation operation
    /// </summary>
    public class LicenseValidationResult
    {
        public bool IsValid { get; set; }
        public string LicenseId { get; set; }
        public string UserId { get; set; }
        public string Tier { get; set; }
        public int MaxConcurrentFiles { get; set; }
        public string Message { get; set; }
    }
    
    /// <summary>
    /// License information stored locally
    /// </summary>
    public class StoredLicenseInfo
    {
        public string LicenseId { get; set; }
        public string LicenseKey { get; set; }
        public string UserId { get; set; }
        public string MachineFingerprint { get; set; }
        public string Tier { get; set; }
        public int MaxConcurrentFiles { get; set; }
        public DateTime RegisteredAt { get; set; }
        public string ServerUrl { get; set; }
    }
} 