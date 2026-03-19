using System.Threading.Tasks;

namespace TypingApp.Services
{
    /// <summary>
    /// Defines the contract for a typing engine that simulates keyboard input.
    /// </summary>
    public interface ITypingEngine
    {
        /// <summary>
        /// Triggered when the processing state changes (e.g., typing starts/ends).
        /// </summary>
        event System.Action<bool> ProcessingChanged;

        /// <summary>
        /// Types the specified text with an optional delay between keystrokes.
        /// </summary>
        /// <param name="text">The text to type.</param>
        /// <param name="delayMilliseconds">Delay in milliseconds between each key press.</param>
        /// <param name="ct">Optional cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task TypeTextAsync(string text, int delayMilliseconds = 0, System.Threading.CancellationToken ct = default);

        /// <summary>
        /// Instantly pastes the entire text as a single stream/batch.
        /// </summary>
        Task PasteTextAsBulkAsync(string text, System.Threading.CancellationToken ct = default);
    }
}
