using System;
using System.IO;
using System.Reflection.PortableExecutable;

using Shouldly;
using Xunit;

namespace Assimalign.Cohesion.Viu.DevServer.Tests;

public sealed class ToolAssemblyPortabilityTests
{
    // The package's tools/ payload runs under the consumer's dotnet on any architecture. A RID-specific build stamps
    // the assembly AMD64 or Arm64, and dotnet exec on the other architecture then refuses to load it.
    [Fact(DisplayName = "Viu Cohesion DevServer - package: the tool assembly is AnyCPU IL")]
    public void ToolAssembly_AnyHostArchitecture_ShouldBeAnyCpuIntermediateLanguage()
    {
        string path = Path.Combine(AppContext.BaseDirectory, typeof(DevServerRunner).Assembly.GetName().Name + ".dll");
        using FileStream image = File.OpenRead(path);

        PEHeaders headers = new(image);

        headers.CoffHeader.Machine.ShouldBe(Machine.I386);
        headers.CorHeader.ShouldNotBeNull();
        (headers.CorHeader.Flags & CorFlags.ILOnly).ShouldBe(CorFlags.ILOnly);
        (headers.CorHeader.Flags & (CorFlags.Requires32Bit | CorFlags.ILLibrary)).ShouldBe((CorFlags)0);
    }
}
