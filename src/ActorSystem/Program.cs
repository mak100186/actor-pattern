using ActorFramework.Extensions;

using ActorSystem.Controllers;

using Serilog;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, lc) => lc
    .ReadFrom.Configuration(ctx.Configuration));

// Add services to the container.
builder.Services.AddActorFramework(builder.Configuration, actorBuilder =>
{
    actorBuilder.AddMessage<TestMessage>();
});

//Add controllers and OpenAPI support
builder.Services.AddControllers();
builder.Services.AddOpenApi();

WebApplication app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "ActorSystem API v1");
        options.RoutePrefix = "swagger";

        options.DisplayRequestDuration();
        options.EnableTryItOutByDefault();
    });
}

app.UseHttpsRedirection();

app.MapControllers();

await app.RunAsync();