using System;
using System.Linq.Expressions;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Refit.Insane.PowerPack.Caching;
using Refit.Insane.PowerPack.Caching.Internal;
using Refit.Insane.PowerPack.Data;

namespace Refit.Insane.PowerPack.Services
{
    public class RefitRestServiceCachingDecorator : IRestService
    {
        private readonly IRestService _decoratedRestService;
        private readonly RefitCacheController _refitCacheController;

        public RefitRestServiceCachingDecorator(IRestService decoratedRestService, RefitCacheController cacheController)
            : this(decoratedRestService, new AkavachePersistedCache(), cacheController)
        {

        }

        readonly IPersistedCache persistedCache;

        public RefitRestServiceCachingDecorator(IRestService decoratedRestService, IPersistedCache persistedCache, RefitCacheController refitCacheController)
        {
            this.persistedCache = persistedCache;
            _decoratedRestService = decoratedRestService;
            _refitCacheController = refitCacheController;
        }

        public async Task<Response<TResult>> Execute<TApi, TResult>(Expression<Func<TApi, Task<TResult>>> executeApiMethod, bool invalidateCache = false)
        {
            if (!_refitCacheController.IsMethodCacheable(executeApiMethod))
                return await _decoratedRestService.Execute(executeApiMethod, invalidateCache).ConfigureAwait(false);

            var cacheKey = _refitCacheController.GetCacheKey(executeApiMethod);
            if (invalidateCache)
            {
                await persistedCache.Delete(cacheKey);
            }
            else
            {
                var cachedValue = await persistedCache.Get<TResult>(cacheKey);
                if (cachedValue != null)
                {
                    return new Response<TResult>(cachedValue);
                }
            }

            var restResponse = await _decoratedRestService.Execute(executeApiMethod);

            if (restResponse.IsSuccess)
            {
                var refitCacheAttributes = _refitCacheController.GetRefitCacheAttribute<TApi, TResult>(executeApiMethod);

                await persistedCache.Save(cacheKey, restResponse.Results, refitCacheAttributes.CacheAttribute.CacheTtl);
            }

            return restResponse;
        }

        public Task<Response> Execute<TApi>(Expression<Func<TApi, Task>> executeApiMethod, bool invalidateCache = false) => _decoratedRestService.Execute(executeApiMethod, invalidateCache);


    }
}
