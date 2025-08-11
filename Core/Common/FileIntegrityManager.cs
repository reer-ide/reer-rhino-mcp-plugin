using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Linq;
using Newtonsoft.Json;
using Rhino;
using Rhino.DocObjects;
using ReerRhinoMCPPlugin.Core.Client;

namespace ReerRhinoMCPPlugin.Core.Common
{
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
        public bool ValidationError { get; set; } = false;
        public bool RequiresUserDecision { get; set; }
    }

    /// <summary>
    /// File validation scenarios
    /// </summary>
    public enum FileValidationScenario
    {
        PerfectMatch,        // GUID and path both match
        FilePathChanged,     // GUID matches but path changed (file moved/renamed)
        FileReplaced,       // Path matches but GUID different or no GUID (file was replaced)
        NoLinkFound,        // No existing link found, file is new or could have has some other GUID
    }

    /// <summary>
    /// Document information for SaveAs detection
    /// </summary>
    public class DocumentInfo
    {
        public string FilePath { get; set; }
        public string DocumentGuid { get; set; }
        public string Name { get; set; }
    }

    /// <summary>
    /// Event arguments for SaveAs detection
    /// </summary>
    public class SaveAsDetectedEventArgs : EventArgs
    {
        public string DocumentGuid { get; set; }
        public string OldFilePath { get; set; }
        public string NewFilePath { get; set; }
        public LinkedFileInfo LinkedFileInfo { get; set; }
    }

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

                // Set GUID in document if provided (server-generated GUID)
                if (!string.IsNullOrEmpty(documentGUID))
                {
                    SetDocumentGUID(documentGUID);
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
        /// <param name="documentGuid">Current document GUID if exists</param>
        /// <returns>Comprehensive validation result with recommendations</returns>
        public Task<FileConnectionValidation> ValidateFileForConnectionAsync(string filePath, string documentGuid = null)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return Task.FromResult(new FileConnectionValidation
                {
                    IsValid = false,
                    ValidationError = true,
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
                    validation.IsValid = false;
                    validation.SessionId = linkedByGuid.SessionId;
                    validation.LinkedFileInfo = linkedByGuid;
                    validation.ValidationScenario = FileValidationScenario.FilePathChanged;
                    validation.Message = "Document GUID matches but file path has changed. File was likely moved or renamed.";
                    validation.RequiresUserDecision = true;
                    return Task.FromResult(validation);
                }

                // Case 3: Path matches but GUID is different or missing (file replaced)
                if (linkedByPath != null && linkedByPath.DocumentGUID != documentGuid)
                {
                    validation.IsValid = false;
                    validation.LinkedFileInfo = linkedByPath;
                    validation.SessionId = linkedByPath.SessionId;
                    validation.ValidationScenario = FileValidationScenario.FileReplaced;
                    validation.Message = "File at this path has been replaced. The file was likely deleted and a new one was placed at the same location.";
                    validation.RequiresUserDecision = false; // Auto-connect, user's intent is clear
                    return Task.FromResult(validation);
                }

                // Case 4: No existing link found - should try server connection
                validation.IsValid = true; // Let connection logic handle this
                validation.ValidationScenario = FileValidationScenario.NoLinkFound;
                validation.Message = "No existing local link found. Will attempt to connect to server session.";
                return Task.FromResult(validation);
            }
            catch (Exception ex)
            {
                validation.IsValid = false;
                validation.ValidationError = true;
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
                var comparison = System.Environment.OSVersion.Platform == PlatformID.Win32NT
                    ? StringComparison.OrdinalIgnoreCase
                    : StringComparison.Ordinal;

                return linkedFiles.Values.Where(f =>
                    string.Equals(f.FilePath, filePath, comparison)).ToList();
            }
        }

        /// <summary>
        /// Check if a document GUID has an active session
        /// </summary>
        /// <param name="documentGuid">Document GUID to check</param>
        /// <returns>LinkedFileInfo if found, null otherwise</returns>
        public LinkedFileInfo GetLinkedFileByGUID(string documentGuid)
        {
            if (string.IsNullOrEmpty(documentGuid))
                return null;

            Logger.Debug($"Looking for linked file with GUID: {documentGuid}");
            
            // Debug: List all linked files
            lock (lockObject)
            {
                Logger.Debug($"Currently have {linkedFiles.Count} linked files:");
                foreach (var kvp in linkedFiles)
                {
                    var file = kvp.Value;
                    Logger.Debug($"  Session {kvp.Key}: GUID={file.DocumentGUID}, Path={file.FilePath}");
                }
            }

            return FindFileByGUID(documentGuid);
        }

        /// <summary>
        /// Update session file path (called by plugin after user decision)
        /// </summary>
        /// <param name="sessionId">Session ID</param>
        /// <param name="newPath">New file path</param>
        /// <returns>True if updated successfully</returns>
        public async Task<bool> UpdateSessionFilePathAsync(string sessionId, string newPath)
        {
            try
            {
                LinkedFileInfo linkedFile = null;
                lock (lockObject)
                {
                    if (!linkedFiles.TryGetValue(sessionId, out linkedFile))
                    {
                        Logger.Warning($"Session {sessionId} not found for file path update");
                        return false;
                    }

                    // Update the linked file information
                    linkedFile.FilePath = newPath;
                    linkedFile.FileSize = GetFileSize(newPath);
                    linkedFile.LastModified = File.Exists(newPath) ? File.GetLastWriteTime(newPath) : DateTime.MinValue;
                    linkedFile.Status = File.Exists(newPath) ? FileStatus.PathChanged : FileStatus.Missing;
                    linkedFile.LastChecked = DateTime.Now;
                }

                await SaveLinkedFiles();

                Logger.Success($"Session {sessionId} file path updated to: {Path.GetFileName(newPath)}");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error updating session file path: {ex.Message}");
                return false;
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
        /// Dispose of resources
        /// </summary>
        public void Dispose()
        {
            try
            {
                Logger.Debug("FileIntegrityManager disposed successfully");
            }
            catch (Exception ex)
            {
                Logger.Error($"Error disposing FileIntegrityManager: {ex.Message}");
            }
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
                // This call will automatically trigger migration of any hidden .linked_files.dat to linked_files.dat
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
                else
                {
                    Logger.Debug("No linked files found in storage");
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error loading linked files: {ex.Message}");
            }
        }

        #region GUID Management

        private const string DOCUMENT_GUID_KEY = "REER_MCP_DOCUMENT_ID";

        /// <summary>
        /// Get existing document GUID without creating a new one
        /// </summary>
        /// <param name="doc">Rhino document (null for active document)</param>
        /// <returns>Document GUID string or null if not found</returns>
        public static string GetExistingDocumentGUID(RhinoDoc doc = null)
        {
            doc = doc ?? RhinoDoc.ActiveDoc;
            if (doc == null)
            {
                return null;
            }

            try
            {
                var guid = doc.Strings.GetValue(DOCUMENT_GUID_KEY);
                return string.IsNullOrEmpty(guid) ? null : guid;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error getting document GUID: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Set document GUID (from server-provided GUID)
        /// </summary>
        /// <param name="documentGuid">GUID to set</param>
        /// <param name="doc">Rhino document (null for active document)</param>
        /// <returns>True if successful</returns>
        public static bool SetDocumentGUID(string documentGuid, RhinoDoc doc = null)
        {
            if (string.IsNullOrEmpty(documentGuid))
                return false;

            doc = doc ?? RhinoDoc.ActiveDoc;
            if (doc == null)
                return false;

            try
            {
                doc.Strings.SetString(DOCUMENT_GUID_KEY, documentGuid);
                Logger.Info($"Set document GUID: {documentGuid}");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error setting document GUID: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Delete document GUID 
        /// </summary>
        /// <param name="doc">Rhino document (null for active document)</param>
        /// <returns>True if successful</returns>
        public static bool DeleteDocumentGUID(RhinoDoc doc = null)
        {
            doc = doc ?? RhinoDoc.ActiveDoc;
            if (doc == null)
                return false;
            try
            {
                doc.Strings.Delete(DOCUMENT_GUID_KEY);
                Logger.Info($"Deleted document GUID");
                return true;
            }catch (Exception ex)
            {
                Logger.Error($"Error deleting document GUID: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Check if current document has a different GUID than expected
        /// </summary>
        /// <param name="expectedGuid">Expected GUID</param>
        /// <returns>True if GUID differs or is missing</returns>
        public static bool HasDifferentGUID(string expectedGuid)
        {
            if (string.IsNullOrEmpty(expectedGuid))
                return false;

            var currentGuid = GetExistingDocumentGUID();
            return currentGuid != expectedGuid;
        }

        /// <summary>
        /// Clear the document GUID (for testing/debugging)
        /// </summary>
        /// <param name="doc">Rhino document (null for active document)</param>
        public static void ClearDocumentGUID(RhinoDoc doc = null)
        {
            doc = doc ?? RhinoDoc.ActiveDoc;
            if (doc == null)
                return;

            try
            {
                doc.Strings.Delete(DOCUMENT_GUID_KEY);
                Logger.Info($"Cleared document GUID for {doc.Name}");
            }
            catch (Exception ex)
            {
                Logger.Error($"Error clearing document GUID: {ex.Message}");
            }
        }

        #endregion

    }

} 