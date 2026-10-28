namespace BookonnectCore.Exceptions
{
  public sealed class AlreadyExistsException : Exception
  {
    public AlreadyExistsException(string message)
      : base(message)
    {
    }

    public AlreadyExistsException(string message, Exception innerException)
      : base(message, innerException)
    {
    }
  }
}
