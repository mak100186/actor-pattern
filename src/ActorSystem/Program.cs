using ActorFramework.AspNetCore.Extensions;
using ActorFramework.Extensions;

using ActorSystem.Actors;
using ActorSystem.Messages;

using Bogus;

using Serilog;

var builder = WebApplication.CreateBuilder(args);

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

builder.Services.AddSingleton(new Faker<ContestMessage>()
        .CustomInstantiator(f => new(
            Key: f.Random.Guid().ToString(),
            FeedProvider: f.Company.CompanyName(),
            Name: f.Commerce.ProductName(),
            Start: f.Date.FutureOffset(),
            End: f.Date.FutureOffset(),
            Delay: f.Random.Int(100, 5000)
        )));

builder.Services.AddSingleton(new Faker<PropositionMessage>()
    .CustomInstantiator(f => new(
        Key: f.Random.Guid().ToString(),
        ContestKey: f.Random.Guid().ToString(),
        Name: f.Commerce.Department(),
        PropositionAvailability: f.PickRandom<PropositionAvailability>(),
        IsOpen: f.Random.Bool(),
        Delay: f.Random.Int(100, 5000)
    )));

builder.Services.AddOpenApi();

var app = builder.Build();

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
