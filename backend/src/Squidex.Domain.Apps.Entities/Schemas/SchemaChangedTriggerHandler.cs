﻿// ==========================================================================
//  Squidex Headless CMS
// ==========================================================================
//  Copyright (c) Squidex UG (haftungsbeschraenkt)
//  All rights reserved. Licensed under the MIT license.
// ==========================================================================

using System.Runtime.CompilerServices;
using Squidex.Domain.Apps.Core.HandleRules;
using Squidex.Domain.Apps.Core.Rules;
using Squidex.Domain.Apps.Core.Rules.EnrichedEvents;
using Squidex.Domain.Apps.Core.Rules.Triggers;
using Squidex.Domain.Apps.Core.Scripting;
using Squidex.Domain.Apps.Events;
using Squidex.Domain.Apps.Events.Schemas;
using Squidex.Infrastructure.EventSourcing;
using Squidex.Infrastructure.Reflection;

namespace Squidex.Domain.Apps.Entities.Schemas;

public sealed class SchemaChangedTriggerHandler(IScriptEngine scriptEngine) : IRuleTriggerHandler
{
    public Type TriggerType => typeof(SchemaChangedTrigger);

    public bool Handles(AppEvent appEvent)
    {
        return appEvent is SchemaEvent;
    }

    public async IAsyncEnumerable<EnrichedEvent> CreateEnrichedEventsAsync(Envelope<AppEvent> @event, RulesContext context,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var result = new EnrichedSchemaEvent();

        // Use the concrete event to map properties that are not part of app event.
        SimpleMapper.Map((SchemaEvent)@event.Payload, result);

        switch (@event.Payload)
        {
            case FieldEvent:
            case SchemaPreviewUrlsConfigured:
            case SchemaScriptsConfigured:
            case SchemaUpdated:
            case ParentFieldEvent:
                result.Type = EnrichedSchemaEventType.Updated;
                break;
            case SchemaCreated:
                result.Type = EnrichedSchemaEventType.Created;
                break;
            case SchemaPublished:
                result.Type = EnrichedSchemaEventType.Published;
                break;
            case SchemaUnpublished:
                result.Type = EnrichedSchemaEventType.Unpublished;
                break;
            case SchemaDeleted:
                result.Type = EnrichedSchemaEventType.Deleted;
                break;
            default:
                yield break;
        }

        await Task.Yield();

        yield return result;
    }

    public bool Trigger(EnrichedEvent @event, RuleTrigger trigger)
    {
        var schemaTrigger = (SchemaChangedTrigger)trigger;

        if (string.IsNullOrWhiteSpace(schemaTrigger.Condition))
        {
            return true;
        }

        // Script vars are just wrappers over dictionaries for better performance.
        var vars = new EventScriptVars
        {
            ["event"] = @event,
        };

        return scriptEngine.Evaluate(vars, schemaTrigger.Condition);
    }
}
