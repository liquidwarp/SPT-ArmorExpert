using BepInEx;

namespace Liquidwarp.ArmorExpert;

[BepInPlugin(MyModInfo.MOD_GUID, MyModInfo.MOD_NAME, MyModInfo.MOD_VERSION)]
[BepInDependency("com.SPT.custom", MyModInfo.CLIENT_SPT_VERSION)]
public class Plugin : BaseUnityPlugin
{
    private void Awake()
    {
        new StaticIconsPatch().Enable();
        new ArmorComponentPatch().Enable();
        new ArmoredEquipmentPatch().Enable();
        new HeadphonesPatch().Enable();
    }
}
