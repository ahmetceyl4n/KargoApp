using Microsoft.Extensions.Localization;
using KargoApp.Localization;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Ui.Branding;

namespace KargoApp;

[Dependency(ReplaceServices = true)]
public class KargoAppBrandingProvider : DefaultBrandingProvider
{
    private IStringLocalizer<KargoAppResource> _localizer;

    public KargoAppBrandingProvider(IStringLocalizer<KargoAppResource> localizer)
    {
        _localizer = localizer;
    }

    public override string AppName => _localizer["AppName"];

    public override string? LogoUrl => "~/images/logo/leptonxlite/logo-light-thumbnail.png";

    public override string? LogoIconUrl => "~/images/logo/leptonxlite/logo-dark-thumbnail.png";
}
