﻿// Copyright(c) .NET Foundation.All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using TriageBuildFailures.Abstractions;
using TriageBuildFailures.GitHub;

namespace TriageBuildFailures.VSTS.Models
{
    public class VSTSBuild : ICIBuild
    {
        private readonly Build _build;
        private string _branch;

        public VSTSBuild(Build build)
        {
            _build = build;
        }

        public Type CIType { get; set; } = typeof(VSTSBuildClient);

        public string Id => _build.Id;

        public string BuildTypeID => _build.Definition.Id;

        public string BuildName => _build.Definition.Name;

        public BuildStatus Status
        {
            get
            {
                switch (_build.Result)
                {
                    case VSTSBuildResult.Canceled:
                        return BuildStatus.UNKNOWN;
                    case VSTSBuildResult.Failed:
                        return BuildStatus.FAILURE;
                    case VSTSBuildResult.Succeeded:
                        return BuildStatus.SUCCESS;
                    case VSTSBuildResult.PartiallySucceeded:
                    default:
                        throw new NotImplementedException($"VSTS had an unknown build result '{_build.Result}'!");
                }
            }
        }

        public string Project => _build.Project.Id;

        public string Branch
        {
            get
            {
                return string.IsNullOrEmpty(_branch) ? _build.SourceBranch.Replace("refs/heads/", string.Empty) : _branch;
            }
            set
            {
                _branch = value;
            }
        }

        public DateTimeOffset? StartDate => _build.StartTime.HasValue? new DateTimeOffset(_build.StartTime.Value) : (DateTimeOffset?)null;

        public Uri Uri => _build.Uri;

        public Uri WebURL => _build._Links.Web.Href;

        public CIConfigBase GetCIConfig(Config config)
        {
            return config.VSTS;
        }

        public GitHubPR PRSource { get; set; }

        public IEnumerable<ValidationResults> ValidationResults => _build.ValidationResults;
    }

    public class Build
    {
        public string Id { get; set; }
        public BuildDefinition Definition { get; set; }
        public DateTime? StartTime { get; set; }
        public Uri Uri { get; set; }
        public string SourceBranch { get; set; }
        public VSTSProject Project { get; set; }
        public VSTSBuildResult Result { get; set; }
        public IDictionary<string, string> TriggerInfo { get; set; }

        public Links _Links { get; set; }
        public IEnumerable<ValidationResults> ValidationResults { get; set; }
    }

    public class ValidationResults
    {
        public string Result { get; set; }
        public string Message { get; set; }
    }

    public class Links
    {
        public Link Self { get; set; }
        public Link Web { get; set; }
        public Link Badge { get; set; }
    }

    public class Link
    {
        public Uri Href { get; set; }
    }

    public class BuildDefinition
    {
        public string Name { get; set; }
        public string Id { get; set; }
        public string Path { get; set; }
    }
}
