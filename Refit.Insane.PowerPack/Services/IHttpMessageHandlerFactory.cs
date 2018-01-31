using System.Net.Http;

namespace Refit.Insane.PowerPack.Services
{
    public interface IHttpMessageHandlerFactory
    {
        HttpMessageHandler Create();
    }
}
