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

#if UNITY_EDITOR
            //if (cameraData.camera.cameraType == CameraType.SceneView)
            //    AddEditorRenderTargetPass(renderGraph);
            AddDrawEditorGizmoPass(renderGraph, cameraData, GizmoSubset.PreImageEffects);
            AddDrawEditorGizmoPass(renderGraph, cameraData, GizmoSubset.PostImageEffects);
#endif
        }

        private void CreateRenderGraphCameraRenderTargets(RenderGraph renderGraph, CameraData cameraData) {
            // 如果相机自身存在 Texture 就用它自身的
            var cameraTargetTexture = cameraData.camera.targetTexture;
            bool isBuiltInTexture = (cameraTargetTexture == null);
            bool isCameraTargetOffscreenDepth = !isBuiltInTexture && cameraData.camera.targetTexture.format == RenderTextureFormat.Depth;

            RenderTargetIdentifier targetColorId = isBuiltInTexture ? BuiltinRenderTextureType.CameraTarget : new RenderTargetIdentifier(cameraTargetTexture);
            RenderTargetIdentifier targetDepthId = isBuiltInTexture ? BuiltinRenderTextureType.Depth : new RenderTargetIdentifier(cameraTargetTexture);

            if (m_TargetColorHandle == null)
                m_TargetColorHandle = RTHandles.Alloc((RenderTargetIdentifier)targetColorId, "BackBuffer color");
            // m_TargetColorHandle 是我们自己创建的 backbufferRT。如果 camera 有自己的 texture，那么这两个 ID，不相同，需要用这个接口绑定一下
            else if (m_TargetColorHandle.nameID != targetColorId)
                RTHandleStaticHelpers.SetRTHandleUserManagedWrapper(ref m_TargetColorHandle, targetColorId);

            if (m_TargetDepthHandle == null)
                m_TargetDepthHandle = RTHandles.Alloc(targetDepthId, "Backbuffer depth");
            else if (m_TargetDepthHandle.nameID != targetDepthId)
                RTHandleStaticHelpers.SetRTHandleUserManagedWrapper(ref m_TargetDepthHandle, targetDepthId);

            Color clearColor = cameraData.GetClearColor();

            bool clearBackbufferOnFirstUse = !renderGraph.nativeRenderPassesEnabled;
            bool discardColorBackbufferOnLastUse = !renderGraph.nativeRenderPassesEnabled;
            bool discardDepthBackbufferOnLastUse = !isCameraTargetOffscreenDepth;

            ImportResourceParams importBackbufferColorParams = new ImportResourceParams();
            importBackbufferColorParams.clearOnFirstUse = clearBackbufferOnFirstUse;
            importBackbufferColorParams.clearColor = clearColor;
            importBackbufferColorParams.discardOnLastUse = discardColorBackbufferOnLastUse;

            // clearOnFirstUse 和 discardOnLastUse 在 nativeRenderPasses 下一定设置成 false，否则 scene 控件显示会异常
            ImportResourceParams importBackbufferDepthParams = new ImportResourceParams();
            importBackbufferDepthParams.clearOnFirstUse = clearBackbufferOnFirstUse;
            importBackbufferDepthParams.clearColor = clearColor;
            importBackbufferDepthParams.discardOnLastUse = discardDepthBackbufferOnLastUse;
#if UNITY_EDITOR
            // 在像 Apple M1/M2 这样的 TBDR gpu 上，我们需要在 Gizmos 编辑器中保留覆盖相机的后缓冲深度
            if (cameraData.camera.cameraType == CameraType.SceneView)
                importBackbufferDepthParams.discardOnLastUse = false;
#endif

            bool colorRT_sRGB = (QualitySettings.activeColorSpace == ColorSpace.Linear);
            RenderTargetInfo importInfoColor = new RenderTargetInfo();
            RenderTargetInfo importInfoDepth = new RenderTargetInfo();

            if (isBuiltInTexture) {
                importInfoColor.width = Screen.width;
                importInfoColor.height = Screen.height;
                importInfoColor.volumeDepth = 1;
                importInfoColor.msaaSamples = 1;
                importInfoColor.format = GraphicsFormatUtility.GetGraphicsFormat(RenderTextureFormat.Default, colorRT_sRGB);
                importInfoColor.bindMS = false;

                importInfoDepth = importInfoColor;
                importInfoDepth.format = SystemInfo.GetGraphicsFormat(DefaultFormat.DepthStencil);
            }
            else {
                importInfoColor.width = cameraTargetTexture.width;
                importInfoColor.height = cameraTargetTexture.height;
                importInfoColor.volumeDepth = cameraTargetTexture.volumeDepth;
                importInfoColor.msaaSamples = cameraTargetTexture.antiAliasing;
                importInfoColor.format = GraphicsFormatUtility.GetGraphicsFormat(RenderTextureFormat.Default, colorRT_sRGB);

                importInfoDepth = importInfoColor;
                importInfoDepth.format = SystemInfo.GetGraphicsFormat(DefaultFormat.DepthStencil);
            }

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