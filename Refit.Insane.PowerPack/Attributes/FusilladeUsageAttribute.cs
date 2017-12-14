using System;
namespace Refit.Insane.PowerPack.Attributes
{
    public enum FusilladeHandlerType
    {
        None,
        Speculative,
        Background,
        UserInitiated,
    }

    public class FusilladeUsageAttribute : Attribute
    {
        public FusilladeUsageAttribute(FusilladeHandlerType handlerType)
        {
            HandlerType = handlerType;
        }

        public FusilladeHandlerType HandlerType { get; }
    }
}