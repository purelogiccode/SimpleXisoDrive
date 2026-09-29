using SimpleXisoDrive.Core;

namespace SimpleXisoDrive.Tests;

/// <summary>
/// Tests the constructors and exception semantics of <c>InvalidImageException</c>.
/// </summary>
public class InvalidImageExceptionTests
{
    /// <summary>
    /// Verifies the constructor stores the supplied message.
    /// </summary>
    [Fact]
    public void Constructor_SetsMessage()
    {
        var ex = new InvalidImageException("Test error");
        Assert.Equal("Test error", ex.Message);
    }

    /// <summary>
    /// Verifies the constructor stores the supplied inner exception.
    /// </summary>
    [Fact]
    public void Constructor_SetsInnerException()
    {
        var inner = new InvalidOperationException("Inner error");
        var ex = new InvalidImageException("Outer error", inner);

        Assert.Equal("Outer error", ex.Message);
        Assert.Same(inner, ex.InnerException);
    }

    /// <summary>
    /// Verifies a null inner exception is allowed.
    /// </summary>
    [Fact]
    public void Constructor_AllowsNullInnerException()
    {
        var ex = new InvalidImageException("Test error", null);
        Assert.Equal("Test error", ex.Message);
        Assert.Null(ex.InnerException);
    }

    /// <summary>
    /// Verifies the parameterless constructor produces a default message.
    /// </summary>
    [Fact]
    public void ParameterlessConstructor_HasDefaultMessage()
    {
        var ex = new InvalidImageException();

        Assert.NotNull(ex.Message);
        Assert.Null(ex.InnerException);
    }

    /// <summary>
    /// Verifies the message-only constructor accepts a null message.
    /// </summary>
    [Fact]
    public void MessageOnlyConstructor_AcceptsNull()
    {
        var ex = new InvalidImageException((string?)null);

        Assert.NotNull(ex.Message);
    }

    /// <summary>
    /// Verifies <c>InvalidImageException</c> derives from <c>Exception</c>.
    /// </summary>
    [Fact]
    public void IsException_DerivedFromException()
    {
        var ex = new InvalidImageException("Test");
        Assert.IsType<Exception>(ex, exactMatch: false);
    }

    /// <summary>
    /// Verifies the exception can be caught as <c>Exception</c>.
    /// </summary>
    [Fact]
    public void CanBeCaughtAsException()
    {
        Exception caught;
        try
        {
            throw new InvalidImageException("Test");
        }
        catch (Exception ex)
        {
            caught = ex;
        }

        Assert.IsType<InvalidImageException>(caught);
    }
}