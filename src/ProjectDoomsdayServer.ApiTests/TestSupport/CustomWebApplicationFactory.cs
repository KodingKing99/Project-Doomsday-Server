using System.Collections.Concurrent;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using ProjectDoomsdayServer.Application.Files;
using ProjectDoomsdayServer.Domain.Files;

namespace ProjectDoomsdayServer.ApiTests.TestSupport;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    public IFileStorage? FileStorageSubstitute { get; private set; }
    public IFileRepository? FileRepositorySubstitute { get; private set; }

    // Track operations for assertions
    public ConcurrentDictionary<Guid, byte[]> SavedFiles { get; } = new();
    public ConcurrentBag<Guid> DeletedFileIds { get; } = new();
    public ConcurrentDictionary<Guid, FileRecord> FileRecords { get; } = new();

    private static void RemoveService<T>(IServiceCollection services)
    {
        var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(T));
        if (descriptor != null)
            services.Remove(descriptor);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureTestServices(services =>
        {
            RemoveService<IFileStorage>(services);
            RemoveService<IFileRepository>(services);

            // Configure IFileStorage substitute
            var storageSub = Substitute.For<IFileStorage>();

            storageSub
                .GetPresignedUploadUrlAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(call => Task.FromResult("https://example.com/presigned-url"));

            storageSub
                .SaveAsync(Arg.Any<Guid>(), Arg.Any<Stream>(), Arg.Any<CancellationToken>())
                .Returns(callInfo =>
                {
                    var id = callInfo.ArgAt<Guid>(0);
                    var stream = callInfo.ArgAt<Stream>(1);
                    using var ms = new MemoryStream();
                    stream.CopyTo(ms);
                    SavedFiles[id] = ms.ToArray();
                    return Task.CompletedTask;
                });

            storageSub
                .OpenReadAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
                .Returns(callInfo =>
                {
                    var id = callInfo.ArgAt<Guid>(0);
                    // Return saved content if exists, otherwise return dummy content
                    // (simulates client having uploaded directly to S3)
                    if (SavedFiles.TryGetValue(id, out var content))
                        return Task.FromResult<Stream>(new MemoryStream(content));
                    return Task.FromResult<Stream>(new MemoryStream("dummy content"u8.ToArray()));
                });

            storageSub
                .DeleteAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
                .Returns(callInfo =>
                {
                    var id = callInfo.ArgAt<Guid>(0);
                    DeletedFileIds.Add(id);
                    SavedFiles.TryRemove(id, out _);
                    return Task.CompletedTask;
                });

            storageSub
                .ExistsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
                .Returns(callInfo =>
                {
                    var id = callInfo.ArgAt<Guid>(0);
                    return Task.FromResult(SavedFiles.ContainsKey(id));
                });

            FileStorageSubstitute = storageSub;
            services.AddSingleton(storageSub);

            // Configure IFileRepository substitute
            var repoSub = Substitute.For<IFileRepository>();

            repoSub
                .GetAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
                .Returns(callInfo =>
                {
                    var id = callInfo.ArgAt<Guid>(0);
                    FileRecords.TryGetValue(id, out var rec);
                    return Task.FromResult<FileRecord?>(rec);
                });

            repoSub
                .ListAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
                .Returns(callInfo =>
                {
                    var skip = callInfo.ArgAt<int>(0);
                    var take = callInfo.ArgAt<int>(1);
                    var list = FileRecords
                        .Values.OrderByDescending(f => f.UpdatedAtUtc)
                        .Skip(skip)
                        .Take(take)
                        .ToList();
                    return Task.FromResult<IReadOnlyList<FileRecord>>(list);
                });

            repoSub
                .UpsertAsync(Arg.Any<FileRecord>(), Arg.Any<CancellationToken>())
                .Returns(callInfo =>
                {
                    var record = callInfo.ArgAt<FileRecord>(0);
                    FileRecords[record.Id] = record;
                    return Task.CompletedTask;
                });

            repoSub
                .DeleteAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
                .Returns(callInfo =>
                {
                    var id = callInfo.ArgAt<Guid>(0);
                    FileRecords.TryRemove(id, out _);
                    return Task.CompletedTask;
                });

            FileRepositorySubstitute = repoSub;
            services.AddSingleton(repoSub);
        });
    }

    public void Reset()
    {
        SavedFiles.Clear();
        DeletedFileIds.Clear();
        FileRecords.Clear();
    }
}
