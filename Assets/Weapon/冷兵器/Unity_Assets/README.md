# 古代冷兵器 — Unity 模型包

已从原始素材（Cinema 4D + DDS 贴图）整理为可直接拖入 Unity 使用的格式。

## 内容

```
AncientWeapons/
├── Models/
│   └── AncientWeapons.fbx      # 60 款古代兵器（刀剑斧盾枪弓），单文件、内含全部子物体
└── Textures/
    └── 3d66Model-691525-files-*.png   # 66 张贴图（已由 DDS 转 PNG，RGBA）
```

## 如何导入

1. 打开你的 Unity 项目。
2. 把 `AncientWeapons` 整个文件夹**拖进 `Assets/` 目录**（或直接在 Assets 下粘贴）。
3. Unity 会自动：
   - 导入 FBX，生成一个 prefab（在 Project 窗口里看到模型图标）；
   - 按文件名自动把 PNG 贴图关联到对应材质（贴图与 FBX 内材质名同名，因此自动匹配）。
4. 把模型从 Project 窗口拖进 Scene / Hierarchy 即可使用。

## 注意事项（常见问题）

- **贴图没自动关联**：如果某个材质显示为粉红色（missing），选中该材质 → 在 Inspector 的 `Albedo` / `Base Map` 槽手动拖入对应 `Textures/` 里的同名 PNG。
- **模型尺寸 / 方向**：源文件是 Cinema 4D（单位 cm）。若导入后模型过大或过小，选中 `AncientWeapons.fbx` → Inspector 的 **Model** 标签下调整 **Scale Factor**（如 `0.01`），或在 `Convert Units` 里勾选 cm→m。方向若不对，把 prefab 根物体绕 X 轴转 `-90°` 试试。
- **单个武器单独使用**：FBX 内含全部 60 个武器（各为一个子物体）。展开 Hierarchy 里的模型节点，即可把单个武器单独拖出来用，或复制成多个实例。
- **渲染管线**：默认用内置管线（Standard Shader）即可。URP / HDRP 下会自动使用 Lit 材质，若材质变紫，用 `Edit → Rendering → Materials → Convert ...` 一键转换。
- **原始 DDS / OBJ / C4D** 无需放进 Unity，已保留在原 `dass036---60款古代兵器3D模型素材` 文件夹中。

## 文件来源

- 原始素材：`dass036---60款古代兵器3D模型素材/`（含 `古代兵器.c4d / .fbx / .obj / .mtl` 及 `tex/*.dds`）。
- 本包由脚本 `dass036---60款古代兵器3D模型素材/convert_dds_to_png.py` 生成（DDS→PNG）。
