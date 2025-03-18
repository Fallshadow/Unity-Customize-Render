using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace LiteRP {
    // urp 管线下是通过定义 UniversalRender : ScriptableRender 来实现的自定义记录
    // 这里更推荐 IRenderGraphRecorder 接口，未来更方便统一管理所有自定义管线接口
    public partial class LiteRenderGraphRecorder : IRenderGraphRecorder {
        // frameData 容器对象，传递当前渲染上下文的帧数据信息
        public void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData) {
            AddDrawObjectsPass(renderGraph, frameData);
        }
    }
}