using System;

namespace Assimalign.Cohesion.Viu.DevServer;

internal sealed class CommandLineException : Exception
{
    public CommandLineException(string message)
        : base(message)
    {
    }
}
