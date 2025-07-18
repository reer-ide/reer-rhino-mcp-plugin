using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Linq;
using Newtonsoft.Json;
using Rhino;

namespace ReerRhinoMCPPlugin.Core.Client
{
    /// <summary>
    /// Manages file integrity checking and monitoring for linked Rhino files
    /// </summary>
    public class FileIntegrityManager
    {
        private const string LINKED_FILES_STORAGE_KEY = "linked_files";
        
        private readonly Dictionary<string, LinkedFileInfo> linkedFiles;
        private readonly object lockObject = new object();

        public FileIntegrityManager()
        {
            linkedFiles = new Dictionary<string, LinkedFileInfo>();
            _ = Task.Run(LoadLinkedFilesAsync); // Load asynchronously
        }

        /// <summary>
        /// Calculate SHA-256 hash of a file
        /// </summary>
        /// <param name="filePath">Path to the file</param>
        /// <returns>SHA-256 hash or null if file doesn't exist</returns>
        public async Task<string> CalculateFileHashAsync(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    RhinoApp.WriteLine($"File not found for hash calculation: {filePath}");
                    return null;
                }

                using (var sha256 = SHA256.Create())
                using (var stream = File.OpenRead(filePath))
                {
                    var hashBytes = await Task.Run(() => sha256.ComputeHash(stream));
                    return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
                }
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine($"Error calculating file hash for {filePath}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Get file size in bytes
        /// </summary>
        /// <param name="filePath">Path to the file</param>
        /// <returns>File size in bytes or 0 if file doesn't exist</returns>
        public long GetFileSize(string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    var fileInfo = new FileInfo(filePath);
                    return fileInfo.Length;
                }
                return 0;
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine($"Error getting file size for {filePath}: {ex.Message}");
                return 0;
            }
        }

        /// <summary>
        /// Register a file as linked to a session
        /// </summary>
        /// <param name="sessionId">Session ID</param>
        /// <param name="filePath">Path to the file</param>
        /// <param name="fileHash">Current file hash</param>
        public async Task RegisterLinkedFileAsync(string sessionId, string filePath, string fileHash = null)
        {
            try
            {
                // Calculate hash if not provided
                if (string.IsNullOrEmpty(fileHash))
                {
                    fileHash = await CalculateFileHashAsync(filePath);
                }

                var linkedFile = new LinkedFileInfo
                {
                    SessionId = sessionId,
                    FilePath = filePath,
                    FileHash = fileHash,
                    FileSize = GetFileSize(filePath),
                    LastModified = File.Exists(filePath) ? File.GetLastWriteTime(filePath) : DateTime.MinValue,
                    RegisteredAt = DateTime.Now,
                    Status = File.Exists(filePath) ? FileStatus.Available : FileStatus.Missing
                };

                lock (lockObject)
                {
                    linkedFiles[sessionId] = linkedFile;
                }

                await SaveLinkedFiles();

                RhinoApp.WriteLine($"File registered for session {sessionId}: {Path.GetFileName(filePath)}");
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine($"Error registering linked file: {ex.Message}");
            }
        }

        /// <summary>
        /// Validate all linked files and return status changes
        /// </summary>
        /// <returns>List of file status changes</returns>
        public async Task<List<FileStatusChange>> ValidateLinkedFilesAsync()
        {
            var statusChanges = new List<FileStatusChange>();

            lock (lockObject)
            {
                foreach (var kvp in linkedFiles.ToList())
                {
                    var sessionId = kvp.Key;
                    var linkedFile = kvp.Value;
                    var currentStatus = linkedFile.Status;
                    var newStatus = GetCurrentFileStatus(linkedFile.FilePath);

                    // Check if file exists and hasn't been moved
                    if (newStatus == FileStatus.Available)
                    {
                        // Check if file has been modified
                        var lastModified = File.GetLastWriteTime(linkedFile.FilePath);
                        var currentSize = GetFileSize(linkedFile.FilePath);

                        if (lastModified != linkedFile.LastModified || currentSize != linkedFile.FileSize)
                        {
                            newStatus = FileStatus.Modified;
                        }
                    }

                    // Update status if changed
                    if (newStatus != currentStatus)
                    {
                        linkedFile.Status = newStatus;
                        linkedFile.LastChecked = DateTime.Now;

                        statusChanges.Add(new FileStatusChange
                        {
                            SessionId = sessionId,
                            FilePath = linkedFile.FilePath,
                            OldStatus = currentStatus,
                            NewStatus = newStatus,
                            Message = GetStatusChangeMessage(currentStatus, newStatus, linkedFile.FilePath)
                        });

                        RhinoApp.WriteLine($"File status changed for session {sessionId}: {currentStatus} -> {newStatus}");
                    }
                }
            }

            if (statusChanges.Any())
            {
                await SaveLinkedFiles();
            }

            return statusChanges;
        }

        /// <summary>
        /// Check if a specific file is still valid for reconnection
        /// </summary>
        /// <param name="sessionId">Session ID</param>
        /// <param name="expectedFilePath">Expected file path</param>
        /// <param name="expectedHash">Expected file hash</param>
        /// <returns>File validation result</returns>
        public async Task<FileValidationResult> ValidateFileForReconnectionAsync(string sessionId, string expectedFilePath, string expectedHash)
        {
            try
            {
                var result = new FileValidationResult
                {
                    SessionId = sessionId,
                    ExpectedPath = expectedFilePath,
                    ExpectedHash = expectedHash
                };

                // Check if file exists at expected location
                if (!File.Exists(expectedFilePath))
                {
                    result.IsValid = false;
                    result.Issue = FileValidationIssue.FileNotFound;
                    result.Message = $"File not found at expected location: {expectedFilePath}";
                    return result;
                }

                // Calculate current hash
                var currentHash = await CalculateFileHashAsync(expectedFilePath);
                if (string.IsNullOrEmpty(currentHash))
                {
                    result.IsValid = false;
                    result.Issue = FileValidationIssue.HashCalculationFailed;
                    result.Message = "Failed to calculate file hash";
                    return result;
                }

                result.CurrentHash = currentHash;
                result.CurrentPath = expectedFilePath;

                // Compare hashes
                if (!string.Equals(expectedHash, currentHash, StringComparison.OrdinalIgnoreCase))
                {
                    result.IsValid = false;
                    result.Issue = FileValidationIssue.FileModified;
                    result.Message = "File has been modified since last session";
                    return result;
                }

                // File is valid
                result.IsValid = true;
                result.Message = "File validation successful";
                
                return result;
            }
            catch (Exception ex)
            {
                return new FileValidationResult
                {
                    SessionId = sessionId,
                    ExpectedPath = expectedFilePath,
                    IsValid = false,
                    Issue = FileValidationIssue.ValidationError,
                    Message = $"Error during file validation: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Remove a linked file from tracking
        /// </summary>
        /// <param name="sessionId">Session ID</param>
        public async Task UnregisterLinkedFileAsync(string sessionId)
        {
            lock (lockObject)
            {
                if (linkedFiles.ContainsKey(sessionId))
                {
                    var filePath = linkedFiles[sessionId].FilePath;
                    linkedFiles.Remove(sessionId);
                    RhinoApp.WriteLine($"File unregistered for session {sessionId}: {Path.GetFileName(filePath)}");
                }
            }

            await SaveLinkedFiles();
        }

        /// <summary>
        /// Get all linked files for status reporting
        /// </summary>
        /// <returns>List of linked file information</returns>
        public List<LinkedFileInfo> GetAllLinkedFiles()
        {
            lock (lockObject)
            {
                return linkedFiles.Values.ToList();
            }
        }

        /// <summary>
        /// Clear all linked files (for troubleshooting)
        /// </summary>
        public async Task ClearAllLinkedFilesAsync()
        {
            lock (lockObject)
            {
                linkedFiles.Clear();
            }

            await SaveLinkedFiles();
            RhinoApp.WriteLine("All linked files cleared");
        }
        
        /// <summary>
        /// Clean up expired sessions (sessions older than specified hours)
        /// Note: Session expiration is now managed by the remote server (30 days)
        /// This method is kept for manual cleanup only
        /// </summary>
        /// <param name="expiredHours">Number of hours after which a session is considered expired</param>
        /// <returns>Number of sessions cleaned up</returns>
        public async Task<int> CleanupExpiredSessionsAsync(int expiredHours = 720) // 30 days = 720 hours
        {
            var expiredSessions = new List<string>();
            var cutoffTime = DateTime.Now.AddHours(-expiredHours);
            
            lock (lockObject)
            {
                foreach (var kvp in linkedFiles.ToList())
                {
                    var linkedFile = kvp.Value;
                    // Consider a session expired if it's older than the cutoff time
                    if (linkedFile.RegisteredAt < cutoffTime)
                    {
                        expiredSessions.Add(kvp.Key);
                        linkedFiles.Remove(kvp.Key);
                    }
                }
            }
            
            if (expiredSessions.Any())
            {
                await SaveLinkedFiles();
                RhinoApp.WriteLine($"Cleaned up {expiredSessions.Count} expired sessions");
            }
            
            return expiredSessions.Count;
        }

        private FileStatus GetCurrentFileStatus(string filePath)
        {
            return File.Exists(filePath) ? FileStatus.Available : FileStatus.Missing;
        }

        private string GetStatusChangeMessage(FileStatus oldStatus, FileStatus newStatus, string filePath)
        {
            var fileName = Path.GetFileName(filePath);
            switch (newStatus)
            {
                case FileStatus.Missing:
                    return $"File {fileName} is no longer available";
                case FileStatus.Modified:
                    return $"File {fileName} has been modified";
                case FileStatus.Available:
                    return $"File {fileName} is now available";
                default:
                    return $"File {fileName} status changed from {oldStatus} to {newStatus}";
            }
        }

        /// <summary>
        /// Clears all stored session information for a fresh start
        /// </summary>
        /// <returns>Task representing the async operation</returns>
        public async Task ClearAllSessionsAsync()
        {
            try
            {
                lock (lockObject)
                {
                    linkedFiles.Clear();
                }

                await SaveLinkedFiles();
                RhinoApp.WriteLine("All session data cleared");
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine($"Error clearing session data: {ex.Message}");
                throw;
            }
        }

        private async Task SaveLinkedFiles()
        {
            try
            {
                var fileList = linkedFiles.Values.ToList();
                await CrossPlatformStorage.StoreDataAsync(LINKED_FILES_STORAGE_KEY, fileList);
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine($"Error saving linked files: {ex.Message}");
            }
        }

        private async Task LoadLinkedFilesAsync()
        {
            try
            {
                var fileList = await CrossPlatformStorage.RetrieveDataAsync<List<LinkedFileInfo>>(LINKED_FILES_STORAGE_KEY);
                
                if (fileList != null)
                {
                    lock (lockObject)
                    {
                        linkedFiles.Clear();
                        foreach (var file in fileList)
                        {
                            linkedFiles[file.SessionId] = file;
                        }
                    }

                    RhinoApp.WriteLine($"Loaded {fileList.Count} linked files from storage");
                }
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine($"Error loading linked files: {ex.Message}");
            }
        }

    }

    /// <summary>
    /// Information about a linked file
    /// </summary>
    public class LinkedFileInfo
    {
        public string SessionId { get; set; }
        public string FilePath { get; set; }
        public string FileHash { get; set; }
        public long FileSize { get; set; }
        public DateTime LastModified { get; set; }
        public DateTime RegisteredAt { get; set; }
        public DateTime LastChecked { get; set; }
        public FileStatus Status { get; set; }
    }

    /// <summary>
    /// File status enumeration
    /// </summary>
    public enum FileStatus
    {
        Available,
        Missing,
        Modified,
        Moved
    }

    /// <summary>
    /// File status change notification
    /// </summary>
    public class FileStatusChange
    {
        public string SessionId { get; set; }
        public string FilePath { get; set; }
        public FileStatus OldStatus { get; set; }
        public FileStatus NewStatus { get; set; }
        public string Message { get; set; }
    }

    /// <summary>
    /// File validation result
    /// </summary>
    public class FileValidationResult
    {
        public string SessionId { get; set; }
        public string ExpectedPath { get; set; }
        public string CurrentPath { get; set; }
        public string ExpectedHash { get; set; }
        public string CurrentHash { get; set; }
        public bool IsValid { get; set; }
        public FileValidationIssue Issue { get; set; }
        public string Message { get; set; }
    }

    /// <summary>
    /// File validation issues
    /// </summary>
    public enum FileValidationIssue
    {
        None,
        FileNotFound,
        FileModified,
        HashCalculationFailed,
        ValidationError
    }
} 