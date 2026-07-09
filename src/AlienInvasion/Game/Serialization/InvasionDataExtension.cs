using System.Collections.Generic;
using AlienInvasion.Core;
using ICities;

namespace AlienInvasion.Game.Serialization
{
    /// <summary>汚染ゾーン台帳をセーブデータへ永続化する。ゲームが自動検出。</summary>
    public class InvasionDataExtension : SerializableDataExtensionBase
    {
        private const string DataId = "AlienInvasion.Contamination.v1";

        public override void OnSaveData()
        {
            try
            {
                List<ContaminationZone> zones = ContaminationManager.Zones;
                byte[] bytes = ZoneSerializer.Serialize(zones);
                serializableDataManager.SaveData(DataId, bytes);
                ModConfig.Log("saved " + zones.Count + " zone(s)");
            }
            catch (System.Exception e)
            {
                ModConfig.LogError("save error: " + e);
            }
        }

        public override void OnLoadData()
        {
            try
            {
                byte[] bytes = serializableDataManager.LoadData(DataId);
                List<ContaminationZone> zones = ZoneSerializer.Deserialize(bytes);
                ContaminationManager.ReplaceAll(zones);
                ModConfig.Log("loaded " + zones.Count + " zone(s)");
            }
            catch (System.Exception e)
            {
                ModConfig.LogError("load error: " + e);
            }
        }
    }
}
