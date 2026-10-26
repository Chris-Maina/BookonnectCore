namespace BookonnectCore.Exceptions;

public sealed class AccountLinkRequiredException : Exception
{
    public AccountLinkRequiredException()
        : base("An existing profile requires verified account linking.")
    {
    }
}
