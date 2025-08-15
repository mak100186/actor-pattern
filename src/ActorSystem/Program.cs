using ActorFramework.AspNetCore.Extensions;
using ActorFramework.Extensions;

using ActorSystem.Controllers;

using Serilog;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, lc) => lc
    .ReadFrom.Configuration(ctx.Configuration));

ActorRegistrationBuilder actorRegistrationBuilder = new();
builder.Services.AddActorFramework(builder.Configuration, actorBuilder =>
{
    actorBuilder.AddActor<ContestActor, ContestMessage>();
    actorBuilder.AddActor<PropositionActor, PropositionMessage>();

    actorRegistrationBuilder = actorBuilder;
});

//Add controllers and OpenAPI support
builder.Services
    .AddControllers()
    .AddActorFrameworkJsonPolymorphism(actorRegistrationBuilder);

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