using System.Threading.Tasks;

namespace TypingApp.Services
{
    /// <summary>
    /// Defines the contract for a typing engine that simulates keyboard input.
    /// </summary>
    public interface ITypingEngine
    {
        /// <summary>
        /// Types the specified text with an optional delay between keystrokes.
        /// </summary>
        /// <param name="text">The text to type.</param>
        /// <param name="delayMilliseconds">Delay in milliseconds between each key press.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task TypeTextAsync(string text, int delayMilliseconds = 0);
    }
}
