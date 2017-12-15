using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Net.Http;
using System.Threading.Tasks;
using Refit.Insane.PowerPack.Data;
using Refit.Insane.PowerPack.Attributes;
using Refit.Insane.PowerPack.Configuration;
using Fusillade;

namespace Refit.Insane.PowerPack.Services
{
	public class RefitRestService : IRestService
    {
	    private readonly IDictionary<Type, Func<DelegatingHandler>> _handlerFactories;
	    private readonly IDictionary<Type, DelegatingHandler> _handlerImplementations;
	    private readonly IDictionary<Type, object> _implementations = new Dictionary<Type, object>();
	    private readonly RefitSettings _refitSettings;

	    public RefitRestService(RefitSettings refitSettings) : this()
	    {
		    _refitSettings = refitSettings;
	    }
	    
	    public RefitRestService()
	    {
		    _handlerImplementations = new Dictionary<Type, DelegatingHandler>();
		    _handlerFactories = new Dictionary<Type, Func<DelegatingHandler>>();
	    }

	    public RefitRestService(IDictionary<Type, DelegatingHandler> handlerImplementations, RefitSettings refitSettings) : this(handlerImplementations)
	    {
		    _refitSettings = refitSettings;
	    }
		
	    public RefitRestService(IDictionary<Type, Func<DelegatingHandler>> handlerFactories, RefitSettings refitSettings) : this(handlerFactories)
	    {
		    _handlerImplementations = new Dictionary<Type, DelegatingHandler>();
		    _handlerFactories = handlerFactories;
		    _refitSettings = refitSettings;
	    }

	    public RefitRestService(IDictionary<Type, DelegatingHandler> handlerImplementations)
	    {
		    _handlerImplementations = handlerImplementations;
		    _handlerFactories = new Dictionary<Type, Func<DelegatingHandler>>();
	    }
		
	    public RefitRestService(IDictionary<Type, Func<DelegatingHandler>> handlerFactories)
	    {
		    _handlerImplementations = new Dictionary<Type, DelegatingHandler>();
		    _handlerFactories = handlerFactories;
	    }

		public async Task<Response<TResult>> Execute<TApi, TResult>(Expression<Func<TApi, Task<TResult>>> executeApiMethod)
		{
			var restApi = GetRestApiImplementation<TApi>();

			try
			{
				var responseData = await executeApiMethod.Compile()(restApi).ConfigureAwait(false);
				return new Response<TResult>(responseData);
			}
			catch (ApiException refitApiException)
			{
				if (CanPrepareResponse(refitApiException))
					return GetResponse<TResult>(refitApiException);

				throw;
			}
            catch (HttpRequestException requestException)
            {
                if (CanWrapNetworkException)
                {
                    WrapNetworkException(requestException);
                }

                throw;
            }
		}

		public async Task<Response> Execute<TApi>(Expression<Func<TApi, Task>> executeApiMethod)
		{
			var restApi = GetRestApiImplementation<TApi>();

			try
			{
				await executeApiMethod.Compile()(restApi).ConfigureAwait(false);
				return new Response();
			}
			catch (ApiException refitApiException)
			{
				if (CanPrepareResponse(refitApiException))
					return GetResponse(refitApiException);

				throw;
			}
            catch (HttpRequestException requestException)
            {
                if (CanWrapNetworkException)
                {
                    WrapNetworkException(requestException);
                }

                throw;
            }
		}
	    
	    private TApi GetRestApiImplementation<TApi>()
	    {
		    if (_implementations.ContainsKey(typeof(TApi))) 
			    return (TApi)_implementations[typeof(TApi)];
			
            var httpClientMessageHandler = GetMessageHandler<TApi>();
		    
            var httpClient = new HttpClient(httpClientMessageHandler)
		    {
			    BaseAddress = ApiDefinitionAttributeExtension.GetUri<TApi>(), 
			    Timeout = ApiDefinitionAttributeExtension.GetTimeout<TApi>()
		    };
		    
		    if (httpClient.BaseAddress == null)
			    throw new InvalidOperationException($"HttpClient Base Address has not been set. " +
			                                        $"In case you are not using {nameof(ApiDefinitionAttribute)} on your interface, set global constant in:" +
			                                        $"{nameof(BaseApiConfiguration)} class.");
			    
			
		    var restApi = default(TApi);
		    try
		    {
			    restApi = _refitSettings != null
				    ? RestService.For<TApi>(httpClient, _refitSettings)
				    : RestService.For<TApi>(httpClient);
			    
			    _implementations.Add(typeof(TApi), restApi);
		    }
		    catch (Exception ex)
		    {
			    System.Diagnostics.Debug.WriteLine(ex);
		    }

		    return restApi;
	    }

        private DelegatingHandler GetMessageHandler<TApi>()
        {
            var fusilladeHandlerType = FusilladeAttributeExtensions.GetHandlerType<TApi>();

            if (fusilladeHandlerType != FusilladeHandlerType.None)
            {
                return GetFusilladeHandler(fusilladeHandlerType);
            }

            return GetConfiguredHandler<TApi>();
        }

        private DelegatingHandler GetFusilladeHandler(FusilladeHandlerType handlerType)
        {
            switch (handlerType)
            {
                case FusilladeHandlerType.Speculative:
                    return NetCache.Speculative;
                case FusilladeHandlerType.Background:
                    return (DelegatingHandler)NetCache.Background;
                case FusilladeHandlerType.UserInitiated:
                    return (DelegatingHandler)NetCache.UserInitiated;
                default:
                    throw new Exception("Unknown Fusillade handler type");
            }
        }

	    private DelegatingHandler GetConfiguredHandler<TApi>()
	    {
            var httpClientHandlerType = ApiDefinitionAttributeExtension.GetHttpClientHandlerType<TApi>();

		    var httpClientMessageHandler = default(DelegatingHandler);

		    if (_handlerFactories.ContainsKey(httpClientHandlerType) && !_handlerImplementations.ContainsKey(httpClientHandlerType))
		    {
			    var factory = _handlerFactories[httpClientHandlerType];
			    _handlerImplementations.Add(httpClientHandlerType, factory());
		    }

		    if (_handlerImplementations.ContainsKey(httpClientHandlerType))
			    httpClientMessageHandler = _handlerImplementations[httpClientHandlerType];
		    else
		    {
			    httpClientMessageHandler = Activator.CreateInstance(httpClientHandlerType) as DelegatingHandler;
			    _handlerImplementations.Add(httpClientHandlerType, httpClientMessageHandler);
		    }

		    return httpClientMessageHandler;
	    }

        protected virtual bool CanPrepareResponse(ApiException fromApiException) => false;

        protected virtual bool CanWrapNetworkException => false;

        protected virtual Response GetResponse(ApiException fromApiException) {
            throw new InvalidOperationException($"If you are returning true in CanPrepareResponse method " +
                                                "you have to override GetResponse methods.");
        }

        protected virtual Response<TResult> GetResponse<TResult>(ApiException fromApiException){
			throw new InvalidOperationException($"If you are returning true in CanPrepareResponse method " +
												"you have to override GetResponse methods.");
        } 

        protected virtual void WrapNetworkException(HttpRequestException networkException)
        {
            throw new InvalidOperationException($"If you are returning true in CanHandleNetworkException method " +
                                                "you have to override HandleNetworkException method.");
        }
    }
}
