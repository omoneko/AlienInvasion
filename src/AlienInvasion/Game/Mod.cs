using System.IO;
using System.Reflection;
using ICities;

namespace AlienInvasion.Game
{
    public class Mod : IUserMod
    {
        public string Name => "Alien Invasion";
        public string Description => "UFO母船が飛来し、雷とクレーターで街を破壊、放射能汚染を残します。手動発動キー: F7";

        public void OnEnabled()
        {
            string dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            AssetLoader.Initialize(dir);
        }
    }
}
