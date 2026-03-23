using Nocturne.Core.Contracts;

namespace Nocturne.API.Services.Effects;

public class FoodEffectDescriptor : ICollectionEffectDescriptor
{
    public string CollectionName => "food";
    public IReadOnlyList<string> GetCacheKeysToRemove(string tid) => [];
    public IReadOnlyList<string> GetCachePatternsToClear(string tid) => [];
    public bool DecomposeToV4 => false;
    public bool BroadcastDataUpdateOnCreate => false;
}
