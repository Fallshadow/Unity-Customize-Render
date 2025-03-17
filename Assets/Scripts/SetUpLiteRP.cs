using UnityEngine;
using UnityEngine.Rendering;

public class SetUpLiteRP : MonoBehaviour
{
    public RenderPipelineAsset currentPipelineAsset;

    private void OnEnable() {
        GraphicsSettings.renderPipelineAsset = currentPipelineAsset;
    }

    // 可以在运行状态下动态切换
    private void OnValidate() {
        GraphicsSettings.renderPipelineAsset = currentPipelineAsset;
    }
}
