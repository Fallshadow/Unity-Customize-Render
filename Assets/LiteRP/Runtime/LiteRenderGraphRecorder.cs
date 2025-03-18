using LiteRP.FrameData;
using System;
using UnityEngine.Experimental.Rendering;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace LiteRP {
    // urp 管线下是通过定义 UniversalRender : ScriptableRender 来实现的自定义记录
    // 这里更推荐 IRenderGraphRecorder 接口，未来更方便统一管理所有自定义管线接口
    public partial class LiteRenderGraphRecorder : IRenderGraphRecorder, IDisposable {

        private TextureHandle m_BackbufferColorHandle = TextureHandle.nullHandle;
        private RTHandle m_TargetColorHandle = null;

        // frameData 容器对象，传递当前渲染上下文的帧数据信息
        public void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData) {
            CameraData cameraData = frameData.Get<CameraData>();
            CreateRenderGraphCameraRenderTargets(renderGraph, cameraData);
            AddSetupCameraPropertiesPass(renderGraph, cameraData);
            AddDrawObjectsPass(renderGraph, cameraData);
        }

        private void CreateRenderGraphCameraRenderTargets(RenderGraph renderGraph, CameraData cameraData) {
            RenderTargetIdentifier targetColorId = BuiltinRenderTextureType.CameraTarget;
            if (m_TargetColorHandle == null)
                m_TargetColorHandle = RTHandles.Alloc((RenderTargetIdentifier)targetColorId, "BackBuffer color");

            Color cameraBackgroundColor = CoreUtils.ConvertSRGBToActiveColorSpace(cameraData.camera.backgroundColor);

            ImportResourceParams importBackbufferColorParams = new ImportResourceParams();
            importBackbufferColorParams.clearOnFirstUse = true;
            importBackbufferColorParams.clearColor = cameraBackgroundColor;
            importBackbufferColorParams.discardOnLastUse = false;

            bool colorRT_sRGB = (QualitySettings.activeColorSpace == ColorSpace.Linear);
            RenderTargetInfo importInfoColor = new RenderTargetInfo();
            importInfoColor.width = Screen.width;
            importInfoColor.height = Screen.height;
            importInfoColor.volumeDepth = 1;
            importInfoColor.msaaSamples = 1;
            importInfoColor.format = GraphicsFormatUtility.GetGraphicsFormat(RenderTextureFormat.Default, colorRT_sRGB);

            m_BackbufferColorHandle = renderGraph.ImportTexture(m_TargetColorHandle, importInfoColor, importBackbufferColorParams);
        }

        public void Dispose() {
            RTHandles.Release(m_TargetColorHandle);
            GC.SuppressFinalize(this);
        }
    }
}