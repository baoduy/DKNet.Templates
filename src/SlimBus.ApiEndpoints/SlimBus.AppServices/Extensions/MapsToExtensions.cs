using System.Reflection;
using DKNet.Fw.Extensions.TypeExtractors;

namespace SlimBus.AppServices.Extensions;

internal static class MapsToExtensions
{
    #region Methods

    public static void ScanMaps(this TypeAdapterConfig config)
    {
        var mapsToTypes = typeof(MapsToExtensions).Assembly
            .Extract().Classes().NotAbstract().NotGeneric().HasAttribute<MapsFromAttribute>();

        foreach (var type in mapsToTypes)
        {
            var attribute = type.GetCustomAttribute<MapsFromAttribute>();
            if (attribute == null)
            {
                continue;
            }

            //var ctor = attribute.EntityType.GetConstructors().First(c => c.IsPublic);
            // config.NewConfig(type, attribute.EntityType)
            //     .PreserveReference(true)
            //     .Settings.MapToConstructor = ctor;
            config.NewConfig(attribute.EntityType, type);
        }
    }

    #endregion
}