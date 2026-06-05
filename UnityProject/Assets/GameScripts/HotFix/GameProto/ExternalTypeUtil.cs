using UnityEngine;

// 说明（BlockBlast v1）：
// 原模板在此提供 GameConfig.vector2/3/4/2int/3int 与 UnityEngine.Vector* 的转换。
// 这些 builtin 外部类型仅在某个配置表字段实际使用 vector* 类型时才会被 Luban 生成；
// 本项目当前没有任何字段使用它们，引用 GameConfig.vector2 等会导致 GameProto 编译失败。
// 因此暂时清空。若未来新增 vector* 字段，请从下方注释恢复对应方法。
public static class ExternalTypeUtil
{
    // public static Vector2 NewVector2(GameConfig.vector2 v) => new Vector2(v.X, v.Y);
    // public static Vector3 NewVector3(GameConfig.vector3 v) => new Vector3(v.X, v.Y, v.Z);
    // public static Vector4 NewVector4(GameConfig.vector4 v) => new Vector4(v.X, v.Y, v.Z, v.W);
    // public static Vector2Int NewVector2Int(GameConfig.vector2int v) => new Vector2Int(v.X, v.Y);
    // public static Vector3Int NewVector3Int(GameConfig.vector3int v) => new Vector3Int(v.X, v.Y, v.Z);
}
