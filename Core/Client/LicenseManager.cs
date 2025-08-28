using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Security.Cryptography;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Rhino;
using ReerRhinoMCPPlugin.Core.Common;

namespace ReerRhinoMCPPlugin.Core.Client
{
    /// <summary>
    /// Handles license registration and management for the Rhino plugin
    /// </summary>
    public class LicenseManager
    {
        private const string LICENSE_STORAGE_KEY = "license_registration";
        
        private readonly HttpClient httpClient;
        
        public LicenseManager()
        {
            httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(60); // lifespan 60s
        }
        
        /// <summary>
        /// Register a license with the remote server
        /// </summary>
        /// <param name="licenseKey">The license key provided by the user</param>
        /// <param name="userId">User identifier</param>
        /// <returns>License registration result</returns>
        public async Task<LicenseRegistrationResult> RegisterLicenseAsync(string licenseKey, string userId)
        {
            try
            {
                // Generate machine fingerprint
                var machineFingerprint = MachineFingerprinting.GenerateMachineFingerprint();
                
                Logger.Info($"Registering license with server...");
                Logger.Info($"Machine fingerprint: {MachineFingerprinting.GetDisplayFingerprint()}");
                
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
                var response = await httpClient.PostAsync($"{ConnectionSettings.GetHttpServerUrl()}/license/register", content);
                
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
                    RegisteredAt = DateTime.Now
                };
                
                await CrossPlatformStorage.StoreDataAsync(LICENSE_STORAGE_KEY, licenseInfo);
                
                Logger.Success($"License registered successfully!");
                Logger.Info($"License ID: {licenseId}");
                Logger.Info($"Tier: {tier}");
                Logger.Info($"Max concurrent files: {maxConcurrentFiles}");
                
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
                Logger.Error($"License registration failed: {ex.Message}");
                return new LicenseRegistrationResult
                {
                    Success = false,
                    Message = "No valid license found. Please check your license details or get one."
                };
            }
        }
        
        /// <summary>
        /// Validate the stored license with the remote server
        /// This method automatically syncs with server state and clears local cache if server says invalid
        /// </summary>
        /// <returns>License validation result</returns>
        public async Task<LicenseValidationResult> ValidateLicenseAsync()
        {
            try
            {
                var storedLicense = await CrossPlatformStorage.RetrieveDataAsync<StoredLicenseInfo>(LICENSE_STORAGE_KEY);
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
                    Logger.Warning("Machine fingerprint mismatch detected - clearing local license");
                    ClearStoredLicense(); // Clear invalid local cache
                    return new LicenseValidationResult
                    {
                        IsValid = false,
                        Message = "No valid license found. Please check your license details or get one."
                    };
                }
                
                Logger.Debug($"Validating license {storedLicense.LicenseId} with server...");
                
                // Validate with server (this naturally syncs server state)
                var validationRequest = new
                {   
                    license_id = storedLicense.LicenseId,
                    license_key = storedLicense.LicenseKey,
                    machine_fingerprint = currentFingerprint
                };
                
                var jsonContent = JsonConvert.SerializeObject(validationRequest);
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
                
                var response = await httpClient.PostAsync($"{ConnectionSettings.GetHttpServerUrl()}/license/validate", content);
                
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    var errorMessage = $"Server validation failed: {response.StatusCode} - {errorContent}";
                    
                    // If server says license not found or forbidden, clear local cache
                    if (response.StatusCode == System.Net.HttpStatusCode.NotFound || 
                        response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                    {
                        Logger.Warning("Server indicates license is invalid - clearing local cache");
                        ClearStoredLicense();
                    }
                    
                    return new LicenseValidationResult
                    {
                        IsValid = false,
                        Message = errorMessage
                    };
                }
                
                var responseJson = await response.Content.ReadAsStringAsync();
                var validationData = JsonConvert.DeserializeObject<JObject>(responseJson);
                
                var status = validationData["status"]?.ToString();
                var isValid = status == "valid";
                
                // If server says license is invalid, clear local cache to sync state
                if (!isValid)
                {
                    var invalidMessage = validationData["message"]?.ToString() ?? "Invalid license";
                    Logger.Warning($"Server indicates license is invalid: {invalidMessage}");
                    Logger.Info("Clearing local license cache to sync with server state");
                    ClearStoredLicense();
                    
                    return new LicenseValidationResult
                    {
                        IsValid = false,
                        Message = "License is invalid or expired. Please check your license or get one."
                    };
                }
                
                // License is valid - server is the source of truth
                Logger.Debug($"License {storedLicense.LicenseId} validated successfully with server");
                
                return new LicenseValidationResult
                {
                    IsValid = true,
                    LicenseId = storedLicense.LicenseId,
                    UserId = storedLicense.UserId,
                    Tier = storedLicense.Tier,
                    MaxConcurrentFiles = storedLicense.MaxConcurrentFiles,
                    Message = "License is valid"
                };
            }
            catch (Exception ex)
            {
                Logger.Error($"License validation error: {ex.Message}");
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
        public async Task<StoredLicenseInfo> GetStoredLicenseInfoAsync()
        {
            return await CrossPlatformStorage.RetrieveDataAsync<StoredLicenseInfo>(LICENSE_STORAGE_KEY);
        }
        
        
        /// <summary>
        /// Clear stored license information
        /// </summary>
        public void ClearStoredLicense()
        {
            try
            {
                CrossPlatformStorage.DeleteData(LICENSE_STORAGE_KEY);
                Logger.Info("Stored license information cleared");
            }
            catch (Exception ex)
            {
                Logger.Error($"Error clearing stored license: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Get the license status (validates with server each time)
        /// </summary>
        /// <returns>License validation result</returns>
        public async Task<LicenseValidationResult> GetLicenseStatusAsync()
        {
            return await ValidateLicenseAsync();
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
    }
} 