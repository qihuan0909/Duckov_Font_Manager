namespace DuckovFontManager
{
    /// <summary>
    /// Entry point. The game loads "DuckovFontManager.dll" and instantiates "DuckovFontManager.ModBehaviour".
    /// </summary>
    public class ModBehaviour : Duckov.Modding.ModBehaviour
    {
        protected override void OnAfterSetup()
        {
            FontManagerCore.Init(info.path);
        }

        protected override void OnBeforeDeactivate()
        {
            FontManagerCore.Shutdown();
        }

        private void Update()
        {
            FontManagerCore.Tick();
        }

        private void OnDestroy()
        {
            FontManagerCore.Shutdown();
        }
    }
}
