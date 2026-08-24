using System.IO;

namespace ReactorSim.TestInfrastructure;

/// <summary>
/// Resolves test-declared assets from the test output directory.
/// </summary>
public static class TestDataLocator
{
    private const string TestDataDirectory = "TestData";

    public static string RequireRepositoryFile(
        string repositoryRelativePath,
        string? assetDescription = null)
    {
        if (string.IsNullOrWhiteSpace(repositoryRelativePath))
        {
            throw new ArgumentException(
                "A repository-relative test asset path is required.",
                nameof(repositoryRelativePath));
        }

        if (Path.IsPathRooted(repositoryRelativePath))
        {
            throw new ArgumentException(
                "Repository test asset paths must be relative.",
                nameof(repositoryRelativePath));
        }

        string normalizedPath = repositoryRelativePath.Replace(
            '/',
            Path.DirectorySeparatorChar);
        string outputRelativePath = Path.Combine(TestDataDirectory, normalizedPath);
        return RequireFile(
            outputRelativePath,
            assetDescription ?? $"repository test asset '{repositoryRelativePath}'");
    }

    public static string RequireFile(
        string outputRelativePath,
        string? assetDescription = null)
    {
        if (string.IsNullOrWhiteSpace(outputRelativePath))
        {
            throw new ArgumentException(
                "A relative test output path is required.",
                nameof(outputRelativePath));
        }

        if (Path.IsPathRooted(outputRelativePath))
        {
            throw new ArgumentException(
                "Test output asset paths must be relative to AppContext.BaseDirectory.",
                nameof(outputRelativePath));
        }

        string outputDirectory = Path.GetFullPath(AppContext.BaseDirectory);
        string candidatePath = Path.GetFullPath(Path.Combine(outputDirectory, outputRelativePath));
        if (!IsWithinDirectory(outputDirectory, candidatePath))
        {
            throw new ArgumentException(
                $"Test output asset path '{outputRelativePath}' escapes AppContext.BaseDirectory.",
                nameof(outputRelativePath));
        }

        if (File.Exists(candidatePath))
        {
            return candidatePath;
        }

        string description = string.IsNullOrWhiteSpace(assetDescription)
            ? "declared test asset"
            : assetDescription;
        throw new FileNotFoundException(
            $"The {description} at output path '{outputRelativePath}' was not found. " +
            $"AppContext.BaseDirectory='{outputDirectory}'. " +
            "Declare the asset in the test project and set CopyToOutputDirectory " +
            "so artifact-output test runs remain portable.",
            candidatePath);
    }

    private static bool IsWithinDirectory(string directory, string candidate)
    {
        string relativePath = Path.GetRelativePath(directory, candidate);
        return relativePath != ".." &&
            !relativePath.StartsWith(
                ".." + Path.DirectorySeparatorChar,
                StringComparison.Ordinal) &&
            !Path.IsPathRooted(relativePath);
    }
}
