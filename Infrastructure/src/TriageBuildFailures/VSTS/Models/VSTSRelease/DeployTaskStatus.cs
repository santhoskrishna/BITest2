﻿// Copyright(c) .NET Foundation.All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace TriageBuildFailures.VSTS.Models
{
    public enum DeployTaskStatus
    {
        Canceled,
        Failed,
        Failure,
        InProgress,
        PartiallySucceeded,
        Pending,
        Skipped,
        Succeeded,
        Success,
        Unknown
    }
}
