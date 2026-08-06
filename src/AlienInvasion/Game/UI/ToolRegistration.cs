using System;
using System.Collections.Generic;
using System.Reflection;

namespace AlienInvasion.Game.UI
{
    /// <summary>
    /// Registers a custom ToolBase with ToolController and ToolsModifierControl at runtime.
    ///
    /// Why this is needed, as confirmed by decompiling the game:
    /// - ToolController.m_tools is built exactly once, by GetComponents&lt;ToolBase&gt;() in Awake.
    /// - ToolsModifierControl.SetTool&lt;T&gt;() does nothing but look in the static dictionary
    ///   m_Tools, which CollectTools() builds from toolController.Tools[] on first use.
    /// Neither knows about a tool a mod added after the game started, so SetTool&lt;MyTool&gt;()
    /// finds nothing, returns (T)null and does nothing - which is exactly why the button and
    /// the hotkey used to do nothing at all.
    ///
    /// The fix is threefold: add the component to the ToolController's GameObject, append it to
    /// the private m_tools array by reflection, and register it in the static m_Tools
    /// dictionary. SetTool only works once all three are done.
    ///
    /// Reloading a level recreates the ToolController and rebuilds m_tools in Awake, so this
    /// must be called again on every level load; an existing instance is reused.
    /// </summary>
    internal static class ToolRegistration
    {
        public static T Register<T>() where T : ToolBase
        {
            ToolController controller = ToolsModifierControl.toolController;
            if (controller == null)
            {
                ModConfig.LogError("ToolRegistration: cannot register, toolController is null");
                return null;
            }

            // Reuse an existing instance, so nothing is created twice
            T tool = controller.gameObject.GetComponent<T>();
            if (tool == null)
            {
                tool = controller.gameObject.AddComponent<T>();
                AppendToControllerTools(controller, tool);
            }

            RegisterInModifierDictionary(tool);
            ModConfig.Log("ToolRegistration: registered " + typeof(T).Name);
            return tool;
        }

        private static void AppendToControllerTools(ToolController controller, ToolBase tool)
        {
            FieldInfo field = typeof(ToolController).GetField("m_tools", BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null)
            {
                ModConfig.LogError("ToolRegistration: the ToolController.m_tools field was not found");
                return;
            }

            ToolBase[] tools = field.GetValue(controller) as ToolBase[];
            int len = tools == null ? 0 : tools.Length;
            for (int i = 0; i < len; i++)
            {
                if (tools[i] == tool) return; // already there
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
                ModConfig.LogError("ToolRegistration: the ToolsModifierControl.m_Tools field was not found");
                return;
            }

            Dictionary<Type, ToolBase> dict = dictField.GetValue(null) as Dictionary<Type, ToolBase>;
            if (dict == null)
            {
                // CollectTools() has not run yet. It collects from toolController.Tools[] on
                // the first SetTool, so anything AppendToControllerTools already added to that
                // array is picked up automatically.
                return;
            }

            dict[tool.GetType()] = tool;
        }
    }
}
