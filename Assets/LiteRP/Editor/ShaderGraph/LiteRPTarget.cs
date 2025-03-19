using System;
using UnityEditor.ShaderGraph;
using UnityEngine.Rendering;

namespace LiteRP.Editor {
    sealed class LiteRPTarget : Target {
        
        // shader graph 可以选择 active target 为 LiteRP 了 
        public LiteRPTarget() {
            displayName = "LiteRP";
        }

        public override bool IsActive() {
            bool isLiteRenderPipeline = GraphicsSettings.currentRenderPipeline is LiteRPAsset;
            return isLiteRenderPipeline;
        }

        public override void Setup(ref TargetSetupContext context) {
        }

        public override void GetFields(ref TargetFieldContext context) {

        }

        public override void GetActiveBlocks(ref TargetActiveBlockContext context) {

        }

        public override void GetPropertiesGUI(ref TargetPropertyGUIContext context, Action onChange, Action<string> registerUndo) {
        }

        public override bool WorksWithSRP(RenderPipelineAsset scriptableRenderPipeline) {
            return scriptableRenderPipeline?.GetType() == typeof(LiteRPAsset);
        }
    }
}