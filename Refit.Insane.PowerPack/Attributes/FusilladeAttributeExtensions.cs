using System.Linq;
using System.Reflection;

namespace Refit.Insane.PowerPack.Attributes
{
    public static class FusilladeAttributeExtensions
    {
        public static FusilladeHandlerType GetHandlerType<TApi>()
        {
            var attribute = GetAttribute<TApi>();
            return attribute != null ? attribute.HandlerType : FusilladeHandlerType.None;
        }

        private static FusilladeUsageAttribute GetAttribute<TApi>()
        {
            var attrs = typeof(TApi).GetTypeInfo().GetCustomAttributes(typeof(FusilladeUsageAttribute));
            return attrs.FirstOrDefault(attr => attr is FusilladeUsageAttribute) as FusilladeUsageAttribute;
        }
    }
}