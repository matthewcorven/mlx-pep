namespace MlxPep.Core.Tests.Python;

using System;
using System.IO;
using Xunit;
using MlxPep.Core.Python;

public class PythonEnvironmentManagerTests
{
    [Fact]
    public void GetModelAssessorRootPath_ReturnsRepoLocalModelAssessorDirectory()
    {
        // Act
        var modelAssessorRoot = PythonEnvironmentManager.GetModelAssessorRootPath();

        // Assert
        Assert.EndsWith(Path.Combine("src", "model-assessor"), modelAssessorRoot.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar));
        Assert.True(Directory.Exists(modelAssessorRoot));
        Assert.True(Directory.Exists(Path.Combine(modelAssessorRoot, "scripts")));
    }
}
