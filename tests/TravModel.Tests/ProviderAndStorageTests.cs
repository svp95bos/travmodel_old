using System.Text;
using System.Globalization;
using TravModel.Infrastructure;

namespace TravModel.Tests;

public sealed class ProviderAndStorageTests
{
    [Fact]
    public async Task SvenskTravsportProvider_RequiresExplicitAuthorization()
    {
        var root = CreateTempDirectory();
        try
        {
            var provider = new SvenskTravsportProvider(new HttpClient(), new ProviderAccessOptions(), new RawArtifactStore(root));
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                provider.GetMeetingsAsync(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 2), CancellationToken.None));
            Assert.Contains("authorization", exception.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task RawArtifactStore_IsContentAddressedAndIdempotent()
    {
        var root = CreateTempDirectory();
        try
        {
            var store = new RawArtifactStore(root);
            var content = Encoding.UTF8.GetBytes("{\"race\":1}");
            var first = await store.StoreAsync("ATG", DateTimeOffset.Parse("2026-09-12T12:00:00Z", CultureInfo.InvariantCulture), content, CancellationToken.None);
            var second = await store.StoreAsync("ATG", DateTimeOffset.Parse("2026-09-12T12:00:00Z", CultureInfo.InvariantCulture), content, CancellationToken.None);

            Assert.Equal(first, second);
            Assert.True(File.Exists(Path.Combine(root, first.RelativePath.Replace('/', Path.DirectorySeparatorChar))));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"travmodel-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
