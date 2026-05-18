using Ganss.Xss;
using Nocturne.Core.Models;

namespace Nocturne.Core.Contracts.Legacy;

/// <summary>
/// Generic service for processing documents with sanitization and timestamp conversion
/// </summary>
public interface IDocumentProcessingService
{
    /// <summary>
    /// Process a collection of documents by sanitizing HTML content and converting timestamps
    /// </summary>
    /// <typeparam name="T">Document type that implements IProcessableDocument</typeparam>
    /// <param name="documents">The documents to process</param>
    /// <returns>Processed documents with sanitized content and converted timestamps</returns>
    IEnumerable<T> ProcessDocuments<T>(IEnumerable<T> documents)
        where T : IProcessableDocument;

    /// <summary>
    /// Sanitize HTML content
    /// </summary>
    /// <param name="htmlContent">The HTML content to sanitize</param>
    /// <returns>Sanitized HTML content safe for display</returns>
    string SanitizeHtml(string? htmlContent);

    /// <summary>
    /// Convert timezone-aware timestamps to UTC and set utcOffset
    /// </summary>
    /// <param name="document">The document to process</param>
    void ProcessTimestamp(IProcessableDocument document);

    /// <summary>
    /// Process a single entry document
    /// </summary>
    /// <param name="entry">The entry to process</param>
    /// <returns>Processed entry</returns>
    Entry ProcessEntry(Entry entry);

    /// <summary>
    /// Process a single treatment document
    /// </summary>
    /// <param name="treatment">The treatment to process</param>
    /// <returns>Processed treatment</returns>
    Treatment ProcessTreatment(Treatment treatment);
}
