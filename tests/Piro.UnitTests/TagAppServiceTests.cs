using FluentAssertions;
using Piro.Application.DTOs;
using Piro.Application.Interfaces;
using Piro.Application.Services;
using Piro.Domain;
using Piro.Domain.Entities;
using Piro.Domain.Enums;
using Piro.Domain.Exceptions;

namespace Piro.UnitTests;

public class TagAppServiceTests
{
    /// <summary>
    /// In-memory tag repository: a key catalog plus per-entity assignment lists. Enough to exercise the
    /// app service's validation, dedupe, ceiling, and effective-tag inheritance without a database.
    /// </summary>
    private class FakeTagRepository : ITagRepository
    {
        private readonly Dictionary<string, Tag> _catalog = new();
        private int _nextId = 1;

        public HashSet<int> Services { get; } = [];
        public HashSet<int> Checks { get; } = [];
        public HashSet<Guid> Workers { get; } = [];
        public Dictionary<int, int> CheckParents { get; } = [];

        public Dictionary<int, List<ServiceTag>> ServiceTags { get; } = [];
        public Dictionary<int, List<CheckTag>> CheckTags { get; } = [];
        public Dictionary<Guid, List<WorkerTag>> WorkerTags { get; } = [];

        public Tag SeedTag(string key, TagSource source)
        {
            var tag = new Tag { Id = _nextId++, Key = key, Source = source };
            _catalog[key] = tag;
            return tag;
        }

        public Task<Tag?> GetTagByKeyAsync(string key, CancellationToken ct = default) =>
            Task.FromResult(_catalog.GetValueOrDefault(key));

        public Task<Tag> GetOrCreateTagAsync(string key, TagSource source, CancellationToken ct = default)
        {
            if (!_catalog.TryGetValue(key, out var tag))
                tag = SeedTag(key, source);
            return Task.FromResult(tag);
        }

        public Task<bool> ServiceExistsAsync(int serviceId, CancellationToken ct = default) => Task.FromResult(Services.Contains(serviceId));
        public Task<bool> CheckExistsAsync(int checkId, CancellationToken ct = default) => Task.FromResult(Checks.Contains(checkId));
        public Task<bool> WorkerExistsAsync(Guid workerId, CancellationToken ct = default) => Task.FromResult(Workers.Contains(workerId));

        public Task<IReadOnlyList<ServiceTag>> GetServiceTagsAsync(int serviceId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<ServiceTag>>(ServiceTags.GetValueOrDefault(serviceId, []));
        public Task<IReadOnlyList<CheckTag>> GetCheckTagsAsync(int checkId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<CheckTag>>(CheckTags.GetValueOrDefault(checkId, []));
        public Task<IReadOnlyList<WorkerTag>> GetWorkerTagsAsync(Guid workerId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<WorkerTag>>(WorkerTags.GetValueOrDefault(workerId, []));

        public Task<int?> GetParentServiceIdAsync(int checkId, CancellationToken ct = default) =>
            Task.FromResult(CheckParents.TryGetValue(checkId, out var s) ? s : (int?)null);

        public Task ReplaceServiceUserTagsAsync(int serviceId, IReadOnlyList<(Tag Tag, string? Value)> tags, CancellationToken ct = default)
        {
            var list = ServiceTags.GetValueOrDefault(serviceId, []);
            list.RemoveAll(st => st.Tag.Source == TagSource.User);
            list.AddRange(tags.Select(t => new ServiceTag { ServiceId = serviceId, TagId = t.Tag.Id, Value = t.Value, Tag = t.Tag }));
            ServiceTags[serviceId] = list;
            return Task.CompletedTask;
        }

        public Task ReplaceCheckUserTagsAsync(int checkId, IReadOnlyList<(Tag Tag, string? Value)> tags, CancellationToken ct = default)
        {
            var list = CheckTags.GetValueOrDefault(checkId, []);
            list.RemoveAll(ct2 => ct2.Tag.Source == TagSource.User);
            list.AddRange(tags.Select(t => new CheckTag { CheckId = checkId, TagId = t.Tag.Id, Value = t.Value, Tag = t.Tag }));
            CheckTags[checkId] = list;
            return Task.CompletedTask;
        }

        public Task ReplaceWorkerUserTagsAsync(Guid workerId, IReadOnlyList<(Tag Tag, string? Value)> tags, CancellationToken ct = default)
        {
            var list = WorkerTags.GetValueOrDefault(workerId, []);
            list.RemoveAll(wt => wt.Tag.Source == TagSource.User);
            list.AddRange(tags.Select(t => new WorkerTag { WorkerRegistrationId = workerId, TagId = t.Tag.Id, Value = t.Value, Tag = t.Tag }));
            WorkerTags[workerId] = list;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<string>> GetUserKeysAsync(string? prefix, CancellationToken ct = default)
        {
            var keys = _catalog.Values.Where(t => t.Source == TagSource.User)
                .Select(t => t.Key)
                .Where(k => string.IsNullOrEmpty(prefix) || k.StartsWith(prefix))
                .OrderBy(k => k).ToList();
            return Task.FromResult<IReadOnlyList<string>>(keys);
        }

        public Task<IReadOnlyList<string>> GetValuesForKeyAsync(string key, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<string>>([]);
    }

    private static ReplaceTagsRequest Req(params TagDto[] tags) => new(tags);

    [Fact]
    public async Task ReplaceServiceTags_RejectsSystemNamespaceKey()
    {
        var repo = new FakeTagRepository();
        repo.Services.Add(1);
        var svc = new TagAppService(repo);

        var act = () => svc.ReplaceServiceTagsAsync(1, Req(new TagDto("piro:region", "eu")), default);

        await act.Should().ThrowAsync<DomainValidationException>().WithMessage("*reserved*");
    }

    [Theory]
    [InlineData("Tier")]       // uppercase
    [InlineData("1tier")]      // starts with digit
    [InlineData("-tier")]      // starts with dash
    [InlineData("ti er")]      // space
    public async Task ReplaceServiceTags_RejectsMalformedKey(string key)
    {
        var repo = new FakeTagRepository();
        repo.Services.Add(1);
        var svc = new TagAppService(repo);

        var act = () => svc.ReplaceServiceTagsAsync(1, Req(new TagDto(key, null)), default);

        await act.Should().ThrowAsync<DomainValidationException>();
    }

    [Fact]
    public async Task ReplaceServiceTags_RejectsOverlengthKey()
    {
        var repo = new FakeTagRepository();
        repo.Services.Add(1);
        var svc = new TagAppService(repo);
        var longKey = "a" + new string('b', TagConstants.MaxKeyLength);

        var act = () => svc.ReplaceServiceTagsAsync(1, Req(new TagDto(longKey, null)), default);

        await act.Should().ThrowAsync<DomainValidationException>().WithMessage("*maximum length*");
    }

    [Fact]
    public async Task ReplaceServiceTags_RejectsOverCeiling()
    {
        var repo = new FakeTagRepository();
        repo.Services.Add(1);
        var svc = new TagAppService(repo);
        var many = Enumerable.Range(0, TagConstants.MaxTagsPerEntity + 1)
            .Select(i => new TagDto($"key{i}", null)).ToArray();

        var act = () => svc.ReplaceServiceTagsAsync(1, Req(many), default);

        await act.Should().ThrowAsync<DomainValidationException>().WithMessage("*at most*");
    }

    [Fact]
    public async Task ReplaceServiceTags_DedupesByKey_LastWins()
    {
        var repo = new FakeTagRepository();
        repo.Services.Add(1);
        var svc = new TagAppService(repo);

        var result = await svc.ReplaceServiceTagsAsync(1,
            Req(new TagDto("env", "prod"), new TagDto("env", "staging")), default);

        result.Tags.Should().ContainSingle(t => t.Key == "env")
            .Which.Value.Should().Be("staging");
    }

    [Fact]
    public async Task ReplaceServiceTags_AcceptsKeyOnlyTag()
    {
        var repo = new FakeTagRepository();
        repo.Services.Add(1);
        var svc = new TagAppService(repo);

        var result = await svc.ReplaceServiceTagsAsync(1, Req(new TagDto("critical", null)), default);

        result.Tags.Should().ContainSingle(t => t.Key == "critical" && t.Value == null);
    }

    [Fact]
    public async Task GetServiceTags_UnknownService_Throws()
    {
        var svc = new TagAppService(new FakeTagRepository());
        var act = () => svc.GetServiceTagsAsync(999, default);
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task GetCheckTags_EffectiveIncludesInheritedServiceTags()
    {
        var repo = new FakeTagRepository();
        repo.Checks.Add(10);
        repo.Services.Add(1);
        repo.CheckParents[10] = 1;

        var team = repo.SeedTag("team", TagSource.User);
        var env = repo.SeedTag("env", TagSource.User);
        repo.ServiceTags[1] = [
            new ServiceTag { ServiceId = 1, TagId = team.Id, Value = "payments", Tag = team },
            new ServiceTag { ServiceId = 1, TagId = env.Id, Value = "prod", Tag = env },
        ];

        var svc = new TagAppService(repo);
        var result = await svc.GetCheckTagsAsync(10, default);

        result.Own.Should().BeEmpty();
        result.Effective.Should().BeEquivalentTo(new[]
        {
            new TagDto("team", "payments"),
            new TagDto("env", "prod"),
        });
    }

    [Fact]
    public async Task GetCheckTags_OwnTagOverridesInheritedKey()
    {
        var repo = new FakeTagRepository();
        repo.Checks.Add(10);
        repo.Services.Add(1);
        repo.CheckParents[10] = 1;

        var env = repo.SeedTag("env", TagSource.User);
        var team = repo.SeedTag("team", TagSource.User);
        repo.ServiceTags[1] = [
            new ServiceTag { ServiceId = 1, TagId = env.Id, Value = "prod", Tag = env },
            new ServiceTag { ServiceId = 1, TagId = team.Id, Value = "payments", Tag = team },
        ];
        repo.CheckTags[10] = [
            new CheckTag { CheckId = 10, TagId = env.Id, Value = "staging", Tag = env },
        ];

        var svc = new TagAppService(repo);
        var result = await svc.GetCheckTagsAsync(10, default);

        // own env wins; team inherited
        result.Effective.Should().Contain(new TagDto("env", "staging"));
        result.Effective.Should().Contain(new TagDto("team", "payments"));
        result.Effective.Should().NotContain(new TagDto("env", "prod"));
    }

    [Fact]
    public async Task GetCheckTags_UnknownCheck_Throws()
    {
        var svc = new TagAppService(new FakeTagRepository());
        var act = () => svc.GetCheckTagsAsync(404, default);
        await act.Should().ThrowAsync<NotFoundException>();
    }
}
