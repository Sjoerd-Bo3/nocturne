namespace Nocturne.Core.Contracts;

/// <summary>
/// Describes the write side effects for a specific Nightscout collection.
/// Registered once at startup; resolved by collection name at runtime.
/// </summary>
public interface ICollectionEffectDescriptor
{
    string CollectionName { get; }
    IReadOnlyList<string> GetCacheKeysToRemove(string tenantCacheId);
    IReadOnlyList<string> GetCachePatternsToClear(string tenantCacheId);
    bool DecomposeToV4 { get; }
    bool BroadcastDataUpdateOnCreate { get; }
}
