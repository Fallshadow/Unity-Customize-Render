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

        private static readonly ShaderTagId s_shaderTagId = new ShaderTagId("SRPDefaultUnlit"); // 渲染标签ID

        private TextureHandle m_BackbufferColorHandle = TextureHandle.nullHandle;
        private RTHandle m_TargetColorHandle = null;

        private TextureHandle m_BackbufferDepthHandle = TextureHandle.nullHandle;
        private RTHandle m_TargetDepthHandle = null;

        // frameData 容器对象，传递当前渲染上下文的帧数据信息
        public void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData) {
            CameraData cameraData = frameData.Get<CameraData>();
            CreateRenderGraphCameraRenderTargets(renderGraph, cameraData);
            AddSetupCameraPropertiesPass(renderGraph, cameraData);

            CameraClearFlags clearFlags = cameraData.camera.clearFlags;
            // native 下不需要 ClearRender
            if (!renderGraph.nativeRenderPassesEnabled && clearFlags != CameraClearFlags.Nothing) {
                AddClearRenderTargetPass(renderGraph, cameraData);
            }

            AddDrawOpaqueObjectsPass(renderGraph, cameraData);

            if (clearFlags == CameraClearFlags.Skybox && RenderSettings.skybox != null) {
                AddDrawSkyBoxPass(renderGraph, cameraData);
            }

            AddDrawTransparentObjectsPass(renderGraph, cameraData);
        }

        private void CreateRenderGraphCameraRenderTargets(RenderGraph renderGraph, CameraData cameraData) {
            RenderTargetIdentifier targetColorId = BuiltinRenderTextureType.CameraTarget;
            RenderTargetIdentifier targetDepthId = BuiltinRenderTextureType.Depth;

            if (m_TargetColorHandle == null)
                m_TargetColorHandle = RTHandles.Alloc((RenderTargetIdentifier)targetColorId, "BackBuffer color");
            else if (m_TargetColorHandle.nameID != targetColorId)
                RTHandleStaticHelpers.SetRTHandleUserManagedWrapper(ref m_TargetColorHandle, targetColorId);

            if (m_TargetDepthHandle == null)
                m_TargetDepthHandle = RTHandles.Alloc(targetDepthId, "Backbuffer depth");
            else if (m_TargetDepthHandle.nameID != targetDepthId)
                RTHandleStaticHelpers.SetRTHandleUserManagedWrapper(ref m_TargetDepthHandle, targetDepthId);

            Color clearColor = cameraData.GetClearColor();
            RTClearFlags clearFlags = cameraData.GetClearFlags();

            bool clearOnFirstUse = !renderGraph.nativeRenderPassesEnabled;
            bool discardOnLastUse = !renderGraph.nativeRenderPassesEnabled;

            ImportResourceParams importBackbufferColorParams = new ImportResourceParams();
            importBackbufferColorParams.clearOnFirstUse = clearOnFirstUse;
            importBackbufferColorParams.clearColor = clearColor;
            importBackbufferColorParams.discardOnLastUse = discardOnLastUse;

            // clearOnFirstUse 和 discardOnLastUse 在 nativeRenderPasses 下一定设置成 false，否则 scene 控件显示会异常
            ImportResourceParams importBackbufferDepthParams = new ImportResourceParams();
            importBackbufferDepthParams.clearOnFirstUse = clearOnFirstUse;
            importBackbufferDepthParams.clearColor = clearColor;
            importBackbufferDepthParams.discardOnLastUse = discardOnLastUse;
#if UNITY_EDITOR
            // 在像 Apple M1/M2 这样的 TBDR gpu 上，我们需要在 Gizmos 编辑器中保留覆盖相机的后缓冲深度
            if (cameraData.camera.cameraType == CameraType.SceneView)
                importBackbufferDepthParams.discardOnLastUse = false;
#endif

            bool colorRT_sRGB = (QualitySettings.activeColorSpace == ColorSpace.Linear);
            RenderTargetInfo importInfoColor = new RenderTargetInfo();
            importInfoColor.width = Screen.width;
            importInfoColor.height = Screen.height;
            importInfoColor.volumeDepth = 1;
            importInfoColor.msaaSamples = 1;
            importInfoColor.format = GraphicsFormatUtility.GetGraphicsFormat(RenderTextureFormat.Default, colorRT_sRGB);

            RenderTargetInfo importInfoDepth = new RenderTargetInfo();
            importInfoDepth = importInfoColor;
            importInfoDepth.format = SystemInfo.GetGraphicsFormat(DefaultFormat.DepthStencil);

            m_BackbufferColorHandle = renderGraph.ImportTexture(m_TargetColorHandle, importInfoColor, importBackbufferColorParams);
            m_BackbufferDepthHandle = renderGraph.ImportTexture(m_TargetDepthHandle, importInfoDepth, importBackbufferDepthParams);
        }

        public void Dispose() {
            RTHandles.Release(m_TargetColorHandle);
            RTHandles.Release(m_TargetDepthHandle);
            GC.SuppressFinalize(this);
        }
    }
}