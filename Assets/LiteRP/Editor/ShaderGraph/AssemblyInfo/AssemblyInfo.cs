using System.Runtime.CompilerServices;

// 通过这种方式，让 ShadeGraph 的 internal 对象 暴露给 LiteRP
[assembly: InternalsVisibleTo("LiteRP.Editor")]