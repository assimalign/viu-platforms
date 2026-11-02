using System;

using Assimalign.Cohesion.ApplicationModel;
using Assimalign.Cohesion.ApplicationModel.Gateway;
using Assimalign.Cohesion.ApplicationModel.Gateway.InProcess;

// The in-process platform runs each resource's program on a thread of this process, in an environment
// of its own: its name, the gateway, its http endpoint and its settings. It runs only the programs
// registered here.
await using var infrastructure = new InProcessInfrastructure();
infrastructure.AddProgram("Web", "__APPLICATION_NAME__-web", CohesionViuSite.Web.Program.MainAsync);

ApplicationGatewayBuilder builder = ApplicationGateway.CreateBuilder("__APPLICATION_NAME__").UseInProcess(infrastructure);

// The model: one AddResource(provider, name) per resource, with its dependencies and settings.
builder.AddResource("Web", "__APPLICATION_NAME__-web");

// The gateway stops on Ctrl+C or a termination signal; disposing the infrastructure then stops the
// programs it runs.
ApplicationGateway gateway = builder.Build();
bool started;
try
{
    await gateway.StartAsync();
    started = true;
}
catch
{
    // A startup that failed, or that a shutdown signal cancelled, ends the run; Completion reports
    // its outcome, a failure included (as RunAsync does).
    started = false;
}

if (started)
{
    foreach (IApplicationGatewayDeployment resource in gateway.Resources)
    {
        if (resource.Outputs.TryGetValue(OutputName.Endpoint, out string? endpoint))
        {
            Console.WriteLine($"{resource.Name.Value}: {endpoint}");
        }
    }
}

await gateway.Completion;
