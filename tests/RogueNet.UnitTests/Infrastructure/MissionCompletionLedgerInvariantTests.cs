using System.Text.RegularExpressions;

using FluentAssertions;

namespace RogueNet.UnitTests.Infrastructure;

// Static-source guardrail: no UPDATE or DELETE may target InventoryTransactions
// inside any repository class. The integration tests verify that replaying a
// duplicate mission completion does not write a second ledger row, but they
// can't catch a future refactor that "fixes" the ledger by mutating existing
// rows in a single-shot path that the duplicate test doesn't exercise. This
// test makes that refactor a compile-style failure instead.
public class MissionCompletionLedgerInvariantTests
{
    private static readonly string RepositorySources = ReadAllRepositorySources();

    [Fact]
    public void Repositories_StillWriteToInventoryLedger()
    {
        // Sanity: if this fails, the test is no longer guarding anything because the
        // ledger writes have moved out of src/RogueNet.Infrastructure/Repositories.
        var insertPattern = new Regex(@"\bINSERT\s+INTO\s+InventoryTransactions\b", RegexOptions.IgnoreCase);
        insertPattern.IsMatch(RepositorySources).Should().BeTrue(
            "the inventory ledger should still be written through the Repositories folder");
    }

    [Fact]
    public void Repositories_ContainNoUpdateAgainstInventoryTransactions()
    {
        var updatePattern = new Regex(@"\bUPDATE\s+InventoryTransactions\b", RegexOptions.IgnoreCase);
        updatePattern.IsMatch(RepositorySources).Should().BeFalse(
            "InventoryTransactions is an append-only ledger; mutating an existing row breaks the audit guarantee");
    }

    [Fact]
    public void Repositories_ContainNoDeleteAgainstInventoryTransactions()
    {
        var deletePattern = new Regex(@"\bDELETE\s+(?:FROM\s+)?InventoryTransactions\b", RegexOptions.IgnoreCase);
        deletePattern.IsMatch(RepositorySources).Should().BeFalse(
            "InventoryTransactions is an append-only ledger; rows are never removed");
    }

    private static string ReadAllRepositorySources()
    {
        var repoRoot = FindRepoRoot();
        var repositoriesDir = Path.Combine(
            repoRoot,
            "src",
            "RogueNet.Infrastructure",
            "Repositories");

        if (!Directory.Exists(repositoriesDir))
        {
            throw new InvalidOperationException(
                $"Repositories directory not found at {repositoriesDir}. Has the layout changed?");
        }

        var files = Directory.EnumerateFiles(repositoriesDir, "*.cs", SearchOption.AllDirectories);
        return string.Join("\n", files.Select(File.ReadAllText));
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && dir.GetFiles("*.sln").Length == 0)
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException(
            "Could not locate repository root (no .sln found walking up from the test bin directory)");
    }
}
