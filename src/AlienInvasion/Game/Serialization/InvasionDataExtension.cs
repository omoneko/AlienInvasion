using System.Collections.Generic;
using AlienInvasion.Core;
using ICities;

namespace AlienInvasion.Game.Serialization
{
    /// <summary>
    /// Persists the contamination zone ledger into the save game. The game discovers this
    /// class on its own.
    /// An invasion in progress is deliberately not saved. On every level load, OnLoadData first
    /// calls InvasionManager.ResetForNewLevel() and RedContaminationVisual.Clear(), so that
    /// switching to a different save leaves none of the previous level's static state behind -
    /// no mothership GameObject, no invasion phase, no decals - and then restores the one thing
    /// that is persisted, the contamination zones, through ReplaceAll. This is a deliberate
    /// simplification: an invasion under way is discarded on a level load rather than
    /// resumed.
    /// </summary>
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
                InvasionManager.ResetForNewLevel();
                RedContaminationVisual.Clear();

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
