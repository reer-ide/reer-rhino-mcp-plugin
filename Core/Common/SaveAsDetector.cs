using System;
using System.IO;
using System.Threading.Tasks;
using Rhino;
using Rhino.Input.Custom;
using ReerRhinoMCPPlugin.Core.Client;

namespace ReerRhinoMCPPlugin.Core.Common
{
    /// <summary>
    /// User choice for SaveAs operation
    /// </summary>
    public enum SaveAsUserChoice
    {
        ContinueWithNewFile,
        ReturnToOriginalFile,
        Cancel
    }

    /// <summary>
    /// Detects SaveAs operations by monitoring Rhino document save events
    /// </summary>
    public class SaveAsDetector
    {
        private DocumentInfo documentInfoBeforeSave;

        
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
        /// Show user confirmation dialog for SaveAs operation using Rhino's command prompt
        /// </summary>
        public static Task<SaveAsUserChoice> ShowSaveAsConfirmationDialog(string oldPath, string newPath)
        {
            var tcs = new TaskCompletionSource<SaveAsUserChoice>();
            
            // Execute on main thread after a short delay to ensure SaveAs completes
            RhinoApp.InvokeOnUiThread(new System.Action(async () =>
            {
                try
                {
                    // Add a small delay to ensure SaveAs operation completes
                    await Task.Delay(500);
                    
                    var oldFileName = Path.GetFileName(oldPath);
                    var newFileName = Path.GetFileName(newPath);

                    // Display information to user in command history
                    RhinoApp.WriteLine("");
                    RhinoApp.WriteLine(new string('=', 60));
                    RhinoApp.WriteLine("MCP SAVE AS OPERATION DETECTED");
                    RhinoApp.WriteLine(new string('=', 60));
                    RhinoApp.WriteLine($"You saved a copy to: {newFileName}");
                    RhinoApp.WriteLine($"Original file: {oldFileName}");
                    RhinoApp.WriteLine("");
                    RhinoApp.WriteLine("Choose how to continue your MCP session:");
                    RhinoApp.WriteLine("  1. UseNewFile - Continue with the new saved file");
                    RhinoApp.WriteLine("  2. ReturnToOriginal - Open original file and continue");
                    RhinoApp.WriteLine("  3. DoNothing - Keep current state");
                    RhinoApp.WriteLine("");

                    // Use Rhino's GetOption for user choice
                    var getOption = new GetOption();
                    getOption.SetCommandPrompt("Select action for MCP session");
                    getOption.AcceptNothing(false);

                    int continueNewIndex = getOption.AddOption("UseNewFile");
                    int returnOriginalIndex = getOption.AddOption("ReturnToOriginal");  
                    int cancelIndex = getOption.AddOption("DoNothing");

                    var result = getOption.Get();

                    SaveAsUserChoice choice;
                    if (result == Rhino.Input.GetResult.Option)
                    {
                        var option = getOption.Option();
                        if (option != null && option.Index == continueNewIndex)
                        {
                            RhinoApp.WriteLine($"✓ MCP session will now use: {newFileName}");
                            choice = SaveAsUserChoice.ContinueWithNewFile;
                        }
                        else if (option != null && option.Index == returnOriginalIndex)
                        {
                            RhinoApp.WriteLine($"✓ Opening original file: {oldFileName}");
                            choice = SaveAsUserChoice.ReturnToOriginalFile;
                        }
                        else if (option != null && option.Index == cancelIndex)
                        {
                            RhinoApp.WriteLine("✓ MCP session unchanged");
                            choice = SaveAsUserChoice.Cancel;
                        }
                        else
                        {
                            // Default
                            RhinoApp.WriteLine($"⚠ Defaulting to use new file: {newFileName}");
                            choice = SaveAsUserChoice.ContinueWithNewFile;
                        }
                    }
                    else if (result == Rhino.Input.GetResult.Cancel || result == Rhino.Input.GetResult.Nothing)
                    {
                        RhinoApp.WriteLine("✗ SaveAs handling cancelled - MCP session unchanged");
                        choice = SaveAsUserChoice.Cancel;
                    }
                    else
                    {
                        // Default if no valid option selected
                        RhinoApp.WriteLine($"⚠ Defaulting to use new file: {newFileName}");
                        choice = SaveAsUserChoice.ContinueWithNewFile;
                    }
                    
                    tcs.SetResult(choice);
                }
                catch (Exception ex)
                {
                    Logger.Error($"Error in SaveAs confirmation dialog: {ex.Message}");
                    RhinoApp.WriteLine($"✗ Error in SaveAs dialog: {ex.Message}");
                    // Safe default
                    tcs.SetResult(SaveAsUserChoice.ContinueWithNewFile);
                }
            }));
            
            return tcs.Task;
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