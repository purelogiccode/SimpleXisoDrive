namespace SimpleXisoDrive;

/// <summary>
/// Represents an error that occurs when a file is not a valid Xbox ISO image
/// or when its volume descriptor cannot be read.
/// </summary>
public class InvalidImageException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="InvalidImageException"/> class with a specified
    /// error message and a reference to the inner exception that caused this exception.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="inner">The exception that is the cause of this exception, or <see langword="null"/> if no inner exception is specified.</param>
    public InvalidImageException(string message, Exception? inner = null)
        : base(message, inner)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="InvalidImageException"/> class.
    /// </summary>
    public InvalidImageException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="InvalidImageException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public InvalidImageException(string? message) : base(message)
    {
    }
}
