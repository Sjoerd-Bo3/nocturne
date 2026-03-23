using Nocturne.Core.Contracts;

namespace Nocturne.API.Services.Effects;

public class DeviceStatusEffectDescriptor : ICollectionEffectDescriptor
{
    public string CollectionName => "devicestatus";
    public IReadOnlyList<string> GetCacheKeysToRemove(string tid) => [$"devicestatus:current:{tid}"];
    public IReadOnlyList<string> GetCachePatternsToClear(string tid) => [];
    public bool DecomposeToV4 => true;
    public bool BroadcastDataUpdateOnCreate => false;
}
