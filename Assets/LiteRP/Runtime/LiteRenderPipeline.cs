using UnityEngine.Rendering;
using UnityEngine;
using System.Collections.Generic;

namespace LiteRP {
    public class LiteRenderPipeline : RenderPipeline {

        private static readonly ShaderTagId s_ShaderTagId = new ShaderTagId("SRPDefaultUnlit");

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

            // 结束渲染上下文
            EndContextRendering(context, cameras);
        }

        private void RenderCamera(ScriptableRenderContext context, Camera camera) {
            // 开始渲染相机
            BeginCameraRendering(context, camera);

            // 1: 相机剔除
            // 使用引擎内固定的流程完成，这一步对应在 profiler 中的表现就是 CullScriptable 函数过程，这一过程只能通过降低场景复杂度来节省开销（我们又改不到引擎源码）
            // 
            // 获取相机剔除参数，并进行剔除
            ScriptableCullingParameters cullingParameters;
            if(!camera.TryGetCullingParameters(out cullingParameters)) {
                return;
            }

            CullingResults cullingResults = context.Cull(ref cullingParameters);
            // 2：为相机创建 CommandBuffer
            // CommandBufferPool 需要程序集引用 core.runtime 和 core.runtime.shared
            CommandBuffer cb = CommandBufferPool.Get(camera.name);
            // 3：设置相机属性参数
            context.SetupCameraProperties(camera);

            var clearFlags = camera.clearFlags;
            bool clearSkybox = clearFlags == CameraClearFlags.Skybox;
            bool clearDepth = clearFlags != CameraClearFlags.Nothing;
            bool clearColor = clearFlags == CameraClearFlags.Color;

            // 4：清理渲染目标
            // 这里 srp 的颜色空间是线性空间，需要转换到 build-in 下的 gamma 空间，才能正确显示编辑器下 camera 对应的背景颜色。
            cb.ClearRenderTarget(true, true, CoreUtils.ConvertSRGBToActiveColorSpace(camera.backgroundColor));

            if (clearSkybox) {
                // 虽然可以使用 context.DrawSkybox(camera); 但是接口已经过时了
                // 绘制天空盒
                var skyboxRendererList = context.CreateSkyboxRendererList(camera);
                cb.DrawRendererList(skyboxRendererList);
            }

            // 5：指定渲染排序设置 SortSettings
            var sortSettings = new SortingSettings(camera);
            // 6：指定渲染状态设置 DrawSettings
            var drawSettings = new DrawingSettings(new ShaderTagId("SRPDefaultUnlit"), sortSettings);

            // 789 渲染物体

            // 绘制不透明物体
            sortSettings.criteria = SortingCriteria.CommonOpaque;
            // 7：指定渲染过滤设置 FilterSettings
            var filterSettings = new FilteringSettings(RenderQueueRange.opaque);
            // 8：创建渲染对象列表
            var rendererListParams = new RendererListParams(cullingResults, drawSettings, filterSettings);
            var rendererList = context.CreateRendererList(ref rendererListParams);
            // 9：绘制渲染列表
            cb.DrawRendererList(rendererList);

            // 绘制半透明物体
            sortSettings.criteria = SortingCriteria.CommonTransparent;
            // 7：指定渲染过滤设置 FilterSettings
            filterSettings = new FilteringSettings(RenderQueueRange.transparent);
            // 8：创建渲染对象列表
            rendererListParams = new RendererListParams(cullingResults, drawSettings, filterSettings);
            rendererList = context.CreateRendererList(ref rendererListParams);
            // 9：绘制渲染列表
            cb.DrawRendererList(rendererList);

            // 10：提交命令缓冲区
            context.ExecuteCommandBuffer(cb);
            // 11：释放命令缓冲区
            cb.Clear();
            CommandBufferPool.Release(cb);
            // 12：提交渲染上下文
            context.Submit();
            // 结束渲染相机
            EndCameraRendering(context, camera);
        }
    }
}