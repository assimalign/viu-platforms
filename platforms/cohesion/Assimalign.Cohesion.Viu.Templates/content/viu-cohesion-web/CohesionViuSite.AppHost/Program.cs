using Assimalign.Cohesion.ApplicationModel;

IApplicationBuilder builder = Gateway.CreateBuilder(args);
// The gateway names what it composes: the Viu verb validates the Web manifest's Viu facts and
// plans it as a Web resource. The generated Add__MEMBER_PREFIX__Web() verb plans the same resource
// without that validation.
builder.AddViuWeb(Manifests.__MEMBER_PREFIX__Web);
builder.UseGateway(args);
await builder.Build().RunAsync();
