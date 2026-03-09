# 鏁屼汉 AI 璁剧疆璇存槑涔︼紙Enemy AI Setup锛?

鏈鏄庝功瀵瑰簲褰撳墠宸ョ▼涓殑銆屾晫浜?AI锛堟剰鍥鹃┍鍔?+ 鍐呯疆琛屼负鏍戯級銆嶅疄鐜帮紝鍖呭惈锛?

- 浠ｇ爜渚х粨鏋勪笌閰嶇疆鏂囦欢浣嶇疆
- **鎶€鑳戒笌鏁屼汉鎵╁睍**锛氬彇娑堟櫘鏀汇€佺粺涓€鎶€鑳姐€佸绉嶇簿鑻?瀹堝崼鑰呫€佹妧鑳芥晥鏋滅粍鍚堢殑璇︾粏璁捐瑙?**銆婃晫浜轰笌鎶€鑳界郴缁熻璁℃柟妗?缁熶竴鎶€鑳戒笌鏁堟灉缁勫悎銆?*锛堝悓鐩綍涓?`鏁屼汉涓庢妧鑳界郴缁熻璁℃柟妗?缁熶竴鎶€鑳戒笌鏁堟灉缁勫悎.md`锛?
- 濡備綍涓€閿垱寤洪粯璁ゆ暟鎹厤缃?
- 濡備綍鍒朵綔鏁屼汉棰勫埗浣?
- Animator Controller 濡備綍閰嶇疆锛堝弬鏁板悕蹇呴』鍖归厤锛?
- **鍐呯疆琛屼负鏍?*锛氱函浠ｇ爜瀹炵幇銆?*鏀寔鏁版嵁椹卞姩**锛堟爲缁撴瀯涓庡弬鏁板彲鍦?Inspector/璧勪骇涓紪杈戯紝鏃犻渶鏀逛唬鐮侊級

---

## 1. 涓€閿敓鎴愰粯璁ゆ暟鎹厤缃紙蹇呴』鍋氾級

鏈」鐩墍鏈夐厤缃兘浠?`Resources/閰嶇疆/` 鍔犺浇銆備綘闇€瑕佸厛鍦ㄧ紪杈戝櫒鐢熸垚榛樿閰嶇疆璧勪骇锛?

1. Unity 椤堕儴鑿滃崟锛歚娓告垙 > 涓€閿垱寤哄叏閮ㄩ厤缃紙闇€姹備功榛樿鏁版嵁锛塦
2. 鐢熸垚/瑕嗙洊鐨勫叧閿?AI 鐩稿叧閰嶇疆锛?
   - `Assets/Resources/閰嶇疆/鏁屼汉琛屼负閰嶇疆搴?asset`
   - `Assets/Resources/閰嶇疆/鏁屼汉琛屼负_杩戞垬灏忔€?asset`
   - `Assets/Resources/閰嶇疆/鏁屼汉琛屼负_杩滅▼灏忔€?asset`
   - `Assets/Resources/閰嶇疆/鏁屼汉琛屼负_绮捐嫳.asset`
   - `Assets/Resources/閰嶇疆/鏁屼汉琛屼负_瀹堝崼鑰?asset`
   - `Assets/Resources/閰嶇疆/鏁屼汉琛屼负_Boss.asset`
   - `Assets/Resources/閰嶇疆/鍔ㄧ敾甯т激瀹虫暟鎹厤缃簱.asset`锛堝凡鍖呭惈 `EnemyMelee` / `EnemyRanged`锛?

### 1.1 浼氫笉浼氳鐩栧凡鏈夋暟鎹拰寮曠敤锛?

**浼氥€?* 璇ヨ彍鍗曚細**鐩存帴瑕嗙洊**浠ヤ笅鎵€鏈夎祫浜х殑鍐呭锛堢敤鑴氭湰閲岀殑榛樿鏁版嵁閲嶅啓锛夛紝涓?*涓嶄細**璇㈤棶纭锛?

| 璧勪骇 | 琛屼负 | 瀵瑰紩鐢ㄧ殑褰卞搷 |
|------|------|----------------|
| 鏁屼汉灞炴€у簱銆佸叧鍗￠厤缃簱銆佹帀钀借〃搴撱€佹鍣ㄥ簱銆佺帺瀹舵垚闀垮睘鎬у簱銆佸晢搴椾环鏍笺€佽嵂姘存晥鏋溿€佹妧鑳介厤缃簱銆佸姩鐢诲抚浼ゅ/鐗规晥搴撱€佺墿鍝佹樉绀洪厤缃€佹寜閿噸缁戝畾閰嶇疆銆佽儗鍖匲I閰嶇疆銆丅uff閰嶇疆 | **瑕嗙洊**锛氬悓璺緞涓嬬敤鏂版暟鎹浛鎹㈠師璧勪骇 | 璺緞鍜?GUID 涓€鑸笉鍙橈紝鍏朵粬璧勪骇閲屽瀹冧滑鐨勫紩鐢ㄩ€氬父**涓嶄細鏂?*锛堜粛鎸囧悜鍚屼竴璧勪骇锛?|
| **鏁屼汉琛屼负閰嶇疆搴?* 鍙?5 涓瓙璧勪骇锛堟晫浜鸿涓篲杩戞垬灏忔€?/ 杩滅▼灏忔€?/ 绮捐嫳 / 瀹堝崼鑰?/ Boss锛?| **瑕嗙洊**锛氬悓璺緞涓嬬敤鏂版暟鎹浛鎹紝涓嶅垹闄ゅ師璧勪骇 | 璺緞涓?GUID 涓嶅彉锛?*Archetype Override** 绛夊紩鐢ㄤ細淇濈暀 |
| 闅惧害绯绘暟.asset | **浠呭綋涓嶅瓨鍦ㄦ椂鍒涘缓** | 鑻ュ凡瀛樺湪鍒欎笉浼氬姩锛屼篃涓嶄細褰卞搷寮曠敤 |

**寤鸿锛?*

- 鎵€鏈夎瑕嗙洊鐨勮祫浜?*鍐呭浼氳閲嶇疆涓鸿剼鏈噷鐨勯粯璁ゆ暟鎹?*锛屼綘涔嬪墠鏀硅繃鐨勬暟鍊间細涓㈠け銆?
- 鑻ヤ綘宸茬粡鏀硅繃 `Resources/閰嶇疆/` 涓嬬殑浠绘剰閰嶇疆锛?*鍏堝浠芥暣涓?`Assets/Resources/閰嶇疆` 鏂囦欢澶?*锛堟垨鑷冲皯澶囦唤浣犳敼杩囩殑 .asset锛夛紝鍐嶇偣涓€閿垱寤恒€?
- **寮曠敤**锛氬綋鍓嶅疄鐜颁负鍚岃矾寰勮鐩栥€佷笉鍒犺祫浜э紝GUID 涓嶅彉锛岄鍒朵綋/鍦烘櫙閲屽涓婅堪閰嶇疆鐨勫紩鐢?*涓嶄細**鍙樻垚 Missing銆?

**涓€閿垱寤虹幇鍦ㄨ繕浼氱敓鎴愶細**
- `Assets/Resources/閰嶇疆/琛屼负鏍慱.asset`锛堥粯璁ゆ晫浜鸿涓烘爲锛?
- `Assets/Resources/Config/MainMenuBgmTracks.asset` 涓?`LevelBgmTracks.asset`锛圔GM 鏇茬洰鍒楄〃锛屽垵濮嬩负绌猴級

---

## 1.2 浠庨浂寮€濮嬶細鎺ヤ笅鏉ュ湪缂栬緫鍣ㄩ噷鍋氫粈涔堬紙鎺ㄨ崘椤哄簭锛?

鑻ヤ綘杩樻病鍋氳繃浠讳綍閰嶇疆涓庨鍒朵綋锛屾寜涓嬮潰椤哄簭鍋氬嵆鍙€?

| 姝ラ | 鍦ㄧ紪杈戝櫒閲屽仛浠€涔?|
|------|------------------|
| **1** | 鑿滃崟 **娓告垙 鈫?涓€閿垱寤哄叏閮ㄩ厤缃紙闇€姹備功榛樿鏁版嵁锛?*銆備細鐢熸垚 `Resources/閰嶇疆/` 涓嬫墍鏈夐厤缃€侀粯璁よ涓烘爲 `Resources/閰嶇疆/琛屼负鏍慱.asset`锛屼互鍙?BGM 鏇茬洰鍒楄〃銆?|
| **2** | **鍦烘櫙鐑樼剻 NavMesh锛堢粍浠舵柟寮忥級**锛氭墦寮€鍏冲崱鍦烘櫙锛屽湪 Hierarchy 閲屾柊寤虹┖鐗╀綋锛堝鍛藉悕涓?`Navigation`锛夛紝涓哄叾娣诲姞 **Nav Mesh Surface** 缁勪欢锛堥渶宸插畨瑁?AI Navigation 鍖咃級锛涙妸闇€瑕佸弬涓庣儤鐒欑殑鍦伴潰/妤兼绛夌墿浣撳嬀閫?**Navigation Static**锛圛nspector 鍙充笂瑙?Static锛夛紱鍦?Nav Mesh Surface 缁勪欢涓婄偣 **Bake**锛屽嵆鍙湪鍦烘櫙閲岀湅鍒拌摑鑹插鑸綉鏍笺€傝瑙佷笅鏂?搂2.1銆?|
| **3** | **鍋氭晫浜?Animator**锛氭柊寤烘垨鎵撳紑涓€涓?Animator Controller锛屾坊鍔犵姸鎬侊紙濡?Idle/Locomotion銆丮eleeAttack銆丷angedAttack銆丠urt銆丆astSkill0锝?锛夛紝娣诲姞鍙傛暟锛欶loat `Speed`銆両nt `MeleeIndex`銆乀rigger `MeleeAttack` / `RangedAttack` / `Hurt` / `CastSkill0`锝瀈CastSkill3`銆傜敤杩囨浮杩炲ソ锛圓ny State 鈫?鏀诲嚮/鍙楀嚮/鎶€鑳界敤 Trigger锛屾敾鍑荤粨鏉熺敤 Exit Time 鍥?Locomotion锛夈€?|
| **4** | **鍋氭晫浜洪鍒朵綋**锛氭柊寤虹┖鐗╀綋锛屾寕 **Animator**锛堢敤涓婁竴姝ョ殑 Controller锛夈€?*NavMeshAgent**銆?*CapsuleCollider**锛屼互鍙婅剼鏈?**EnemyController**銆?*EnemyAI**銆?*EnemyPerception**銆?*EnemyMover**銆?*EnemyCombat**銆傚湪 EnemyController 涓婇€夊ソ **Enemy Type**锛涘湪 EnemyAI 涓婃妸 **Behavior Tree Asset** 鎷栨垚 `Resources/閰嶇疆/琛屼负鏍慱`锛堝彲閫夛紝涓嶆嫋鍒欎紭鍏堢敤鏁屼汉閰嶇疆涓婄殑琛屼负鏍戝紩鐢級銆?|
| **5** | **鏀诲嚮鍔ㄧ敾浜嬩欢**锛氬湪鏁屼汉杩戞垬/杩滅▼鏀诲嚮鍔ㄧ敾鐨勫懡涓抚涓婂姞 **Animation Event**锛孎unction 濉?`OnDealDamage`锛孲tring 濉?`EnemyMelee` 鎴?`EnemyRanged`銆?|
| **6** | **BGM锛堝彲閫夛級**锛氬湪 `Resources/Config/MainMenuBgmTracks.asset` 涓?`LevelBgmTracks.asset` 閲屾坊鍔犳洸鐩€佹嫋鍏ラ煶棰戙€?|
| **7** | 鎶婃晫浜洪鍒朵綋鏀捐繘鍏冲崱鍦烘櫙锛堟斁鍦?NavMesh 涓婏級锛岃繍琛屾父鎴忥紝纭鐜╁瀛樺湪涓?`LevelBootstrapper` 宸茶缃?`GameStateMachine.LevelPlayerTransform`锛屾祴璇曟晫浜哄贰閫汇€佸彂鐜扮帺瀹躲€佽拷鍑讳笌鏀诲嚮銆?|

鍋氬畬 1锝? 鍚庯紝鏁屼汉 AI 鍗冲彲鍦ㄥ叧鍗￠噷璺戣捣鏉ワ紱6銆? 鎸夐渶鍋氥€?

---

## 2. 浠ｇ爜缁撴瀯锛堜綘闇€瑕佺煡閬撳摢浜涚粍浠惰鎸傦級

### 2.1 NavMesh 鐑樼剻锛堢粍浠舵柟寮忥紝閫傜敤鏃?Bake 椤电鐨勬柊鐗?Unity锛?

鑻ヤ綘鐨?**Window > AI > Navigation** 閲屾病鏈夈€孊ake銆嶉〉绛撅紝璇风敤 **Nav Mesh Surface 缁勪欢** 鐑樼剻锛?

1. **瀹夎鍖?*锛氳彍鍗?**Window > Package Manager**锛岀‘璁ゅ凡瀹夎 **AI Navigation**锛堟垨 **Navigation**锛夈€傝嫢娌℃湁锛屽湪 Unity Registry 閲屾悳绱㈠苟瀹夎銆?
2. **寤虹┖鐗╀綋**锛氬湪鍏冲崱鍦烘櫙鐨?Hierarchy 閲屽彸閿?**Create Empty**锛屽懡鍚嶄负渚嬪 `Navigation`銆?
3. **鍔犵粍浠?*锛氶€変腑璇ョ墿浣擄紝鍦?Inspector 閲?**Add Component**锛屾悳绱㈠苟娣诲姞 **Nav Mesh Surface**锛堟潵鑷?AI Navigation 鍖咃級銆?
4. **鏍囪鍦伴潰**锛氶€変腑鍦烘櫙閲屾墍鏈夈€屽彲琛岃蛋銆嶇殑鐗╀綋锛堝湴闈€佹ゼ姊€佸钩鍙扮瓑锛夛紝鍦?Inspector 鍙充笂瑙?**Static** 鍕鹃€?**Navigation Static**锛堣嫢娌℃湁璇ラ€夐」锛屽彲鍦?**Window > AI > Navigation** 鐨?**Object** 椤电閲岃缃紝鎴栧 Nav Mesh Surface 浣跨敤 **Collect Objects** 浠?Layer 鏀堕泦锛夈€?
5. **鐑樼剻**锛氶€変腑甯?Nav Mesh Surface 鐨勭墿浣擄紝鍦?Inspector 閲岃缁勪欢涓婃壘鍒?**Bake** 鎸夐挳锛岀偣鍑诲嵆鍙€傚満鏅腑浼氬嚭鐜拌摑鑹插鑸綉鏍硷紱鏁屼汉甯?NavMeshAgent 鍗冲彲鍦ㄤ笂闈㈠璺€?

NavMeshAgent 涓庤繖绉嶇儤鐒欐柟寮忓吋瀹癸紝鏃犻渶鏀逛唬鐮併€?

---

鏁屼汉鐢便€屾剰鍥撅紙Intent锛夈€嶉┍鍔細

- AI锛堝喅绛栧眰锛夊啓鍏ワ細`EnemyController.CurrentIntent`
- 绉诲姩灞傝鍙栧苟鎵ц锛歚EnemyMover`锛圢avMeshAgent锛?
- 鎴樻枟/鍔ㄧ敾灞傝鍙栧苟鎵ц锛歚EnemyCombat`锛圓nimator + 鍔ㄧ敾浜嬩欢鎵撲激瀹筹級

鍏抽敭鑴氭湰浣嶇疆锛?

- `Assets/Scripts/Game/Presentation/EnemyController.cs`
- `Assets/Scripts/Game/Presentation/Enemy/EnemyAI.cs`
- `Assets/Scripts/Game/Presentation/Enemy/EnemyPerception.cs`
- `Assets/Scripts/Game/Presentation/Enemy/EnemyMover.cs`
- `Assets/Scripts/Game/Presentation/Enemy/EnemyCombat.cs`

**鍐呯疆琛屼负鏍戯紙鏀寔鏁版嵁椹卞姩锛夛細**

- `Assets/Scripts/Game/AI/BehaviorTree/` 涓嬶細
  - `TaskStatus.cs`銆乣IBehaviorNode.cs`銆乣EnemyAIContext.cs`
  - `SelectorNode.cs`銆乣SequenceNode.cs`锛堢粍鍚堣妭鐐癸級
  - `EnemyConditionNodes.cs`銆乣EnemyActionNodes.cs`锛堟潯浠朵笌鍔ㄤ綔鑺傜偣锛?
  - `BehaviorTreeNodeData.cs`銆乣BehaviorTreeAsset.cs`锛?*鍙簭鍒楀寲鏍戞暟鎹笌璧勪骇**
  - `BehaviorTreeFromDataBuilder.cs`锛氭牴鎹祫浜ф暟鎹瀯寤鸿繍琛屾椂鏍?
  - `EnemyBehaviorTreeBuilder.cs`锛氫唬鐮侀粯璁ゆ爲锛堟湭鎸傝祫浜ф椂浣跨敤锛?

閰嶇疆浣嶇疆锛?

- `Assets/Scripts/Game/Data/EnemyArchetypeSO.cs`
- `Assets/Scripts/Game/Data/EnemyArchetypeDatabaseSO.cs`
- 鍔犺浇鍏ュ彛锛歚Assets/Scripts/Game/Config/ConfigManager.cs`锛坄Resources.Load("閰嶇疆/鏁屼汉琛屼负閰嶇疆搴?)`锛?

---

## 3. 鏁屼汉棰勫埗浣撴€庝箞鍋氾紙涓€姝ヤ竴姝ワ級

### 3.1 鍩虹缁勪欢锛堝繀椤伙級

鍦ㄤ綘鐨勬晫浜?GameObject 涓婃坊鍔?纭浠ヤ笅缁勪欢锛?

1. `Animator`
2. `NavMeshAgent`
3. `Collider`锛堟帹鑽?`CapsuleCollider`锛?
4. 鑴氭湰缁勪欢锛?
   - `EnemyController`
   - `EnemyAI`
   - `EnemyPerception`
   - `EnemyMover`
   - `EnemyCombat`

寤鸿锛堝彲閫夛級锛?

- 鑻ヤ綘甯屾湜鏁屼汉涓庣帺瀹跺彂鐢熲€滅墿鐞嗛樆鎸♀€濓紙浜掔浉鎺ㄦ尋/鏃犳硶绌胯繃锛夛紝骞朵笖浣犵殑鐜╁鏄?`Rigidbody` 浣撶郴锛氱粰鏁屼汉鍔犱竴涓?`Rigidbody`锛屽苟鍕鹃€?`Is Kinematic = true`銆?
- 鑻ョ帺瀹朵娇鐢?`CharacterController`锛岄€氬父鍙鍙屾柟鏈?Collider 灏辫兘浜х敓闃绘尅鏁堟灉锛堝彇鍐充簬浣犵殑瑙掕壊鎺у埗瀹炵幇锛夈€?

### 3.2 EnemyController 璁剧疆

- `Enemy Type`锛氶€夋嫨鏁屼汉绫诲瀷锛堜笌灞炴€у簱涓€鑷达級
- `Archetype Override`锛堝彲涓嶅～锛夛細涓€鑸笉闇€瑕佸～锛汢oss 5 鍙樹綋瑕佺敤涓嶅悓鎶€鑳?涓嶅悓鎰熺煡鍙傛暟鏃舵墠濉€?

### 3.3 NavMeshAgent 寤鸿鍊?

- `Speed`锛氫笉鐢ㄥ湪 Inspector 鍥哄畾锛岃繍琛屾椂鐢?`EnemyController.MoveSpeed` 鍐欏叆
- `Angular Speed`锛?20~720锛堢湅浣犺浆鍚戝揩鎱級
- 璁板緱鍦ㄥ満鏅噷鐑樼剻 NavMesh锛歚Window > AI > Navigation`锛堟垨 Unity 鏂扮増鐨?NavMesh Components 宸ヤ綔娴侊級

---

## 4. Animator Controller 鎬庝箞閰嶇疆锛堝繀椤讳弗鏍煎榻愬弬鏁板悕锛?

`EnemyCombat` 榛樿浣跨敤濡備笅 Animator 鍙傛暟鍚嶏紙浣犱篃鍙互鍦ㄧ粍浠朵笂鏀癸紝浣嗘帹鑽愪繚鎸侀粯璁わ級锛?

- Float锛歚Speed`
- Int锛歚MeleeIndex`
- Trigger锛歚MeleeAttack`
- Trigger锛歚RangedAttack`
- Trigger锛歚Hurt`
- Trigger锛歚CastSkill0`銆乣CastSkill1`銆乣CastSkill2`銆乣CastSkill3`

### 4.1 鎺ㄨ崘鐨勭姸鎬佹満缁撴瀯锛堟渶灏忓彲鐢級

浣犲彲浠ョ敤涓€涓緢绠€鍗曠殑 Animator锛?

- `Locomotion`锛圛dle/Walk/Run 娣峰悎鎴栧崟鐘舵€侀兘琛岋紝闈?`Speed` 鎺у埗锛?
- `MeleeAttack`锛堝彲鍋氳繛鍑伙紱鐢?`MeleeIndex` 鎺у埗涓嶅悓鏀诲嚮鍔ㄧ敾锛?
- `RangedAttack`
- `Hurt`
- `CastSkill0..3`锛圔oss 鎶€鑳藉姩鐢伙級

杩囨浮寤鸿锛?

- Any State -> `MeleeAttack`锛堟潯浠讹細Trigger `MeleeAttack`锛?
- Any State -> `RangedAttack`锛堟潯浠讹細Trigger `RangedAttack`锛?
- Any State -> `Hurt`锛堟潯浠讹細Trigger `Hurt`锛?
- Any State -> `CastSkill0..3`锛堟潯浠讹細Trigger `CastSkillX`锛?
- 鏀诲嚮/鍙楀嚮/鎶€鑳藉姩鐢荤粨鏉?-> 鍥炲埌 `Locomotion`锛堢敤 Exit Time锛?

> 閲嶇偣锛歚EnemyCombat` 鍙湪鎰忓浘绫诲瀷鍙樺寲鏃惰Е鍙?Trigger锛屾墍浠?Animator 蹇呴』鑳藉湪瑙﹀彂鍚庡畬鎴愭挱鏀惧苟鍥炲埌绉诲姩/寰呮満銆?

---

## 5. 鏁屼汉浼ゅ鎬庝箞鎵撳埌鐜╁锛堝姩鐢讳簨浠讹級

`EnemyCombat.OnDealDamage(string damageName)` 閫氳繃鍔ㄧ敾浜嬩欢瑙﹀彂銆?

浣犻渶瑕佸湪鏁屼汉鏀诲嚮鍔ㄧ敾鐨勫悎閫傚抚涓婂姞 Animation Event锛?

- Function锛歚OnDealDamage`
- String 鍙傛暟锛歚EnemyMelee` 鎴?`EnemyRanged`锛堟垨浣犺嚜宸卞湪閰嶇疆搴撻噷鍔犵殑鍚嶅瓧锛?

璇ヤ激瀹冲悕闇€瑕佸湪 `鍔ㄧ敾甯т激瀹虫暟鎹厤缃簱` 涓瓨鍦紝骞朵笖 `hitLayerName = "Player"`銆?

榛樿宸插湪涓€閿厤缃腑鍔犲叆锛?

- `EnemyMelee`
- `EnemyRanged`

---

## 6. 琛屼负鏍戯細鏁版嵁椹卞姩锛堟帹鑽愶級涓庝唬鐮侀粯璁?

鏈」鐩娇鐢?*绾唬鐮佽涓烘爲**锛屽苟鏀寔**鏁版嵁椹卞姩**锛氭爲缁撴瀯銆佽妭鐐圭被鍨嬨€佸弬鏁板潎鍙斁鍦?ScriptableObject 璧勪骇涓紝鍦?Inspector 閲岀紪杈戯紝**鏃犻渶鏀逛唬鐮?*鍗冲彲璋冮€昏緫銆?

### 6.1 鏁版嵁椹卞姩鎬庝箞鐢紙鎺ㄨ崘锛?

1. **鍒涘缓琛屼负鏍戣祫浜?*
   - 鑿滃崟锛歚娓告垙 > 涓€閿垱寤哄叏閮ㄩ厤缃紙闇€姹備功榛樿鏁版嵁锛塦 浼氫竴骞剁敓鎴愰粯璁ゆ晫浜鸿涓烘爲锛堝強鎵€鏈夊叾浠栭厤缃級銆?
   - 榛樿琛屼负鏍戣矾寰勶細`Assets/Resources/閰嶇疆/琛屼负鏍慱.asset`锛屼笌浠ｇ爜榛樿閫昏緫涓€鑷淬€?

2. **鍦?Inspector 涓紪杈?*
   - 閫変腑璇ヨ祫浜э紝鍦?Inspector 涓紪杈?**Nodes** 鍒楄〃锛堟墎骞崇粨鏋勶紝閬垮厤 Unity 搴忓垪鍖栨繁搴﹂檺鍒讹級锛?
     - 姣忛」锛?*Node Type**銆?*Parent Index**锛堢埗鑺傜偣鍦ㄥ垪琛ㄤ腑鐨勪笅鏍囷紝-1 琛ㄧず鏍癸紝浠呬竴涓級銆?*Sibling Order**锛堝悓鐖朵笅鐨勯『搴忥級銆?
     - **Param Float1 / Float2**锛氬 `ActionSetIntentPatrol` 鐨勫贰閫婚棿闅旀渶灏?鏈€澶у€硷紙绉掞級銆?
     - 澧炲垹鑺傜偣鏃朵繚鎸佸彧鏈変竴涓妭鐐圭殑 `parentIndex == -1`锛堟牴锛夛紝鍏朵綑鑺傜偣鐨?`parentIndex` 鎸囧悜姝ｇ‘涓嬫爣鍗冲彲銆?

3. **鎸傚埌鏁屼汉**
   - 鍦ㄦ晫浜洪鍒朵綋锛堟垨鍦烘櫙涓殑鏁屼汉锛変笂锛屾壘鍒?**EnemyAI** 缁勪欢銆?
   - 灏嗚涓烘爲璧勪骇鎷栧埌 **Behavior Tree Asset** 妲戒綅銆?
   - 杩愯鏃跺皢鎸夎祫浜т腑鐨勬爲鎵ц锛涗笉鎸傚垯浣跨敤浠ｇ爜榛樿鏍戙€?

杩欐牱浣犲彲浠ワ細
- 璋冩暣浼樺厛绾э紙鏀?Selector 涓嬪瓙鑺傜偣椤哄簭锛夈€佸鍒犲垎鏀紙鍔?鍑?Sequence銆佹潯浠躲€佸姩浣滐級銆?
- 鏀瑰贰閫婚棿闅旂瓑鍙傛暟锛堟敼瀵瑰簲鑺傜偣鐨?`paramFloat1` / `paramFloat2`锛夛紝鏃犻渶鏀?C#銆?

### 6.2 鏀寔鐨勮妭鐐圭被鍨嬶紙nodeType 瀛楃涓诧級

| 绫诲瀷 | 璇存槑 | 瀛愯妭鐐?| 鍙傛暟 |
|------|------|--------|------|
| `Selector` | 浠庡乏鍒板彸锛屾湁涓€涓?Success 鍗宠繑鍥?Success | 鏈?| 鏃?|
| `Sequence` | 浠庡乏鍒板彸锛屾湁涓€涓?Failure 鍗宠繑鍥?Failure | 鏈?| 鏃?|
| `CondHasTarget` | 鏈夎拷鍑荤洰鏍?| 鏃?| 鏃?|
| `CondIsHurt` | 澶勪簬鍙楀嚮鐘舵€?| 鏃?| 鏃?|
| `CondCanCastSkill` | 鎸囧畾鎶€鑳芥Ы褰撳墠鍙柦鏀?| 鏃?| `paramInt1`=鎶€鑳芥Ы |
| `CondIsInSkillRange` | 褰撳墠鐩爣鍦ㄦ寚瀹氭妧鑳芥Ы鏂芥硶璺濈鍐?| 鏃?| `paramInt1`=鎶€鑳芥Ы |
| `ActionSetIntentHurt` | 璁剧疆鎰忓浘锛氬彈鍑?| 鏃?| 鏃?|
| `ActionSetIntentChase` | 璁剧疆鎰忓浘锛氳拷鍑?| 鏃?| 鏃?|
| `ActionSetIntentCastSkill` | 璁剧疆鎰忓浘锛氭柦鏀炬寚瀹氭妧鑳芥Ы | 鏃?| `paramInt1`=鎶€鑳芥Ы |
| `ActionSetIntentPatrol` | 璁剧疆鎰忓浘锛氬贰閫伙紙鍗婂緞鏉ヨ嚜 Archetype锛?| 鏃?| paramFloat1=闂撮殧鏈€灏忓€硷紝paramFloat2=闂撮殧鏈€澶у€硷紙绉掞級 |

鏂板鑺傜偣绫诲瀷锛堝 Boss 鎶€鑳姐€佽嚜瀹氫箟鏉′欢锛変粛闇€鍦ㄤ唬鐮侀噷鍔犱竴娆¤妭鐐圭被骞跺湪 `BehaviorTreeFromDataBuilder` 涓敞鍐岋紱涔嬪悗璇ョ被鍨嬪嵆鍙湪璧勪骇閲岄噸澶嶄娇鐢紝鏃犻渶鍐嶆敼浠ｇ爜銆?

### 6.3 杩愯娴佺▼

- `EnemyAI` 鍦?`Start` 涓細鑻ユ寕浜?`BehaviorTreeAsset` 涓?`nodes` 闈炵┖锛屽垯鐢?`BehaviorTreeFromDataBuilder.Build(asset)` 浠庢墎骞宠妭鐐瑰垪琛ㄦ瀯寤烘爲锛涘惁鍒欑敤 `EnemyBehaviorTreeBuilder.BuildDefaultTree()`銆?
- 姣忓抚锛氬厛 `EnemyPerception.Tick()`锛屽啀濉厖 `EnemyAIContext`锛屾渶鍚?`root.Tick(ctx)`銆?
- 鏍戝唴鍔ㄤ綔鑺傜偣璁剧疆 `EnemyController.CurrentIntent`锛岀敱 `EnemyMover` 鍜?`EnemyCombat` 鎵ц銆?

### 6.4 浣曟椂闇€瑕佹敼浠ｇ爜

- **鍙敼鏍戠粨鏋勩€侀『搴忋€佸弬鏁?*锛氬湪琛屼负鏍戣祫浜ч噷鏀瑰嵆鍙紝**涓嶉渶瑕佹敼浠ｇ爜**銆?
- **鏂板涓€绉嶈妭鐐圭被鍨?*锛堜緥濡傛柊鏉′欢銆佹柊鍔ㄤ綔銆丅oss 鎶€鑳斤級锛氶渶瑕佸湪 `EnemyConditionNodes.cs` / `EnemyActionNodes.cs` 閲屽姞绫伙紝骞跺湪 `BehaviorTreeFromDataBuilder.BuildNode()` 閲屽姞瀵瑰簲 `nodeType` 鐨?case锛涗箣鍚庤绫诲瀷鍗冲彲鍦ㄤ换鎰忚祫浜т腑鏁版嵁椹卞姩浣跨敤銆?

---

## 7. Boss 5 鍙樹綋鎬庝箞鍋氾紙鎺ㄨ崘鍋氭硶锛?

褰撳墠榛樿閰嶇疆搴撻噷鍙湁涓€涓?`鏁屼汉琛屼负_Boss.asset`锛屽鏋滀綘闇€瑕?5 涓?Boss 鍙樹綋锛?

1. 澶嶅埗鍑?5 浠借涓洪厤缃細
   - `鏁屼汉琛屼负_Boss_1.asset` ... `鏁屼汉琛屼负_Boss_5.asset`
2. 鍒嗗埆璋冩暣鎶€鑳藉弬鏁帮紙50% 琛€鍓?鍚庡彲鐢ㄦ妧鑳芥Ы浣嶇瓑锛?
3. 鍦ㄥ搴?Boss 棰勫埗浣撲笂锛?
   - `EnemyController -> Archetype Override` 鎸囧悜瀵瑰簲鐨?Boss 琛屼负閰嶇疆

鑻ュ笇鏈?Boss 浣跨敤涓嶅悓鐨勬爲閫昏緫锛氫负 Boss 棰勫埗浣撳崟鐙?*澶嶅埗涓€浠借涓烘爲璧勪骇**锛屽湪 Inspector 閲屽姞涓婃妧鑳藉垎鏀紙闇€鍏堝湪浠ｇ爜涓鍔犫€滄妧鑳解€濈被鑺傜偣骞跺湪 Builder 涓敞鍐岋級锛屽啀灏嗚璧勪骇鎷栧埌 Boss 鐨?`EnemyAI > Behavior Tree Asset` 鍗冲彲锛屾棤闇€鏀?EnemyAI 鑴氭湰銆?

---

## 8. 甯歌闂鎺掓煡

1. 鏁屼汉涓嶇Щ鍔細
   - 鏄惁鐑樼剻浜?NavMesh锛?
   - `NavMeshAgent` 鏄惁鍦ㄦ纭眰/浣嶇疆锛熸槸鍚﹁绂佺敤锛?
   - `EnemyAI` 鏄惁瀛樺湪涓旀晫浜烘湭姝讳骸锛坄EnemyController.IsAlive`锛夛紵
2. 鍔ㄧ敾涓嶆挱锛?
   - Animator 鍙傛暟鍚嶆槸鍚﹀畬鍏ㄤ竴鑷达紙澶у皬鍐欒涓€鑷达級锛?
   - 鏄惁蹇樹簡浠庢敾鍑诲姩鐢诲洖鍒?Locomotion锛圗xit Time锛夛紵
3. 涓嶆帀琛€锛?
   - 鏀诲嚮鍔ㄧ敾涓婃槸鍚﹀姞浜?Animation Event 璋?`OnDealDamage`锛?
   - 浼犲叆鐨?`damageName` 鏄惁鍦?`鍔ㄧ敾甯т激瀹虫暟鎹厤缃簱` 涓瓨鍦紵
   - `damageName` 鐨?`hitLayerName` 鏄惁涓?`Player`锛?

