using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Rhino;
using ReerRhinoMCPPlugin.Core.Common;

#if WINDOWS
using Microsoft.Win32;
#endif

namespace ReerRhinoMCPPlugin.Core.Client
{
    /// <summary>
    /// Cross-platform secure storage utility for license and session data
    /// </summary>
    public static class CrossPlatformStorage
    {
        private const string APPLICATION_NAME = "ReerRhinoMCPPlugin";
        
        /// <summary>
        /// Get the platform-specific storage directory
        /// </summary>
        private static string GetStorageDirectory()
        {
            string baseDir;
            
            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                // Windows: Use AppData\Roaming
                baseDir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            }
            else if (Environment.OSVersion.Platform == PlatformID.Unix)
            {
                // Check if it's macOS or Linux
                if (Directory.Exists("/Applications") && Directory.Exists("/System"))
                {
                    // macOS: Use ~/Library/Application Support
                    baseDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), 
                                         "Library", "Application Support");
                }
                else
                {
                    // Linux: Use ~/.config
                    baseDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), 
                                         ".config");
                }
            }
            else
            {
                // Fallback to user profile
                baseDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            }
            
            var storageDir = Path.Combine(baseDir, APPLICATION_NAME);
            
            // Ensure directory exists
            if (!Directory.Exists(storageDir))
            {
                Directory.CreateDirectory(storageDir);
            }
            
            return storageDir;
        }
        
        /// <summary>
        /// Store data securely with platform-appropriate encryption
        /// </summary>
        /// <param name="key">Storage key</param>
        /// <param name="data">Data to store</param>
        public static async Task StoreDataAsync(string key, object data)
        {
            try
            {
                var json = JsonConvert.SerializeObject(data, Formatting.Indented);
                var encryptedData = EncryptData(json);
                
                var filePath = Path.Combine(GetStorageDirectory(), $"{key}.dat");
                File.WriteAllText(filePath, encryptedData);
                
                // Set file permissions to be user-readable only on Unix systems
                if (Environment.OSVersion.Platform == PlatformID.Unix)
                {
                    try
                    {
                        // Set file permissions to 600 (user read/write only)
                        var fileInfo = new FileInfo(filePath);
                        if (fileInfo.Exists)
                        {
                            // This requires additional platform-specific code for Unix permissions
                            // For now, rely on the OS defaults
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Warning($"Warning: Could not set file permissions: {ex.Message}");
                    }
                }
                
                Logger.Info($"Data stored securely: {key}");
            }
            catch (Exception ex)
            {
                Logger.Error($"Error storing data {key}: {ex.Message}");
                throw;
            }
        }
        
        /// <summary>
        /// Retrieve stored data
        /// </summary>
        /// <typeparam name="T">Type to deserialize to</typeparam>
        /// <param name="key">Storage key</param>
        /// <returns>Stored data or default(T) if not found</returns>
        public static async Task<T> RetrieveDataAsync<T>(string key) where T : class
        {
            try
            {
                var filePath = Path.Combine(GetStorageDirectory(), $"{key}.dat");
                
                if (!File.Exists(filePath))
                {
                    return default(T);
                }
                
                var encryptedData = File.ReadAllText(filePath);
                var decryptedJson = DecryptData(encryptedData);
                
                return JsonConvert.DeserializeObject<T>(decryptedJson);
            }
            catch (Exception ex)
            {
                Logger.Error($"Error retrieving data {key}: {ex.Message}");
                return default(T);
            }
        }
        
        /// <summary>
        /// Delete stored data
        /// </summary>
        /// <param name="key">Storage key</param>
        public static void DeleteData(string key)
        {
            try
            {
                var filePath = Path.Combine(GetStorageDirectory(), $"{key}.dat");
                
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                    Logger.Info($"Data deleted: {key}");
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error deleting data {key}: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Check if data exists for the given key
        /// </summary>
        /// <param name="key">Storage key</param>
        /// <returns>True if data exists</returns>
        public static bool DataExists(string key)
        {
            var filePath = Path.Combine(GetStorageDirectory(), $"{key}.dat");
            return File.Exists(filePath);
        }
        
        /// <summary>
        /// Encrypt data using platform-appropriate method
        /// </summary>
        private static string EncryptData(string plainText)
        {
            try
            {
                if (Environment.OSVersion.Platform == PlatformID.Win32NT)
                {
                    // Windows: Use DPAPI
                    return EncryptWithDPAPI(plainText);
                }
                else
                {
                    // Unix (macOS/Linux): Use AES with machine-specific key
                    return EncryptWithAES(plainText);
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Encryption error: {ex.Message}");
                // Fallback to base64 encoding (not secure, but better than plain text)
                return Convert.ToBase64String(Encoding.UTF8.GetBytes(plainText));
            }
        }
        
        /// <summary>
        /// Decrypt data using platform-appropriate method
        /// </summary>
        private static string DecryptData(string encryptedText)
        {
            try
            {
                if (Environment.OSVersion.Platform == PlatformID.Win32NT)
                {
                    // Windows: Use DPAPI
                    return DecryptWithDPAPI(encryptedText);
                }
                else
                {
                    // Unix (macOS/Linux): Use AES with machine-specific key
                    return DecryptWithAES(encryptedText);
                }
            }
            catch (Exception)
            {
                // Fallback: try base64 decoding
                try
                {
                    return Encoding.UTF8.GetString(Convert.FromBase64String(encryptedText));
                }
                catch
                {
                    throw new InvalidOperationException("Could not decrypt data");
                }
            }
        }
        
#if WINDOWS
        /// <summary>
        /// Encrypt using Windows DPAPI
        /// </summary>
        private static string EncryptWithDPAPI(string plainText)
        {
            var data = Encoding.UTF8.GetBytes(plainText);
            var encryptedData = ProtectedData.Protect(data, null, DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(encryptedData);
        }
        
        /// <summary>
        /// Decrypt using Windows DPAPI
        /// </summary>
        private static string DecryptWithDPAPI(string encryptedText)
        {
            var encryptedData = Convert.FromBase64String(encryptedText);
            var decryptedData = ProtectedData.Unprotect(encryptedData, null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(decryptedData);
        }
#endif
        
        /// <summary>
        /// Encrypt using AES with machine-specific key (for Unix systems)
        /// </summary>
        private static string EncryptWithAES(string plainText)
        {
            using (var aes = Aes.Create())
            {
                var key = GetMachineSpecificKey();
                aes.Key = key;
                aes.GenerateIV();
                
                using (var encryptor = aes.CreateEncryptor())
                using (var msEncrypt = new MemoryStream())
                {
                    // Write IV first
                    msEncrypt.Write(aes.IV, 0, aes.IV.Length);
                    
                    using (var csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
                    using (var swEncrypt = new StreamWriter(csEncrypt))
                    {
                        swEncrypt.Write(plainText);
                    }
                    
                    return Convert.ToBase64String(msEncrypt.ToArray());
                }
            }
        }
        
        /// <summary>
        /// Decrypt using AES with machine-specific key (for Unix systems)
        /// </summary>
        private static string DecryptWithAES(string encryptedText)
        {
            var encryptedBytes = Convert.FromBase64String(encryptedText);
            
            using (var aes = Aes.Create())
            {
                var key = GetMachineSpecificKey();
                aes.Key = key;
                
                // Extract IV from the beginning
                var iv = new byte[aes.BlockSize / 8];
                var cipherText = new byte[encryptedBytes.Length - iv.Length];
                
                Array.Copy(encryptedBytes, 0, iv, 0, iv.Length);
                Array.Copy(encryptedBytes, iv.Length, cipherText, 0, cipherText.Length);
                
                aes.IV = iv;
                
                using (var decryptor = aes.CreateDecryptor())
                using (var msDecrypt = new MemoryStream(cipherText))
                using (var csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read))
                using (var srDecrypt = new StreamReader(csDecrypt))
                {
                    return srDecrypt.ReadToEnd();
                }
            }
        }
        
        /// <summary>
        /// Generate a machine-specific key for AES encryption
        /// </summary>
        private static byte[] GetMachineSpecificKey()
        {
            // Create a machine-specific key based on system properties
            var machineId = $"{Environment.MachineName}-{Environment.OSVersion}-{Environment.ProcessorCount}";
            
            using (var sha256 = SHA256.Create())
            {
                return sha256.ComputeHash(Encoding.UTF8.GetBytes(machineId));
            }
        }
    }
}