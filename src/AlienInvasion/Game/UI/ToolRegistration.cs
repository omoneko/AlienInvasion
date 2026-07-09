using System;
using System.Collections.Generic;
using System.Reflection;

namespace AlienInvasion.Game.UI
{
    /// <summary>
    /// カスタム ToolBase を実行時に ToolController / ToolsModifierControl へ登録する。
    ///
    /// なぜ必要か(逆コンパイルで確認した実挙動):
    /// - ToolController.m_tools は Awake の GetComponents&lt;ToolBase&gt;() で一度だけ構築される。
    /// - ToolsModifierControl.SetTool&lt;T&gt;() は静的辞書 m_Tools を引くだけで、m_Tools は
    ///   CollectTools() が toolController.Tools[] から初回に構築する。
    /// どちらもゲーム起動後にmodが追加したツールを知らないため、SetTool&lt;MyTool&gt;() は
    /// 辞書に無く (T)null を返して何もしない ← ボタンもF7も空振りしていた原因。
    ///
    /// そこで (1)ToolControllerのGameObjectにコンポーネントを追加し、(2)private配列 m_tools に
    /// reflectionで追記、(3)静的辞書 m_Tools にも登録する。3つ揃えて初めて SetTool が機能する。
    ///
    /// レベル再ロード時は ToolController が作り直され Awake で m_tools が張り直されるため、
    /// レベルロード毎に本メソッドを呼んで再登録する(既存インスタンスがあれば再利用)。
    /// </summary>
    internal static class ToolRegistration
    {
        public static T Register<T>() where T : ToolBase
        {
            ToolController controller = ToolsModifierControl.toolController;
            if (controller == null)
            {
                ModConfig.LogError("ToolRegistration: toolController が null のため登録できません");
                return null;
            }

            // 既存インスタンスがあれば再利用(二重生成防止)
            T tool = controller.gameObject.GetComponent<T>();
            if (tool == null)
            {
                tool = controller.gameObject.AddComponent<T>();
                AppendToControllerTools(controller, tool);
            }

            RegisterInModifierDictionary(tool);
            ModConfig.Log("ToolRegistration: " + typeof(T).Name + " を登録しました");
            return tool;
        }

        private static void AppendToControllerTools(ToolController controller, ToolBase tool)
        {
            FieldInfo field = typeof(ToolController).GetField("m_tools", BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null)
            {
                ModConfig.LogError("ToolRegistration: ToolController.m_tools フィールドが見つかりません");
                return;
            }

            ToolBase[] tools = field.GetValue(controller) as ToolBase[];
            int len = tools == null ? 0 : tools.Length;
            for (int i = 0; i < len; i++)
            {
                if (tools[i] == tool) return; // 既に含まれている
            }

            ToolBase[] newTools = new ToolBase[len + 1];
            if (tools != null) Array.Copy(tools, newTools, len);
            newTools[len] = tool;
            field.SetValue(controller, newTools);
        }

        private static void RegisterInModifierDictionary(ToolBase tool)
        {
            FieldInfo dictField = typeof(ToolsModifierControl).GetField("m_Tools", BindingFlags.Static | BindingFlags.NonPublic);
            if (dictField == null)
            {
                ModConfig.LogError("ToolRegistration: ToolsModifierControl.m_Tools フィールドが見つかりません");
                return;
            }

            Dictionary<Type, ToolBase> dict = dictField.GetValue(null) as Dictionary<Type, ToolBase>;
            if (dict == null)
            {
                // まだ CollectTools() されていない。初回 SetTool 時に toolController.Tools[] から
                // 収集されるので、AppendToControllerTools で配列に足してあれば自動的に拾われる。
                return;
            }

            dict[tool.GetType()] = tool;
        }
    }
}
