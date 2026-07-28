namespace Piro.Cli;

/// <summary>
/// An error meant for the user, printed as a plain message rather than a stack trace. Anything else
/// escaping to the top level is a bug and is reported as such.
/// </summary>
internal sealed class CliException(string message) : Exception(message);
