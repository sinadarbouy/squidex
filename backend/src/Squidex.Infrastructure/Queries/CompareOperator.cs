﻿// ==========================================================================
//  Squidex Headless CMS
// ==========================================================================
//  Copyright (c) Squidex UG (haftungsbeschraenkt)
//  All rights reserved. Licensed under the MIT license.
// ==========================================================================

using System.ComponentModel;

namespace Squidex.Infrastructure.Queries;

[TypeConverter(typeof(CompareOperatorTypeConverter))]
public enum CompareOperator
{
    Contains,
    Empty,
    Exists,
    EndsWith,
    Equals,
    GreaterThan,
    GreaterThanOrEqual,
    In,
    LessThan,
    LessThanOrEqual,
    Matchs,
    NotEquals,
    StartsWith,
}
