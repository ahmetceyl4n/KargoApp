using KargoApp.Localization;
using Volo.Abp.AspNetCore.Mvc;

namespace KargoApp.Controllers;

/* Inherit your controllers from this class.
 */
public abstract class KargoAppController : AbpControllerBase
{
    protected KargoAppController()
    {
        LocalizationResource = typeof(KargoAppResource);
    }
}
