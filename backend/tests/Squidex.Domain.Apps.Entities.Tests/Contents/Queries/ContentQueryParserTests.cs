﻿// ==========================================================================
//  Squidex Headless CMS
// ==========================================================================
//  Copyright (c) Squidex UG (haftungsbeschraenkt)
//  All rights reserved. Licensed under the MIT license.
// ==========================================================================

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Squidex.Domain.Apps.Core;
using Squidex.Domain.Apps.Core.Schemas;
using Squidex.Domain.Apps.Core.TestHelpers;
using Squidex.Domain.Apps.Entities.Contents.Text;
using Squidex.Domain.Apps.Entities.TestHelpers;
using Squidex.Infrastructure;
using Squidex.Infrastructure.Json.Objects;
using Squidex.Infrastructure.Queries;
using Squidex.Infrastructure.Validation;

namespace Squidex.Domain.Apps.Entities.Contents.Queries;

public class ContentQueryParserTests : GivenContext
{
    private readonly ITextIndex textIndex = A.Fake<ITextIndex>();
    private readonly ContentQueryParser sut;

    public ContentQueryParserTests()
    {
        var options = Options.Create(new ContentsOptions { DefaultPageSize = 30 });

        Schema = Schema
            .AddString(1, "firstName", Partitioning.Invariant)
            .AddGeolocation(2, "geo", Partitioning.Invariant);

        var cache = new MemoryCache(Options.Create(new MemoryCacheOptions()));

        sut = new ContentQueryParser(AppProvider, textIndex, options, cache, TestUtils.DefaultSerializer);
    }

    [Fact]
    public async Task Should_skip_total_if_set_in_context()
    {
        var q = await sut.ParseAsync(ApiContext.Clone(b => b.WithNoTotal()), Q.Empty, ct: CancellationToken);

        Assert.True(q.NoTotal);
    }

    [Fact]
    public async Task Should_throw_if_odata_query_is_invalid()
    {
        var query = Q.Empty.WithODataQuery("$filter=invalid");

        await Assert.ThrowsAsync<ValidationException>(() => sut.ParseAsync(ApiContext, query, Schema, CancellationToken));
    }

    [Fact]
    public async Task Should_throw_if_json_query_is_invalid()
    {
        var query = Q.Empty.WithJsonQuery("invalid");

        await Assert.ThrowsAsync<ValidationException>(() => sut.ParseAsync(ApiContext, query, Schema, CancellationToken));
    }

    [Fact]
    public async Task Should_parse_odata_query_without_schema()
    {
        var query = Q.Empty.WithODataQuery("$filter=status eq 'Draft'");

        var q = await sut.ParseAsync(ApiContext, query, ct: CancellationToken);

        Assert.Equal("Filter: status == 'Draft'; Take: 30; Sort: lastModified Descending, id Ascending", q.Query.ToString());
    }

    [Fact]
    public async Task Should_parse_json_query_without_schema()
    {
        var query = Q.Empty.WithJsonQuery("{ \"filter\": { \"path\": \"status\", \"op\": \"eq\", \"value\": \"Draft\" } }");

        var q = await sut.ParseAsync(ApiContext, query, ct: CancellationToken);

        Assert.Equal("Filter: status == 'Draft'; Take: 30; Sort: lastModified Descending, id Ascending", q.Query.ToString());
    }

    [Fact]
    public async Task Should_parse_odata_query()
    {
        var query = Q.Empty.WithODataQuery("$top=100&$orderby=data/firstName/iv asc&$filter=status eq 'Draft'");

        var q = await sut.ParseAsync(ApiContext, query, Schema, CancellationToken);

        Assert.Equal("Filter: status == 'Draft'; Take: 100; Sort: data.firstName.iv Ascending, id Ascending", q.Query.ToString());
    }

    [Fact]
    public async Task Should_parse_odata_query_and_enrich_with_CancellationTokens()
    {
        var query = Q.Empty.WithODataQuery("$top=200&$filter=data/firstName/iv eq 'ABC'");

        var q = await sut.ParseAsync(ApiContext, query, Schema, CancellationToken);

        Assert.Equal("Filter: data.firstName.iv == 'ABC'; Take: 200; Sort: lastModified Descending, id Ascending", q.Query.ToString());
    }

    [Fact]
    public async Task Should_parse_json_query_and_enrich_with_CancellationTokens()
    {
        var query = Q.Empty.WithJsonQuery("{ \"filter\": { \"path\": \"data.firstName.iv\", \"op\": \"eq\", \"value\": \"ABC\" } }");

        var q = await sut.ParseAsync(ApiContext, query, Schema, CancellationToken);

        Assert.Equal("Filter: data.firstName.iv == 'ABC'; Take: 30; Sort: lastModified Descending, id Ascending", q.Query.ToString());
    }

    [Fact]
    public async Task Should_convert_full_text_query_to_filter_with_other_filter()
    {
        A.CallTo(() => textIndex.SearchAsync(ApiContext.App, A<TextQuery>.That.Matches(x => x.Text == "Hello"), ApiContext.Scope(), CancellationToken))
            .Returns([DomainId.Create("1"), DomainId.Create("2")]);

        var query = Q.Empty.WithODataQuery("$search=Hello&$filter=data/firstName/iv eq 'ABC'");

        var q = await sut.ParseAsync(ApiContext, query, Schema, CancellationToken);

        Assert.Equal("Filter: (data.firstName.iv == 'ABC' && id in ['1', '2']); Take: 30; Sort: lastModified Descending, id Ascending", q.Query.ToString());
    }

    [Fact]
    public async Task Should_convert_full_text_query_to_filter()
    {
        A.CallTo(() => textIndex.SearchAsync(ApiContext.App, A<TextQuery>.That.Matches(x => x.Text == "Hello"), ApiContext.Scope(), CancellationToken))
            .Returns([DomainId.Create("1"), DomainId.Create("2")]);

        var query = Q.Empty.WithODataQuery("$search=Hello");

        var q = await sut.ParseAsync(ApiContext, query, Schema, CancellationToken);

        Assert.Equal("Filter: id in ['1', '2']; Take: 30; Sort: lastModified Descending, id Ascending", q.Query.ToString());
    }

    [Fact]
    public async Task Should_convert_full_text_query_to_filter_if_single_id_found()
    {
        A.CallTo(() => textIndex.SearchAsync(ApiContext.App, A<TextQuery>.That.Matches(x => x.Text == "Hello"), ApiContext.Scope(), CancellationToken))
            .Returns([DomainId.Create("1")]);

        var query = Q.Empty.WithODataQuery("$search=Hello");

        var q = await sut.ParseAsync(ApiContext, query, Schema, CancellationToken);

        Assert.Equal("Filter: id in ['1']; Take: 30; Sort: lastModified Descending, id Ascending", q.Query.ToString());
    }

    [Fact]
    public async Task Should_convert_full_text_query_to_filter_if_index_returns_null()
    {
        A.CallTo(() => textIndex.SearchAsync(ApiContext.App, A<TextQuery>.That.Matches(x => x.Text == "Hello"), ApiContext.Scope(), CancellationToken))
            .Returns(Task.FromResult<List<DomainId>?>(null));

        var query = Q.Empty.WithODataQuery("$search=Hello");

        var q = await sut.ParseAsync(ApiContext, query, Schema, CancellationToken);

        Assert.Equal("Filter: id == '__notfound__'; Take: 30; Sort: lastModified Descending, id Ascending", q.Query.ToString());
    }

    [Fact]
    public async Task Should_convert_full_text_query_to_filter_if_index_returns_empty()
    {
        A.CallTo(() => textIndex.SearchAsync(ApiContext.App, A<TextQuery>.That.Matches(x => x.Text == "Hello"), ApiContext.Scope(), CancellationToken))
            .Returns([]);

        var query = Q.Empty.WithODataQuery("$search=Hello");

        var q = await sut.ParseAsync(ApiContext, query, Schema, CancellationToken);

        Assert.Equal("Filter: id == '__notfound__'; Take: 30; Sort: lastModified Descending, id Ascending", q.Query.ToString());
    }

    [Fact]
    public async Task Should_convert_geo_query_to_filter()
    {
        A.CallTo(() => textIndex.SearchAsync(ApiContext.App, new GeoQuery(SchemaId.Id, "geo.iv", 10, 20, 30, 1000), ApiContext.Scope(), CancellationToken))
            .Returns([DomainId.Create("1"), DomainId.Create("2")]);

        var query = Q.Empty.WithODataQuery("$filter=geo.distance(data/geo/iv, geography'POINT(20 10)') lt 30.0");

        var q = await sut.ParseAsync(ApiContext, query, Schema, CancellationToken);

        Assert.Equal("Filter: id in ['1', '2']; Take: 30; Sort: lastModified Descending, id Ascending", q.Query.ToString());
    }

    [Fact]
    public async Task Should_convert_geo_query_to_filter_if_single_id_found()
    {
        A.CallTo(() => textIndex.SearchAsync(ApiContext.App, new GeoQuery(SchemaId.Id, "geo.iv", 10, 20, 30, 1000), ApiContext.Scope(), CancellationToken))
            .Returns([DomainId.Create("1")]);

        var query = Q.Empty.WithODataQuery("$filter=geo.distance(data/geo/iv, geography'POINT(20 10)') lt 30.0");

        var q = await sut.ParseAsync(ApiContext, query, Schema, CancellationToken);

        Assert.Equal("Filter: id in ['1']; Take: 30; Sort: lastModified Descending, id Ascending", q.Query.ToString());
    }

    [Fact]
    public async Task Should_convert_geo_query_to_filter_if_index_returns_null()
    {
        A.CallTo(() => textIndex.SearchAsync(ApiContext.App, new GeoQuery(SchemaId.Id, "geo.iv", 10, 20, 30, 1000), ApiContext.Scope(), CancellationToken))
            .Returns(Task.FromResult<List<DomainId>?>(null));

        var query = Q.Empty.WithODataQuery("$filter=geo.distance(data/geo/iv, geography'POINT(20 10)') lt 30.0");

        var q = await sut.ParseAsync(ApiContext, query, Schema, CancellationToken);

        Assert.Equal("Filter: id == '__notfound__'; Take: 30; Sort: lastModified Descending, id Ascending", q.Query.ToString());
    }

    [Fact]
    public async Task Should_convert_geo_query_to_filter_if_index_returns_empty()
    {
        A.CallTo(() => textIndex.SearchAsync(ApiContext.App, new GeoQuery(SchemaId.Id, "geo.iv", 10, 20, 30, 1000), ApiContext.Scope(), CancellationToken))
            .Returns([]);

        var query = Q.Empty.WithODataQuery("$filter=geo.distance(data/geo/iv, geography'POINT(20 10)') lt 30.0");

        var q = await sut.ParseAsync(ApiContext, query, Schema, CancellationToken);

        Assert.Equal("Filter: id == '__notfound__'; Take: 30; Sort: lastModified Descending, id Ascending", q.Query.ToString());
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(-1L)]
    [InlineData(long.MaxValue)]
    [InlineData(long.MinValue)]
    public async Task Should_apply_CancellationToken_take_size_if_not_defined(long take)
    {
        var query = Q.Empty.WithQuery(new ClrQuery { Take = take });

        var q = await sut.ParseAsync(ApiContext, query, Schema, CancellationToken);

        Assert.Equal("Take: 30; Sort: lastModified Descending, id Ascending", q.Query.ToString());
    }

    [Fact]
    public async Task Should_set_take_to_ids_count_if_take_not_defined()
    {
        var query = Q.Empty.WithIds("1, 2, 3");

        var q = await sut.ParseAsync(ApiContext, query, Schema, CancellationToken);

        Assert.Equal("Take: 3; Sort: lastModified Descending, id Ascending", q.Query.ToString());
    }

    [Fact]
    public async Task Should_not_set_take_to_ids_count_if_take_defined()
    {
        var query = Q.Empty.WithIds("1, 2, 3").WithQuery(new ClrQuery { Take = 20 });

        var q = await sut.ParseAsync(ApiContext, query, Schema, CancellationToken);

        Assert.Equal("Take: 20; Sort: lastModified Descending, id Ascending", q.Query.ToString());
    }

    [Fact]
    public async Task Should_apply_CancellationToken_take_limit()
    {
        var query = Q.Empty.WithODataQuery("$top=300&$skip=20");

        var q = await sut.ParseAsync(ApiContext, query, Schema, CancellationToken);

        Assert.Equal("Skip: 20; Take: 200; Sort: lastModified Descending, id Ascending", q.Query.ToString());
    }

    [Fact]
    public async Task Should_not_apply_id_ordering_twice()
    {
        var query = Q.Empty.WithODataQuery("$top=300&$skip=20&$orderby=id desc");

        var q = await sut.ParseAsync(ApiContext, query, Schema, CancellationToken);

        Assert.Equal("Skip: 20; Take: 200; Sort: id Descending", q.Query.ToString());
    }

    [Fact]
    public async Task Should_convert_json_query_and_enrich_with_CancellationTokens()
    {
        var query = Q.Empty.WithJsonQuery(
            new Query<JsonValue>
            {
                Filter = new CompareFilter<JsonValue>("data.firstName.iv", CompareOperator.Equals, JsonValue.Create("ABC")),
            });

        var q = await sut.ParseAsync(ApiContext, query, Schema, CancellationToken);

        Assert.Equal("Filter: data.firstName.iv == 'ABC'; Take: 30; Sort: lastModified Descending, id Ascending", q.Query.ToString());
    }
}
