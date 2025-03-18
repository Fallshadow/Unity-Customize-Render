using UnityEngine.Rendering;
using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Rendering.RenderGraphModule;
using LiteRP.FrameData;

namespace LiteRP {
    public class LiteRenderPipeline : RenderPipeline {

        private RenderGraph m_RenderGraph = null; // 渲染图
        private LiteRenderGraphRecorder m_LiteRenderGraphRecorder = null; // 渲染图记录器
        private ContextContainer m_ContextContainer = null; // 上下文容器

        public LiteRenderPipeline() {
            InitializeRenderGraph();
        }

        protected override void Dispose(bool disposing) {
            CleanupRenderGraph();
            base.Dispose(disposing);
        }

        // 初始化渲染图
        private void InitializeRenderGraph() {
            m_RenderGraph = new RenderGraph("LiteRPRenderGraph");
            m_LiteRenderGraphRecorder = new LiteRenderGraphRecorder();
            m_ContextContainer = new ContextContainer();
        }

        // 清理渲染图
        private void CleanupRenderGraph() {
            m_ContextContainer?.Dispose();
            m_ContextContainer = null;
            m_LiteRenderGraphRecorder = null;
            m_RenderGraph?.Cleanup();
            m_RenderGraph = null;
        }

        // 为了兼容不得不保留的老接口，现在不用
        // 不用是因为 Camera[] 不够动态，而 Render 又是那种调用频繁的接口，一旦有列表内元素增删变化，会造成额外的开销
        // 
        // Render 方法会在运行时和编辑器的每个 view 与 game view 下每帧调用
        // 并且也会在代码手动调用 Camera.Render 时每帧调用
        // 因此在编辑器模式下，即使只有一个场景摄像机 render 函数中的 camera 列表中也会包含诸如 scene camera、game camera、preview camera 等多个相机
        // 因此我们需要在 render 函数中遍历每个相机来进行渲染
        protected override void Render(ScriptableRenderContext context, Camera[] cameras) {

        }

        // 新接口
        protected override void Render(ScriptableRenderContext context, List<Camera> cameras) {

            // 题外话，BeginFrameRendering 接口已经过时，现在都用 BeginContextRendering
            // 
            // 开始渲染上下文
            BeginContextRendering(context, cameras);

            // 渲染相机
            for (int cameraIndex = 0; cameraIndex < cameras.Count; cameraIndex++) {
                Camera camera = cameras[cameraIndex];
                RenderCamera(context, camera);
            }

            // 结束渲染图
            m_RenderGraph.EndFrame();

            // 结束渲染上下文
            EndContextRendering(context, cameras);
        }

        private void RenderCamera(ScriptableRenderContext context, Camera camera) {
            // 开始渲染相机
            BeginCameraRendering(context, camera);

            // 准备 FrameData
            if (!PrepareFrameData(context, camera))
                return;

            // 为相机创建 CommandBuffer
            // CommandBufferPool 需要程序集引用 core.runtime 和 core.runtime.shared
            CommandBuffer cmd = CommandBufferPool.Get(camera.name);
            // 设置相机属性参数
            context.SetupCameraProperties(camera);

            // 记录并执行渲染图
            RecordAndExecuteRenderGraph(context, camera, cmd);

            // 提交命令缓冲区
            context.ExecuteCommandBuffer(cmd);
            // 释放命令缓冲区
            cmd.Clear();
            CommandBufferPool.Release(cmd);
            // 提交渲染上下文
            context.Submit();
            // 结束渲染相机
            EndCameraRendering(context, camera);
        }

        private void RecordAndExecuteRenderGraph(ScriptableRenderContext context, Camera camera, CommandBuffer cmd) {
            RenderGraphParameters renderGraphParameters = new RenderGraphParameters() {
                executionName = camera.name,
                commandBuffer = cmd,
                scriptableRenderContext = context,
                currentFrameIndex = Time.frameCount
            };
            // 开启录制线，用户自己定义
            m_RenderGraph.BeginRecording(renderGraphParameters);

            // 开启录制线
            m_LiteRenderGraphRecorder.RecordRenderGraph(m_RenderGraph, m_ContextContainer);

            // 执行线隐藏到 srp 中执行，用户不得干预
            m_RenderGraph.EndRecordingAndExecute();
        }

        private bool PrepareFrameData(ScriptableRenderContext context, Camera camera) {
            // 获取相机剔除参数，并进行剔除
            ScriptableCullingParameters cullingParameters;
            if (!camera.TryGetCullingParameters(out cullingParameters))
                return false;
            CullingResults cullingResults = context.Cull(ref cullingParameters);
            CameraData cameraData = m_ContextContainer.GetOrCreate<CameraData>();
            cameraData.camera = camera;
            cameraData.cullingResults = cullingResults;
            return true;
        }
    }
}