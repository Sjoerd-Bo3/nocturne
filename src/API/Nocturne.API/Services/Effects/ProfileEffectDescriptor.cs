using Nocturne.Core.Contracts;
using Nocturne.Infrastructure.Cache.Keys;

namespace Nocturne.API.Services.Effects;

public class ProfileEffectDescriptor : ICollectionEffectDescriptor
{
    public string CollectionName => "profiles";
    public IReadOnlyList<string> GetCacheKeysToRemove(string tid) => [CacheKeyBuilder.BuildCurrentProfileKey(tid)];
    public IReadOnlyList<string> GetCachePatternsToClear(string tid) => [CacheKeyBuilder.BuildProfileTimestampPattern(tid)];
    public bool DecomposeToV4 => false;
    public bool BroadcastDataUpdateOnCreate => false;
}
