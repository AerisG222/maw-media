using System.IO.Compression;
using System.Net;
using Dapper;
using MawMedia;
using MawMedia.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace MawMedia.Services.Tests.Api;

// the category zip download, and what it may hand back from its cache.
//
// a built archive is kept on disk for hours and reused, so the danger is serving
// one that no longer matches what the caller may see: a caller who loses access
// to a category must not keep downloading the archive built while they had it.
// these tests download, change what the caller may see, and download again.
//
// every subject is private - its own user, role, category and files - so nothing
// here is visible to the rest of the suite.
public class CategoryDownloadTests
    : ApiTestBase
{
    readonly TestFixture _fixture;

    public CategoryDownloadTests(TestFixture fixture)
        : base(fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task ACallerWhoLosesAccessCannotDownloadTheCachedZip()
    {
        var token = TestContext.Current.CancellationToken;
        var s = await CreateSubject(2);

        using var client = Client(s.ExternalId, ApiScopes.MediaRead);

        // built and cached while the caller has access
        Assert.Equal(["m0.avif", "m1.avif"], await Entries(await client.GetAsync(Download(s), token), token));

        await Sql("DELETE FROM media.category_role WHERE category_id = @category", new { category = s.Category });

        // the archive is still on disk, but must not be served
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(Download(s), token)).StatusCode);
    }

    [Fact]
    public async Task TheZipFollowsWhatTheCallerMaySeeNow()
    {
        var token = TestContext.Current.CancellationToken;
        var s = await CreateSubject(2);

        using var client = Client(s.ExternalId, ApiScopes.MediaRead);

        Assert.Equal(["m0.avif", "m1.avif"], await Entries(await client.GetAsync(Download(s), token), token));

        // a media leaves the category: the next download is rebuilt without it
        // rather than answered with the archive cached before the change
        await Sql("DELETE FROM media.category_media WHERE category_id = @category AND media_id = @media",
            new { category = s.Category, media = s.Media[1] });

        Assert.Equal(["m0.avif"], await Entries(await client.GetAsync(Download(s), token), token));
    }

    [Fact]
    public async Task RepeatDownloadsOfAnUnchangedCategoryReuseOneZip()
    {
        var token = TestContext.Current.CancellationToken;
        var s = await CreateSubject(2);

        using var client = Client(s.ExternalId, ApiScopes.MediaRead);

        await client.GetAsync(Download(s), token);
        await client.GetAsync(Download(s), token);

        // keyed on content, so an unchanged category is still built only once
        Assert.Single(Directory.EnumerateFiles(DownloadDir(), $"{s.User}-{s.Category}-*.zip"));
    }

    [Fact]
    public async Task ACallerWhoBuildsTooManyZipsIsToldWhenToComeBack()
    {
        var token = TestContext.Current.CancellationToken;
        var limit = Factory.Services.GetRequiredService<IOptions<CategoryDownloadConfig>>().Value.MaxBuildsPerUserPerHour;
        var s = await CreateSubject(limit + 1);

        using var client = Client(s.ExternalId, ApiScopes.MediaRead);

        // every download a distinct set of files, so every one is a build
        for (var i = 0; i < limit; i++)
        {
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(Download(s), token)).StatusCode);

            await Sql("DELETE FROM media.category_media WHERE category_id = @category AND media_id = @media",
                new { category = s.Category, media = s.Media[i] });
        }

        var refused = await client.GetAsync(Download(s), token);

        Assert.Equal(HttpStatusCode.TooManyRequests, refused.StatusCode);
        Assert.True(refused.Headers.RetryAfter?.Delta > TimeSpan.Zero);

        // but an archive already built is still served: put back the media the last
        // build had, and that archive is what comes back
        await Sql(
            """
            INSERT INTO media.category_media (category_id, media_id, slug, is_teaser, created, created_by, modified, modified_by)
            VALUES (@category, @media, @slug, false, NOW(), @createdBy, NOW(), @createdBy)
            """,
            new { category = s.Category, media = s.Media[limit - 1], slug = $"m{limit - 1}", createdBy = Constants.USER_ADMIN });

        Assert.Equal(new[] { $"m{limit - 1}.avif", $"m{limit}.avif" }.Order(StringComparer.Ordinal), await Entries(await client.GetAsync(Download(s), token), token));
    }

    // a user, a role granted a private category, and `count` media in it, each
    // with a full rendition on disk
    async Task<Subject> CreateSubject(int count)
    {
        var tag = Guid.CreateVersion7().ToString("N")[^8..];
        var s = new Subject(
            Guid.CreateVersion7(), $"download-{tag}", Guid.CreateVersion7(),
            [.. Enumerable.Range(0, count).Select(_ => Guid.CreateVersion7())]);

        await using var conn = _fixture.SetupDataSource.CreateConnection();

        await conn.ExecuteAsync(
            """
            INSERT INTO media.user (id, created, modified, name, email)
            VALUES (@user, NOW(), NOW(), @tag, @tag || '@example.test');

            INSERT INTO media.external_identity (external_id, user_id, created, modified, name, email, email_verified)
            VALUES (@externalId, @user, NOW(), NOW(), @tag, @tag || '@example.test', true);

            INSERT INTO media.role (id, name, created, created_by) VALUES (@role, @tag, NOW(), @createdBy);
            INSERT INTO media.user_role (user_id, role_id, created, created_by) VALUES (@user, @role, NOW(), @createdBy);

            INSERT INTO media.category (id, name, slug, effective_date, created, created_by, modified, modified_by)
            VALUES (@category, @tag, @tag, '2001-01-01', NOW(), @createdBy, NOW(), @createdBy);

            INSERT INTO media.category_role (category_id, role_id, created, created_by)
            VALUES (@category, @role, NOW(), @createdBy);
            """,
            new { tag, user = s.User, externalId = s.ExternalId, role = Guid.CreateVersion7(), category = s.Category, createdBy = Constants.USER_ADMIN });

        for (var i = 0; i < count; i++)
        {
            var path = $"/assets/2001/{s.ExternalId}/full/m{i}.avif";

            await conn.ExecuteAsync(
                """
                INSERT INTO media.media (id, type_id, created, created_by, modified, modified_by)
                VALUES (@media, @photo, NOW() + make_interval(secs => @i), @createdBy, NOW(), @createdBy);

                INSERT INTO media.category_media (category_id, media_id, slug, is_teaser, created, created_by, modified, modified_by)
                VALUES (@category, @media, @slug, @teaser, NOW(), @createdBy, NOW(), @createdBy);

                INSERT INTO media.file (id, media_id, type_id, scale_id, width, height, bytes, path)
                VALUES (gen_random_uuid(), @media, @photo, @scale, 1, 1, 1, @path);
                """,
                new { media = s.Media[i], i, slug = $"m{i}", teaser = i == 0, category = s.Category, photo = Constants.TYPE_PHOTO, scale = Constants.SCALE_FULL, path, createdBy = Constants.USER_ADMIN });

            var file = Path.Combine(ApiFactory.AssetRoot, AssetPathBuilder.ToRelativeFilePath(path));

            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            await File.WriteAllBytesAsync(file, ApiFactory.StubRendition(path), TestContext.Current.CancellationToken);
        }

        return s;
    }

    async Task Sql(string sql, object args)
    {
        await using var conn = _fixture.SetupDataSource.CreateConnection();

        await conn.ExecuteAsync(sql, args);
    }

    static async Task<string[]> Entries(HttpResponseMessage response, CancellationToken token)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await using var stream = await response.Content.ReadAsStreamAsync(token);
        using var zip = new ZipArchive(stream, ZipArchiveMode.Read);

        return [.. zip.Entries.Select(e => e.Name).Order(StringComparer.Ordinal)];
    }

    string DownloadDir() =>
        Factory.Services.GetRequiredService<IOptions<CategoryDownloadConfig>>().Value.RootDirectory;

    static string Download(Subject s) => $"/api/v1/categories/{s.Category}/download";

    record Subject(Guid User, string ExternalId, Guid Category, Guid[] Media);
}
