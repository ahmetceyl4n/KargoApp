using Volo.Abp.Settings;

namespace KargoApp.Settings;

public class KargoAppSettingDefinitionProvider : SettingDefinitionProvider
{
    public override void Define(ISettingDefinitionContext context)
    {
        //Define your own settings here. Example:
        //context.Add(new SettingDefinition(KargoAppSettings.MySetting1));
    }
}
