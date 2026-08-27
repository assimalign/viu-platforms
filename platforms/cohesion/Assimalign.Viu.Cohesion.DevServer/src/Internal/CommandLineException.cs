using System;

namespace Assimalign.Viu.Cohesion.DevServer;

internal sealed class CommandLineException : Exception
{
    public CommandLineException(string message)
        : base(message)
    {
    }
}
