using System;
using System.IO;
using System.Threading.Tasks;
using Rhino;
using ReerRhinoMCPPlugin.Core.Client;

namespace ReerRhinoMCPPlugin.Core.Common
{
    /// <summary>
    /// Detects SaveAs operations by monitoring Rhino document save events
    /// </summary>
    public class SaveAsDetector
    {
        private DocumentInfo documentInfoBeforeSave;

        /// <summary>
        /// Event raised when SaveAs operation is detected
        /// </summary>
        public event EventHandler<SaveAsDetectedEventArgs> SaveAsDetected;

        public SaveAsDetector()
        {
            // Subscribe to Rhino save events
            RhinoDoc.BeginSaveDocument += OnBeginSaveDocument;
            RhinoDoc.EndSaveDocument += OnEndSaveDocument;
        }

        /// <summary>
        /// Called when Rhino begins to save a document - capture current state
        /// </summary>
        private void OnBeginSaveDocument(object sender, DocumentSaveEventArgs e)
        {
            try
            {
                // Capture document state before save
                documentInfoBeforeSave = GetCurrentDocumentInfo();

                if (documentInfoBeforeSave != null)
                {
                    Logger.Debug($"BeginSave: Document {documentInfoBeforeSave.DocumentGuid} at '{documentInfoBeforeSave.FilePath}'");
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error in BeginSaveDocument handler: {ex.Message}");
            }
        }

        /// <summary>
        /// Called when Rhino finishes saving a document - detect SaveAs operations
        /// </summary>
        private void OnEndSaveDocument(object sender, DocumentSaveEventArgs e)
        {
            try
            {
                if (documentInfoBeforeSave == null)
                {
                    Logger.Debug("EndSave: No document info captured in BeginSave");
                    return;
                }

                // Get the saved file path from the event
                var savedFilePath = e.FileName;

                Logger.Debug($"EndSave: Document saved to '{savedFilePath}'");

                // Compare paths to detect SaveAs operation
                if (!string.IsNullOrEmpty(savedFilePath) &&
                    !string.IsNullOrEmpty(documentInfoBeforeSave.FilePath) &&
                    !string.Equals(documentInfoBeforeSave.FilePath, savedFilePath, StringComparison.OrdinalIgnoreCase))
                {
                    Logger.Info($"SaveAs operation detected: '{documentInfoBeforeSave.FilePath}' -> '{savedFilePath}'");
                    Logger.Debug($"Document GUID for SaveAs: {documentInfoBeforeSave.DocumentGuid}");

                    // Raise the event for the plugin to handle
                    Logger.Debug("Raising SaveAsDetected event...");
                    SaveAsDetected?.Invoke(this, new SaveAsDetectedEventArgs
                    {
                        DocumentGuid = documentInfoBeforeSave.DocumentGuid,
                        OldFilePath = documentInfoBeforeSave.FilePath,
                        NewFilePath = savedFilePath,
                        LinkedFileInfo = null // Will be populated by the plugin
                    });
                    Logger.Debug("SaveAsDetected event raised successfully");
                }
                else
                {
                    Logger.Debug($"Regular save operation: '{savedFilePath}'");
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error in EndSaveDocument handler: {ex.Message}");
            }
            finally
            {
                // Clear the captured state
                documentInfoBeforeSave = null;
            }
        }

        /// <summary>
        /// Get current document information for SaveAs detection
        /// </summary>
        /// <returns>Current document info or null if no active document</returns>
        private DocumentInfo GetCurrentDocumentInfo()
        {
            var doc = RhinoDoc.ActiveDoc;
            if (doc == null)
                return null;

            return new DocumentInfo
            {
                FilePath = doc.Path,
                DocumentGuid = FileIntegrityManager.GetExistingDocumentGUID(doc),
                Name = doc.Name
            };
        }

        /// <summary>
        /// Dispose of event subscriptions
        /// </summary>
        public void Dispose()
        {
            RhinoDoc.BeginSaveDocument -= OnBeginSaveDocument;
            RhinoDoc.EndSaveDocument -= OnEndSaveDocument;
        }
    }
}