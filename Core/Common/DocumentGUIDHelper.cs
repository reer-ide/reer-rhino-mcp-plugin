using System;
using Rhino;

namespace ReerRhinoMCPPlugin.Core.Common
{
    /// <summary>
    /// Helper class for managing document GUID identification
    /// </summary>
    public static class DocumentGUIDHelper
    {
        private const string DOCUMENT_GUID_KEY = "REER_MCP_DOCUMENT_ID";

        /// <summary>
        /// Get or create document GUID for the active Rhino document
        /// </summary>
        /// <returns>Document GUID string</returns>
        public static string GetOrCreateDocumentGUID()
        {
            var doc = RhinoDoc.ActiveDoc;
            if (doc == null)
            {
                Logger.Warning("No active Rhino document found");
                return null;
            }

            return GetOrCreateDocumentGUID(doc);
        }

        /// <summary>
        /// Get or create document GUID for a specific Rhino document
        /// </summary>
        /// <param name="doc">Rhino document</param>
        /// <returns>Document GUID string</returns>
        public static string GetOrCreateDocumentGUID(RhinoDoc doc)
        {
            if (doc == null)
            {
                Logger.Warning("Document is null");
                return null;
            }

            try
            {
                // Try to get existing GUID from document user strings
                var existingGuid = doc.Strings.GetValue(DOCUMENT_GUID_KEY);
                
                if (!string.IsNullOrEmpty(existingGuid))
                {
                    Logger.Debug($"Found existing document GUID: {existingGuid}");
                    return existingGuid;
                }

                // Generate new GUID and store in document
                var newGuid = Guid.NewGuid().ToString();
                doc.Strings.SetString(DOCUMENT_GUID_KEY, newGuid);
                
                Logger.Info($"Created new document GUID: {newGuid} for {doc.Name}");
                return newGuid;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error managing document GUID: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Get existing document GUID without creating a new one
        /// </summary>
        /// <param name="doc">Rhino document</param>
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
        /// <param name="doc">Rhino document</param>
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
    }
}