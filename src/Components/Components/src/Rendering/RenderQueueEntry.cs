// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.AspNetCore.Components.Rendering;

internal readonly struct RenderQueueEntry
{
    public readonly ComponentState ComponentState;
    public readonly RenderFragment RenderFragment;

    public RenderQueueEntry(ComponentState componentState, RenderFragment renderFragment)
    {
        ComponentState = componentState;
        RenderFragment = renderFragment ?? throw new ArgumentNullException(nameof(renderFragment));
    }
}
