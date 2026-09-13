# Core 源语义审计

基线：`611561f81534354c8c1618dc27c4782cd9277cbf`。范围是 `engine/modules/gui/core/include` 与 `core/src` 中全部 **239 个运行时 C++ 文件（128 个公开头文件、111 个源码/私有头文件）**。`migration/core-semantics-audit.json` 对每个文件记录源 SHA256、源行数、实际存在的 C# 目标和本轮核对重点，缺失映射为 0，源 SHA256 全部与基线清单一致。数学依赖 HCT/SRGB 位于 SkrBase，其单独映射仍保留在 `structure-source-map.json`。

这里区分三件事：文件可追溯、既有源测试通过、额外语言语义审计。文件映射覆盖完整，不把它称为数学意义的等价证明；补充审计用例也不冒充原有 558 个测试用例。最终编译与全套运行由 root 串行执行，避免共享 Core 输出 DLL 被并行编译锁定。

## 本轮已在 structure 所有权范围修正

| 源事实 | 原 C# 偏差 | 已落盘的对应修正 |
| --- | --- | --- |
| `framework/widget/widget_dsl.hpp:11–19` 是 `requires { w->pre_construct() } -> same_as<void>`，不是接口约束 | 仅实现 IPreConstruct/IPostConstruct 才调用 | WidgetBuilder 查找公开、零参数、返回 void 的方法；仍保持 pre → 配置 → post 顺序；反射调用异常解包保留原异常。非 void hook 不调用。 |
| `visual/proxy/visual_transform.hpp:19/57`、`visual/multi_child/visual_placement.hpp:60/295`、`math/m3/tonal_palette.hpp:24` 返回 const 值引用 | 返回值快照，无法持有活引用 | VisualTransform.Transform、VisualPlacementSlot.Placement、M3TonalPalette.KeyColor 返回 `ref readonly`；普通 `var` 读取仍复制，显式 ref readonly 借用能观察后来赋值。 |
| `visual/visual_hit_test.hpp:24/70` 和 `visual/multi_child/visual_grid.hpp:116–117/412–416` 返回 const Vector& | IReadOnlyList 接口背后直接暴露 List，可强转修改内部集合 | 返回活的只读集合包装，读取随内部集合变化，强转写入抛 NotSupportedException。 |
| `math/layout/placement.hpp:1188` 的轴解析失败早退不写输出 | 私有 out helper 首行清零 | 改 ref，调用处使用源一样的已初始化 BoxConstraints 字段；不更改布局规则。 |
| `visual/visual_hit_test.hpp:177/203` 的逆矩阵与投影失败不写输出 | 私有 out helper 失败清零 | 两方法改 ref；调用处局部仍按源初始化。 |
| `src/visual/multi_child/visual_wrap.cpp:29` 的 next_child_index 失败不写 out_index | 失败清零 | 改 ref；原逐子节点定位循环保持不变。 |
| `framework/key.hpp` 的 String 依据 UTF8 原字节拥有/比较 | .NET string 不能表示任意原始字节；没有引用 getter 对应 | Key 采用最小 OwnedUtf8String 值适配；构造、setter、可变引用的赋值均复制输入 UTF8 bytes；equal/hash 按字节；GetString 返回只读引用，GetStringMutable 对应原非 const 引用重载。 |
| `src/text/font/files_font_provider.cpp:40/51/64` 的 read_u16/read_u32/find_table 失败保持输出 | out 开头清零 | 三 helper 改 ref，所有局部参数按源先初始化再传入；请求表缺失或非法范围均保持哨兵。 |
| 同文件 `decode_name_record` 的字段读取失败和不支持平台早退保持输出；`parse_face` 找不到 name table 时保持输出 | 进入方法就清空结果 | 两 helper 同样改 ref；只在源开始真正解码/重置 face 的原位置清空。成功路径和后续失败位置原样保留。 |
| FilesFontProvider.font_faces 返回借用的 const Vector<FilesFontProviderFace>& | IReadOnlyList 的 element 是可变 class，外部可改目录 | 增加 ReadOnlyFilesFontProviderFace 元素借用与 Families 只读集合，公开目录不暴露可变 List；Source 是值副本；Copy 是明确编辑副本。 |
| FilesFontProvider 中原 String 类型用于家族名、源路径、版本键 | UTF16 家族名/路径可能折叠不同原字节 | 这些链路采用 Utf8StringView；保存目录路径和家族名时复制 bytes；仅 OS FileInfo/File.ReadAllBytes 边界转换 string。版本键拼接保留原路径 bytes。 |

另补 NexusSlot 默认哨兵：使用 index+1 的内部 ulong 编码，让 CLR default(T)/数组零初始化也与原 Invalid 一致；公开索引、比较顺序和 8-byte 布局不变。新增针对默认值、数组、首末有效值和大小的验证。

`GuiAssert.Require` 增加 `[DoesNotReturnIf(false)]`，只表达既有抛异常控制流供编译器识别；`Verify` 原来的可继续行为没有改变。

## 协作模块已发现并交回所有者

| 项目 | 状态及证据 |
| --- | --- |
| `ColorGradientStop` 默认色 alpha | 原 SRGBColor{} 为 A=1；目标默认 struct 曾 A=0。render agent 已补显式构造。 |
| Circle/Ellipse/Superellipse sampler | 原是值类型，曾翻译为 class。render agent 已改 struct，ShapeSampleHelper 原 evaluator 的 `Sampler&` 也改 ref delegate；回写缓存状态保留。50 个原 shape case 已重新运行通过，render agent 报告 131+4 个 VG case 通过。 |
| TextStyle const 借用 | VisualTempText/TextLine/TextParagraph 曾暴露可变内部 TextStyle。text agent 已做活 ReadOnlyTextStyle，列表也是活只读包装；Copy 用于外部编辑。 |
| Backend clear color 与嵌套构造 | VisualRenderDesc/VisualRenderTargetDesc 的源 clear_color{} 为 A=1；root 已修，同时核对 VisualOwner 的 BoxConstraints 初始值。 |
| TextFontCache 与 glyph raster | 源若干输出在失败早退保留已有值。text agent 负责 FontCache 五个 ref helper 及 BitmapFormat；不与其文件交叉修改。 |
| PaintTransform.const 借用 | `offset()/transform()`、PaintContext.transform() 的原 const 引用render agent 已用 ref readonly 对应；最新整套回归通过。 |
| SortedBatchCmd/BatchRegion | 原 `batch_cmd.hpp:137–149` 为普通值 struct，目标曾是 class。render agent 已转为 struct 并逐原引用位置回写；原算法不变，最新整套回归通过。 |
| VG 公开集合 | 原 commands()/nodes()/contours() 是 const 集合借用；已交 render agent 核对避免可变容器/元素泄漏。 |

## 已核对为源行为，不做“修复”

- `SkrBase/math/gen/mat/float_matrix.hpp:166` 的 float4x4 默认构造清零。BatchCmd 的 `transform = {}` 因而必须是零矩阵；VisualHitTestEntry 源则显式 `identity()`，两处不能统一。
- Grid 的整组 SetColumns/SetRows 在源里不触发 MarkNeedsLayout；目标继续保留这个行为，不能为测试更方便而改变调度。
- BuildOwner 的 dirty 队列先按深度、再按 dirty 标志稳定排序；迭代中有新增 dirty 时重排并回退检查；finalize 则在排序后逆序清理 inactive 子树。原状态锁、callback/rebuilding 阶段和 defer 清理顺序仍在。
- 原 `CountingAspectRatio` 测试 fixture 没有自己的生成体，它继承的 `Super` 别名指向 VisualProxy。测试中 `Super::perform_layout()` 因而不能译成 VisualAspectRatio 的 C# base.PerformLayout。已按真实生成头文件修正 fixture 的显式祖先调用，没有改 runtime 来迎合测试。
- `DecodeUtf16BeName`、`DecodeAsciiName`、FilesFontProvider.QueryFace、ParseFile、字体 atlas 位图准备等源函数明确先清输出；这些继续保留清空行为，不能机械地全改成失败保留。
- FontFace 源 API 文档明确返回非拥有指针，unload 后失效；C# unload 同步释放 native face 对应这个约束。
- 原 Key 的数值实数载荷是 double，不降精度为 float；HCT/M3 也保留 double 及原全部矩阵/迭代常量。

## 原占位与宿主替代边界

GlobalKey retake/registry、data-scope/notification、reactive 自动依赖追踪、intrinsic/dry/baseline 缓存、paint dirty 管线中源尚未完成的部分仍保持其原状态。Text 中原 SKR_UNIMPLEMENTED 的纵排、部分 word-break/substring/drop-cap/support-data 分支没有冒充新增实现。所有 .NET/Godot 宿主、文件 IO/native FFI、动态模块注册替代均在边界文件中；Widget/Visual 布局与生命周期没有换成 Godot Control/Container。

`src/gui_core_module.cpp` 的 on_load/on_unload 两者原来为空。C# 工程/宿主承接模块装载，没有可遗漏的 GUI 业务函数；`math.hpp`、forward/aggregate headers 的符号由映射中的编译单元直接提供。

## 生命周期与语言边界

源 Visual/Nexus/BuildScope/Batch 持有物采用 RC，C# 普通对象引用没有自动对应“最后一次 RC 释放时立刻析构”的时刻。root 明确采用：普通对象由共享引用/GC 保持活性，拥有原生资源的对象提供 Dispose/finalizer，不引入改变所有公开 API 的 RC<T>。State 的原虚拟析构已由 NexusComponent.OnDestroy 调用 Dispose。VisualTempText 原默认析构隐式释放 RC<TextParagraph>，由 text agent 补对应原生资源生命周期清理；不能在 NexusVisual.OnDestroy 中无条件销毁仍由 VisualOwner 或其他外部强引用持有的 Visual。

源非拥有指针已在指定结构范围恢复为内部 BorrowedReference<T>（WeakReference）：Nexus.ParentNode、NexusVisual._ancestorVisualNexus、State.AttachedNexus、VisualNode._parent/_owner、VisualSlot.AttachedNode。所有公开 getter/参数仍使用原对象类型，不把弱引用泄漏到业务 API。BuildScope.owner 之前已是 WeakReference。活树的向下拥有关系继续使用强引用。

正常结束路径逐源核对未改变：Nexus.DestroyInternal 清 ParentNode/CurrentWidget/Scope；NexusComponent 清 State.AttachedNexus；NexusVisual 清 ancestor/visual；VisualOwner.RemoveRoot 递归 DetachOwnerSubtree，先清 _owner 再调用 OnDetachOwner。外部仍持有子节点也不能保活失去拥有者的 ancestor/owner。没有把 GC 析构时间说成 C++ 末 RC 同步析构时间。

## 本轮新增 18 个边界用例

- WidgetDslSourceContractTests：2 个，方法发现与异常/非 void 约束。
- KeySourceContractTests：1 个，原字节等值、拥有副本、活引用及可变引用赋值隔离。
- NexusSlotDefaultContractTests：1 个，default/数组无效哨兵、排序和原大小。
- StructureBorrowContractTests：4 个，布局/命中失败输出、活 readonly 值借用和集合借用。
- FilesFontSourceContractTests：4 个，标量/表/名称/face 失败哨兵与目录只读借用。
- FilesFontUtf8ContractTests：1 个，无法被 UTF16 安全代替的原 byte 家族名及目录拥有副本。
- RawBorrowGcContractTests：5 个，child 不拥有 Visual parent/owner、slot 不拥有 node、State 不拥有 Nexus、Nexus child 不拥有 ancestor/BuildOwner，以及活树继续保留 child/state。

**上述 18 项及当前全部已注册 610 项均在正式 test 项目的独立 StructureAudit 配置通过：610 passed / 0 failed，结果为 `.report/core-semantics-full-results.json`。** 该配置避免与其他 agent 的 Debug DLL 同时写入；没有为测试另造实现或 stub。先前的 Debug 定向运行曾因共享 Core.dll 编译锁失败，现已用独立配置完成实际验证。

此前原结构 142 个、shape 50 个、VisualTempText 12 个均通过；50 个 shape 在 sampler struct/ref 修正后也复测通过。最新全套结果为 610/610 通过，其中源案例与新增宿主/语言边界案例分开计数。补充语言边界检查不计入原始 558 个 source cases。
