using System.Collections.Generic;
using AlienInvasion.Core;
using ICities;

namespace AlienInvasion.Game.Serialization
{
    /// <summary>
    /// 汚染ゾーン台帳をセーブデータへ永続化する。ゲームが自動検出。
    /// フェーズ1では進行中の襲来状態(InvasionManager)はセーブデータに含まれない
    /// ―― OnLoadData は毎レベルロード時に InvasionManager.ResetForNewLevel()/
    /// RedContaminationVisual.Clear() を呼び、別セーブ切り替え時に旧レベルの
    /// 静的状態(母船GameObject・襲来フェーズ・デカール等)が残留しないようにしてから、
    /// 永続化対象である汚染ゾーンのみを ReplaceAll で復元する。これは意図的な簡略化であり、
    /// 発動中の襲来はレベルロードのたびに「再開」ではなく「破棄」される。
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
