using KargoApp.Localization;
using Volo.Abp.Application.Services;

namespace KargoApp;

/* Inherit your application services from this class.
 */
public abstract class KargoAppAppService : ApplicationService
{
    protected KargoAppAppService()
    {
        LocalizationResource = typeof(KargoAppResource);
    }
}
