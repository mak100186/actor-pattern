using System.Text.Json.Serialization.Metadata;

using ActorFramework.Abstractions;
using ActorFramework.Extensions;

using Microsoft.Extensions.DependencyInjection;

namespace ActorFramework.AspNetCore.Extensions;

public static class ActorFrameworkAspNetCoreExtensions
{
    public static IMvcBuilder AddActorFrameworkJsonPolymorphism(
        this IMvcBuilder mvcBuilder,
        ActorRegistrationBuilder builder) => mvcBuilder.AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.WriteIndented = true;

        options.JsonSerializerOptions.TypeInfoResolverChain.Insert(
            0,
            new DefaultJsonTypeInfoResolver
            {
                Modifiers =
                {
                    typeInfo =>
                    {
                        if (typeInfo.Type == typeof(IMessage))
                        {
                            JsonPolymorphismOptions polyOptions = new() {
                                TypeDiscriminatorPropertyName = "$type",
                                IgnoreUnrecognizedTypeDiscriminators = true
                            };

                            foreach (var messageType in builder.MessageToActorMap.Select(kvp => kvp.Key))
                            {
                                var discriminator = char.ToLowerInvariant(messageType.Name[0]) + messageType.Name[1..];
                                polyOptions.DerivedTypes.Add(new(messageType, discriminator));
                            }

                            typeInfo.PolymorphismOptions = polyOptions;
                        }
                    }
                }
            }
        );
    });
}