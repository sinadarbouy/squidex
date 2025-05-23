﻿// ==========================================================================
//  Squidex Headless CMS
// ==========================================================================
//  Copyright (c) Squidex UG (haftungsbeschraenkt)
//  All rights reserved. Licensed under the MIT license.
// ==========================================================================

using MongoDB.Bson;
using MongoDB.Driver;

#pragma warning disable CA1822 // Mark members as static

namespace Squidex.Infrastructure;

public sealed class ProfilerCollection(IMongoDatabase database)
{
    private readonly IMongoCollection<ProfilerDocument> collection = database.GetCollection<ProfilerDocument>("system.profile");

    public async Task<IReadOnlyList<ProfilerDocument>> GetQueriesAsync(string collectionName,
        CancellationToken ct = default)
    {
        var ns = $"{collection.Database.DatabaseNamespace.DatabaseName}.{collectionName}";

        return await collection.Find(x => x.Operation == "query" && x.Namespace == ns).ToListAsync(ct);
    }

    public async Task ClearAsync(
        CancellationToken ct = default)
    {
        await database.RunCommandAsync<BsonDocument>("{ profile : 0 }", cancellationToken: ct);
        await database.DropCollectionAsync(ProfilerDocument.CollectionName, ct);
        await database.RunCommandAsync<BsonDocument>("{ profile : 2 }", cancellationToken: ct);
    }
}
