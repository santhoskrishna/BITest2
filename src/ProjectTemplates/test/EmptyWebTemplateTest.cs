// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using Templates.Test.Helpers;
using Microsoft.AspNetCore.Testing;
using Xunit;
using Xunit.Abstractions;

namespace Templates.Test
{
    public class EmptyWebTemplateTest : LoggedTest
    {
        public EmptyWebTemplateTest(ProjectFactoryFixture projectFactory)
        {
            ProjectFactory = projectFactory;
            Output = new TestOutputLogger(Logger);
        }

        public ProjectFactoryFixture ProjectFactory { get; }

        public ITestOutputHelper Output { get; }

        [ConditionalFact]
        [SkipOnHelix("Cert failures", Queues = "OSX.1014.Amd64;OSX.1014.Amd64.Open")]
        public async Task EmptyWebTemplateCSharp()
        {
            await EmtpyTemplateCore(languageOverride: null);
        }

        [Fact]
        public async Task EmptyWebTemplateFSharp()
        {
            await EmtpyTemplateCore("F#");
        }

        private async Task EmtpyTemplateCore(string languageOverride)
        {
            var project = await ProjectFactory.GetOrCreateProject("empty" + (languageOverride == "F#" ? "fsharp" : "csharp"), Output);

            var createResult = await project.RunDotNetNewAsync("web", language: languageOverride);
            Assert.True(0 == createResult.ExitCode, ErrorMessages.GetFailedProcessMessage("create/restore", project, createResult));

            // Avoid the F# compiler. See https://github.com/dotnet/aspnetcore/issues/14022
            if (languageOverride != null)
            {
                return;
            }

            var publishResult = await project.RunDotNetPublishAsync();
            Assert.True(0 == publishResult.ExitCode, ErrorMessages.GetFailedProcessMessage("publish", project, publishResult));

            // Run dotnet build after publish. The reason is that one uses Config = Debug and the other uses Config = Release
            // The output from publish will go into bin/Release/netcoreappX.Y/publish and won't be affected by calling build
            // later, while the opposite is not true.

            var buildResult = await project.RunDotNetBuildAsync();
            Assert.True(0 == buildResult.ExitCode, ErrorMessages.GetFailedProcessMessage("build", project, buildResult));

            using (var aspNetProcess = project.StartBuiltProjectAsync())
            {
                Assert.False(
                   aspNetProcess.Process.HasExited,
                   ErrorMessages.GetFailedProcessMessageOrEmpty("Run built project", project, aspNetProcess.Process));

                await aspNetProcess.AssertOk("/");
            }

            using (var aspNetProcess = project.StartPublishedProjectAsync())
            {
                Assert.False(
                    aspNetProcess.Process.HasExited,
                    ErrorMessages.GetFailedProcessMessageOrEmpty("Run published project", project, aspNetProcess.Process));

                await aspNetProcess.AssertOk("/");
            }
        }
    }
}
