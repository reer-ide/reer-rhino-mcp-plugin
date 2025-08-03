using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Linq;
using Newtonsoft.Json;
using Rhino;
using ReerRhinoMCPPlugin.Core.Common;

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
                Logger.Error($"Error getting file size for {filePath}: {ex.Message}");
                return 0;
            }
        }

        /// <summary>
        /// Register a file as linked to a session
        /// </summary>
        /// <param name="sessionId">Session ID</param>
        /// <param name="filePath">Path to the file</param>
        /// <param name="documentGUID">Document GUID for persistent identification</param>
        public async Task RegisterLinkedFileAsync(string sessionId, string filePath, string documentGUID = null)
        {
            try
            {
                var linkedFile = new LinkedFileInfo
                {
                    SessionId = sessionId,
                    DocumentGUID = documentGUID,
                    FilePath = filePath,
                    OriginalPath = filePath,
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

                Logger.Info($"File registered for session {sessionId}: {Path.GetFileName(filePath)}");
            }
            catch (Exception ex)
            {
                Logger.Error($"Error registering linked file: {ex.Message}");
            }
        }

        /// <summary>
        /// Validate all linked files and return status changes
        /// </summary>
        /// <returns>List of file status changes</returns>
        public async Task<List<FileStatusChange>> CheckLinkedFilesAsync()
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

                        Logger.Info($"File status changed for session {sessionId}: {currentStatus} -> {newStatus}");
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
        /// <returns>File validation result</returns>
        public async Task<FileValidationResult> ValidateFileForReconnectionAsync(string sessionId, string expectedFilePath)
        {
            try
            {
                var result = new FileValidationResult
                {
                    SessionId = sessionId,
                    ExpectedPath = expectedFilePath
                };

                // Get linked file info
                LinkedFileInfo linkedFile = null;
                lock (lockObject)
                {
                    linkedFiles.TryGetValue(sessionId, out linkedFile);
                }

                if (linkedFile == null)
                {
                    result.IsValid = false;
                    result.Issue = FileValidationIssue.SessionNotFound;
                    result.Message = "No linked file information found for this session";
                    return result;
                }

                // First check: File exists at expected location
                if (!File.Exists(expectedFilePath))
                {
                    result.IsValid = false;
                    result.Issue = FileValidationIssue.FileNotFound;
                    result.Message = $"File not found at expected location: {expectedFilePath}";
                    result.LinkedFileInfo = linkedFile;
                    return result;
                }

                // Get current document GUID
                var currentDocumentGuid = DocumentGUIDHelper.GetExistingDocumentGUID();
                
                // Check GUID match
                if (!string.IsNullOrEmpty(linkedFile.DocumentGUID))
                {
                    if (currentDocumentGuid != linkedFile.DocumentGUID)
                    {
                        result.IsValid = false;
                        result.Issue = FileValidationIssue.GUIDMismatch;
                        result.Message = "Document GUID does not match. This might be a different file.";
                        result.LinkedFileInfo = linkedFile;
                        result.CurrentDocumentGUID = currentDocumentGuid;
                        return result;
                    }
                }

                // Check file modifications
                var lastModified = File.GetLastWriteTime(expectedFilePath);
                var currentSize = GetFileSize(expectedFilePath);

                if (lastModified != linkedFile.LastModified || currentSize != linkedFile.FileSize)
                {
                    result.IsValid = true; // Still valid but modified
                    result.Issue = FileValidationIssue.FileModified;
                    result.Message = "File has been modified since last connection";
                    result.LinkedFileInfo = linkedFile;
                    
                    // Update the linked file info
                    linkedFile.LastModified = lastModified;
                    linkedFile.FileSize = currentSize;
                    linkedFile.Status = FileStatus.Modified;
                    await SaveLinkedFiles();
                }
                else
                {
                    result.IsValid = true;
                    result.Message = "File validation successful";
                }

                result.CurrentPath = expectedFilePath;
                result.LinkedFileInfo = linkedFile;
                result.CurrentDocumentGUID = currentDocumentGuid;
                
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
                    Logger.Info($"File unregistered for session {sessionId}: {Path.GetFileName(filePath)}");
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
        /// Find linked file by document GUID
        /// </summary>
        /// <param name="documentGUID">Document GUID to search for</param>
        /// <returns>LinkedFileInfo if found, null otherwise</returns>
        public LinkedFileInfo FindFileByGUID(string documentGUID)
        {
            if (string.IsNullOrEmpty(documentGUID))
                return null;

            lock (lockObject)
            {
                return linkedFiles.Values.FirstOrDefault(f => f.DocumentGUID == documentGUID);
            }
        }

        /// <summary>
        /// Validate file for connection with comprehensive checks
        /// </summary>
        /// <param name="filePath">Current file path</param>
        /// <param name="documentGuid">Current document GUID</param>
        /// <returns>Comprehensive validation result with recommendations</returns>
        public Task<FileConnectionValidation> ValidateFileForConnectionAsync(string filePath, string documentGuid)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return Task.FromResult(new FileConnectionValidation
                {
                    IsValid = false,
                    ValidationScenario = FileValidationScenario.ValidationError,
                    Message = "File path cannot be null or empty",
                    FilePath = filePath
                });
            }
            
            var validation = new FileConnectionValidation
            {
                FilePath = filePath,
                DocumentGUID = documentGuid
            };

            try
            {
                // Find by GUID first (most reliable)
                LinkedFileInfo linkedByGuid = null;
                if (!string.IsNullOrEmpty(documentGuid))
                {
                    linkedByGuid = FindFileByGUID(documentGuid);
                }

                // Find by path
                var linkedByPath = FindFilesByPath(filePath).FirstOrDefault();

                // Case 1: Perfect match - both GUID and path match
                if (linkedByGuid != null && linkedByPath != null && linkedByGuid.SessionId == linkedByPath.SessionId)
                {
                    validation.IsValid = true;
                    validation.SessionId = linkedByGuid.SessionId;
                    validation.LinkedFileInfo = linkedByGuid;
                    validation.ValidationScenario = FileValidationScenario.PerfectMatch;
                    validation.Message = "File and document GUID match existing session";
                    return Task.FromResult(validation);
                }

                // Case 2: GUID matches but path changed (file moved/renamed)
                if (linkedByGuid != null && linkedByGuid.FilePath != filePath)
                {
                    validation.IsValid = true;
                    validation.SessionId = linkedByGuid.SessionId;
                    validation.LinkedFileInfo = linkedByGuid;
                    validation.ValidationScenario = FileValidationScenario.FilePathChanged;
                    validation.Message = "Document GUID matches but file path has changed. File was likely moved or renamed.";
                    validation.RequiresUpdate = true;
                    return Task.FromResult(validation);
                }

                // Case 3: Path matches but no GUID in linked file (legacy file)
                if (linkedByPath != null && string.IsNullOrEmpty(linkedByPath.DocumentGUID))
                {
                    validation.IsValid = true;
                    validation.SessionId = linkedByPath.SessionId;
                    validation.LinkedFileInfo = linkedByPath;
                    validation.ValidationScenario = FileValidationScenario.LegacyFile;
                    validation.Message = "File path matches but no document GUID found. This is a legacy linked file.";
                    validation.RequiresUpdate = true;
                    return Task.FromResult(validation);
                }

                // Case 4: Path matches, linked file has GUID, but current file has no GUID (likely replaced)
                if (linkedByPath != null && !string.IsNullOrEmpty(linkedByPath.DocumentGUID) && string.IsNullOrEmpty(documentGuid))
                {
                    validation.IsValid = false;
                    validation.LinkedFileInfo = linkedByPath;
                    validation.ValidationScenario = FileValidationScenario.FileReplacedNoGUID;
                    validation.Message = "File at this path has no GUID but a previous file with GUID was linked here. The file was likely replaced.";
                    validation.RequiresUserDecision = true;
                    return Task.FromResult(validation);
                }

                // Case 5: Path matches but GUID different (file replaced)
                if (linkedByPath != null && !string.IsNullOrEmpty(documentGuid) && linkedByPath.DocumentGUID != documentGuid)
                {
                    validation.IsValid = false;
                    validation.LinkedFileInfo = linkedByPath;
                    validation.ValidationScenario = FileValidationScenario.FileReplaced;
                    validation.Message = "A different file exists at this path. The original file may have been replaced.";
                    validation.RequiresUserDecision = true;
                    return Task.FromResult(validation);
                }

                // Case 6: No existing link found
                validation.IsValid = false;
                validation.ValidationScenario = FileValidationScenario.NoLinkFound;
                validation.Message = "No existing session found for this file. A new session must be created through the host application.";
                return Task.FromResult(validation);
            }
            catch (Exception ex)
            {
                validation.IsValid = false;
                validation.ValidationScenario = FileValidationScenario.ValidationError;
                validation.Message = $"Error during file validation: {ex.Message}";
                return Task.FromResult(validation);
            }
        }

        /// <summary>
        /// Find linked files by file path (case-insensitive)
        /// </summary>
        /// <param name="filePath">File path to search for</param>
        /// <returns>List of matching LinkedFileInfo</returns>
        public List<LinkedFileInfo> FindFilesByPath(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                return new List<LinkedFileInfo>();

            lock (lockObject)
            {
                // Use case-insensitive comparison for Windows compatibility
                var comparison = Environment.OSVersion.Platform == PlatformID.Win32NT 
                    ? StringComparison.OrdinalIgnoreCase 
                    : StringComparison.Ordinal;
                    
                return linkedFiles.Values.Where(f => 
                    string.Equals(f.FilePath, filePath, comparison)).ToList();
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
            Logger.Info("All linked files cleared");
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
                Logger.Info($"Cleaned up {expiredSessions.Count} expired sessions");
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
                Logger.Info("All session data cleared");
            }
            catch (Exception ex)
            {
                Logger.Error($"Error clearing session data: {ex.Message}");
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
                Logger.Error($"Error saving linked files: {ex.Message}");
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

                    Logger.Info($"Loaded {fileList.Count} linked files from storage");
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error loading linked files: {ex.Message}");
            }
        }

    }

    /// <summary>
    /// Information about a linked file
    /// </summary>
    public class LinkedFileInfo
    {
        public string SessionId { get; set; }
        public string DocumentGUID { get; set; }  // NEW: Persistent document identifier
        public string FilePath { get; set; }
        public string OriginalPath { get; set; }  // Path when first registered
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
        Moved,
        PathChanged  // NEW: Path changed but GUID matches
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
        public string CurrentDocumentGUID { get; set; }
        public bool IsValid { get; set; }
        public FileValidationIssue Issue { get; set; }
        public string Message { get; set; }
        public LinkedFileInfo LinkedFileInfo { get; set; }
    }

    /// <summary>
    /// File validation issues
    /// </summary>
    public enum FileValidationIssue
    {
        None,
        FileNotFound,
        FileModified,
        GUIDMismatch,
        SessionNotFound,
        ValidationError
    }

    /// <summary>
    /// Comprehensive file connection validation result
    /// </summary>
    public class FileConnectionValidation
    {
        public string FilePath { get; set; }
        public string DocumentGUID { get; set; }
        public string SessionId { get; set; }
        public bool IsValid { get; set; }
        public string Message { get; set; }
        public LinkedFileInfo LinkedFileInfo { get; set; }
        public FileValidationScenario ValidationScenario { get; set; }
        public bool RequiresUpdate { get; set; }
        public bool RequiresUserDecision { get; set; }
    }

    /// <summary>
    /// File validation scenarios
    /// </summary>
    public enum FileValidationScenario
    {
        PerfectMatch,        // GUID and path both match
        FilePathChanged,     // GUID matches but path changed (file moved/renamed)
        LegacyFile,         // Path matches but no GUID in linked file (pre-GUID implementation)
        FileReplacedNoGUID, // Path matches, linked has GUID, current has no GUID (file replaced)
        FileReplaced,       // Path matches but GUID different (file was replaced)
        NoLinkFound,        // No existing link found
        ValidationError     // Error during validation
    }
} 