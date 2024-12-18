﻿using System.Text.Json;
using FancyToDo.Core;
using FancyToDo.Infrastructure.Configuration;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using Microsoft.Extensions.Options;
using SharedKernel;

namespace FancyToDo.Infrastructure;

public class EventStore(CosmosClient cosmosClient, IOptions<EventStoreOptions> options)
    : IEventStore
{
    private readonly Container _container = cosmosClient
        .GetContainer(options.Value.DatabaseName, options.Value.ContainerName);

    public async Task Append<T>(T aggregateRoot)
        where T : AggregateRoot 
    {
        var batch = _container.CreateTransactionalBatch(new PartitionKey(aggregateRoot.Id.ToString()));

        var version = aggregateRoot.Version;
        foreach (var domainEvent in aggregateRoot.CollectDomainEvents())
        {
            EventStream stream = new
            (
                streamId: aggregateRoot.Id,
                eventType: domainEvent.GetType(),
                version: ++version, 
                payload: JsonSerializer.Serialize(domainEvent, domainEvent.GetType())
            );

            batch.CreateItem(stream);
        }

        // TODO: Best way to throw/handle exception?
        using TransactionalBatchResponse response = await batch.ExecuteAsync();
        if (!response.IsSuccessStatusCode)
            throw new CosmosException(response.ErrorMessage, response.StatusCode, 0, response.ActivityId, response.RequestCharge);
        
        
        aggregateRoot.ClearDomainEvents();
    }
    
    public async Task<T?> Load<T>(Guid id) 
        where T : AggregateRoot 
    {
        var events = await LoadEvents(id);
        if (events.Count == 0) 
            return default;     // TODO: Throw exception instead of return default 

        return (T)Activator.CreateInstance(typeof(T), 
            events
                .Select(s => (BaseDomainEvent)s.Deserialize())
            )!;
    }

    private async Task<List<EventStream>> LoadEvents(Guid id)
    {
        // Query CosmosDB Event Stream
        // StreamId = AggregateId = Partition Key
        IOrderedQueryable<EventStream> queryable = _container.GetItemLinqQueryable<EventStream>();

        var matches = queryable
            .Where(w => w.StreamId == id)
            .OrderBy(o => o.Version);

        using FeedIterator<EventStream> linqFeed = matches.ToFeedIterator();

        var events = new List<EventStream>();
        while (linqFeed.HasMoreResults)
        {
            FeedResponse<EventStream> response = await linqFeed.ReadNextAsync();
            events.AddRange(response);
        }

        return events;
    }
}