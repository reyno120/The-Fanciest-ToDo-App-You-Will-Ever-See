﻿using FancyToDo.Core;
using FancyToDo.Infrastructure;
using FancyToDo.Infrastructure.Configuration;
using Microsoft.Azure.Cosmos;

namespace FancyToDo.API.Configuration;

public static class ConfigurationExtensions
{
   public static WebApplicationBuilder ConfigureDataStore(this WebApplicationBuilder builder)
   {
      builder.AddAzureCosmosClient(
         "local-fancy-cosmos",
         null,
         clientOptions =>
         {
            clientOptions.SerializerOptions = new()
            {
               PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase
            };
         });


      // TODO: Should EventStore & Repository be Scoped or Singleton?
      builder.Services.Configure<EventStoreOptions>(builder.Configuration.GetSection(EventStoreOptions.EventStore));
      builder.Services.AddSingleton<IEventStore, EventStore>();
      
      builder.Services.Configure<ProjectionOptions>(builder.Configuration.GetSection(ProjectionOptions.Projection));
      builder.Services.AddSingleton<ToDoListReadOnlyRepository>();

      return builder;
   }
}
