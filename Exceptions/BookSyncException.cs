namespace BookonnectCore.Exceptions
{
  using System;

  [Serializable]
  public class BookSyncException : Exception
  {
    public string? FilePath { get; }
    public int? LineNumber { get; }

    public BookSyncException(string message)
      : base(message)
    {
    }

    public BookSyncException(string message, Exception inner)
      : base(message, inner)
    {
    }

    public BookSyncException(string message, string? filePath, int? lineNumber = null)
      : base(message)
    {
      FilePath = filePath;
      LineNumber = lineNumber;
    }

    public BookSyncException(string message, string? filePath, Exception inner, int? lineNumber = null)
      : base(message, inner)
    {
      FilePath = filePath;
      LineNumber = lineNumber;
    }
  }
}