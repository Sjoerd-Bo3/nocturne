
namespace Nocturne.API.Services.Effects;

/// <summary>
/// <see cref="ICollectionEffectDescriptor"/> for the <c>food</c> collection.
/// No cache invalidation is performed on food writes; v4 decomposition is not applicable.
/// </summary>
/// <seealso cref="ICollectionEffectDescriptor"/>
public class FoodEffectDescriptor : ICollectionEffectDescriptor
{
    public string CollectionName => "food";
    public IReadOnlyList<string> GetCacheKeysToRemove(string tid) => [];
    public IReadOnlyList<string> GetCachePatternsToClear(string tid) => [];
    public bool DecomposeToV4 => false;
    public bool BroadcastDataUpdateOnCreate => false;
}
