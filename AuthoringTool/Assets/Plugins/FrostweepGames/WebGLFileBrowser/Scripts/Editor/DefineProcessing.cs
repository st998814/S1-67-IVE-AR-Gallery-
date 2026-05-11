namespace FrostweepGames.Plugins.WebGLFileBrowser
{
    [UnityEditor.InitializeOnLoad]
    public class DefineProcessing : Plugins.DefineProcessing
    {
        private static readonly string[] _Defines = new string[] 
        {
            "FG_WEBGLFB"
        };

        static DefineProcessing()
        {
            AddOrRemoveDefines(true, true, _Defines);
        }
    }
}