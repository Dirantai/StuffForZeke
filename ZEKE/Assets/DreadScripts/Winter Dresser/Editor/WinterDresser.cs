using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using UnityEditor.SceneManagement;
using UnityEngine.Networking;
using Object = UnityEngine.Object;

namespace WinterPaw
{
    internal sealed class WinterDresser : EditorWindow
    {
        #region GUI & Constants
        private static Texture2D banner
        {
            get
            {
                if (bannerInit) return _banner;
                bannerInit = true;
                _banner = Resources.Load<Texture2D>("WinterDresserBanner");
                if (!_banner)
                    Debug.LogError("<color=green>[Winter Dresser]</color> WinterDresserBanner Texture not found in Resources!");

                return _banner;
            }
        }
        private static bool bannerInit;
        private static Texture2D _banner;

        private const string identifier = "[WD] ";
        private static string assetFolderPath;
        private static GUIContent greenLight, redLight;
        private static GUIContent warnIcon;
        private static Vector2 scroll = Vector2.zero;

        private static UnityEditorInternal.ReorderableList propReorderableList;
        private static UnityEditorInternal.ReorderableList mergeReorderableList;

        private static bool dressTab = true;
        private static bool propTab;
        private static bool undressTab;

        private static MethodInfo advancedPopupMethod;
        private static string[] boneNames;
        #endregion

        #region Automated Variables
        private static string _folderPath;
        private static string folderPath
        {
            get
            {
                if (string.IsNullOrWhiteSpace(_folderPath))
                {
                    assetFolderPath = PlayerPrefs.GetString("WinterDresserAssetAssetPath", "Assets/DreadScripts/Winter Dresser/Generated Assets");
                    _folderPath = assetFolderPath + "/" + vrca.gameObject.name;
                    ReadyPath(_folderPath);
                }
                return _folderPath;
            }
        }

        private static VRCExpressionsMenu myMenu;
        private static VRCExpressionParameters myParameters;
        #endregion

        #region Input Variables
        private static VRCAvatarDescriptor vrca;
        private static SkinnedMeshRenderer mainMesh;
        private static VRCExpressionsMenu expressionMenu;

        private static bool makeBackup = true;
        private static bool clipsIntegrateMeshes = true;
        private static bool createClips = true;
        private static bool addToFX = true;
        private static bool addToExpressions = true;

        private static readonly List<DressMesh> mergeMeshes = new List<DressMesh>();
        private static readonly List<DressMesh> propMeshes = new List<DressMesh>();
        private static readonly List<RemovableMesh> detectedMeshes = new List<RemovableMesh>();
        #endregion

        [MenuItem("WinterPaw/Winter Dresser")]
        private static void showWindow()
        {
            GetWindow<WinterDresser>(false, "Winter Dresser", true).titleContent.image = EditorGUIUtility.IconContent("Cloth Icon").image;
        }
        private void OnGUI()
        {


            try { scroll = GUILayout.BeginScrollView(scroll); }
            catch { }

            GUILayout.Label(banner, GUIStyle.none, GUILayout.Width(position.width), GUILayout.Height(position.width * 0.1658f));
            using (new GUILayout.HorizontalScope())
            {
                using (var change = new EditorGUI.ChangeCheckScope())
                {
                    dressTab = GUILayout.Toggle(dressTab, "Clothes", "toolbarbutton");
                    if (change.changed)
                    {
                        dressTab = true;
                        propTab = undressTab = false;
                    }
                }

                using (var change = new EditorGUI.ChangeCheckScope())
                {
                    propTab = GUILayout.Toggle(propTab, "Props", "toolbarbutton");
                    if (change.changed)
                    {
                        propTab = true;
                        dressTab = undressTab = false;
                    }
                }

                using (var change = new EditorGUI.ChangeCheckScope())
                {
                    undressTab = GUILayout.Toggle(undressTab, "Remove", "toolbarbutton");
                    if (change.changed)
                    {
                        undressTab = true;
                        propTab = dressTab = false;
                    }
                }
            }

            using (new GUILayout.HorizontalScope())
            {
                EditorGUI.BeginChangeCheck();
                vrca = (VRCAvatarDescriptor)EditorGUILayout.ObjectField("Avatar", vrca, typeof(VRCAvatarDescriptor), true);
                if (EditorGUI.EndChangeCheck())
                    OnAvatarChanged();

                if (dressTab)
                    mainMesh = (SkinnedMeshRenderer)EditorGUILayout.ObjectField(mainMesh, typeof(SkinnedMeshRenderer), true, GUILayout.Width(100));
            }

            if (dressTab || propTab)
                using (new EditorGUI.DisabledScope(!addToExpressions))
                    expressionMenu = (VRCExpressionsMenu)EditorGUILayout.ObjectField("Expression Menu", expressionMenu, typeof(VRCExpressionsMenu), false);

            if (dressTab)
            {
                mergeReorderableList.DoLayoutList();

                using (new BGColoredScope(Color.green, Color.grey, makeBackup))
                    makeBackup = GUILayout.Toggle(makeBackup, new GUIContent("Make Backup", "Make a copy of the Avatar's Hierarchy"), "button");
            }

            if (propTab)
                propReorderableList.DoLayoutList();




            if (!undressTab)
            {
                using (new GUILayout.HorizontalScope())
                {
                    using (new GUILayout.VerticalScope(GUILayout.Width(position.width / 2 - 5)))
                    {
                        using (new BGColoredScope(Color.green, Color.grey, clipsIntegrateMeshes))
                            clipsIntegrateMeshes = GUILayout.Toggle(clipsIntegrateMeshes, new GUIContent("Merge with Clips", "Add own blendshapes to pre-existing clips that use blendshapes with the same name"), "button");

                        using (new BGColoredScope(Color.green, Color.grey, createClips))
                            createClips = GUILayout.Toggle(createClips, new GUIContent("Create Clips", "Create Animation Clips that turns the Clothes On and Off "), "button");

                    }

                    using (new GUILayout.VerticalScope(GUILayout.Width(position.width / 2 - 5)))
                    {
                        using (new BGColoredScope(Color.green, Color.grey, addToFX))
                            addToFX = GUILayout.Toggle(addToFX, new GUIContent("Add to FX", "Add new Layers to the FX Controller for turning the Clothes On and Off"), "button");

                        using (new BGColoredScope(Color.green, Color.grey, addToExpressions))
                            addToExpressions = GUILayout.Toggle(addToExpressions, new GUIContent("Add to Expressions Toggle Menu", "Add new Parameters to the Expression Menu and Parameters for turning the Clothes On and Off"), "button");
                    }
                }


            }



            if (dressTab)
            {
                EditorGUI.BeginDisabledGroup(!vrca || !mainMesh || mergeMeshes.Count(t => t.isSkinned) < 1);
                if (GUILayout.Button("Merge"))
                {
                    MergeDressMeshes();
                }

                EditorGUI.EndDisabledGroup();
            }

            if (propTab)
            {
                using (new EditorGUI.DisabledScope(!vrca || propMeshes.Count(t => t.root != null) < 1))
                    if (GUILayout.Button("Add"))
                        AddDressProps();

            }

            if (undressTab)
            {
                using (new EditorGUI.DisabledScope(!vrca))
                {
                    detectedMeshes.ForEach(m =>
                    {
                        using (new GUILayout.HorizontalScope("box"))
                        {
                            EditorGUILayout.LabelField(m.parameter);
                            m.option = (RemovableMesh.RemovalOption)EditorGUILayout.EnumFlagsField(m.option);
                        }
                    });

                    using (new EditorGUI.DisabledScope(detectedMeshes.All(m => m.option == RemovableMesh.RemovalOption.Ignore)))
                    using (new BGColoredScope(Color.red))
                        if (GUILayout.Button("Remove"))
                        {
                            if (EditorUtility.DisplayDialog("Caution!", "This action cannot be undone. Continue?", "Ok", "Cancel"))
                            {
                                Debug.Log("<color=green>[Winter Dresser]</color> Starting Removal.");
                                if (detectedMeshes.Any(m => m.option.HasFlag(RemovableMesh.RemovalOption.RemoveFX)))
                                {
                                    List<string> FXToRemove = detectedMeshes.Where(r => r.option.HasFlag(RemovableMesh.RemovalOption.RemoveFX)).Select(r => identifier + r.parameter).ToList();
                                    IterateControllers(vrca, c =>
                                    {
                                        for (int i = c.layers.Length - 1; i >= 0; i--)
                                        {
                                            if (GetTags(c.layers[i]).Any(tag => tag.StartsWith(identifier) && FXToRemove.Contains(tag)))
                                            {
                                                Debug.Log($"<color=green>[Winter Dresser]</color> Removed Layer {c.layers[i].name} from {c.name}");
                                                c.RemoveLayer(i);
                                            }
                                        }

                                        for (int i = c.parameters.Length - 1; i >= 0; i--)
                                        {
                                            if (c.parameters[i].name.StartsWith(identifier) && FXToRemove.Contains(c.parameters[i].name))
                                            {
                                                Debug.Log($"<color=green>[Winter Dresser]</color> Removed Parameter {c.parameters[i].name} from {c.name}");
                                                c.RemoveParameter(i);
                                            }
                                        }
                                    });
                                }

                                if (detectedMeshes.Any(m => m.option.HasFlag(RemovableMesh.RemovalOption.RemoveExpression)))
                                {
                                    List<string> ExpressionToRemove = detectedMeshes.Where(r => r.option.HasFlag(RemovableMesh.RemovalOption.RemoveExpression)).Select(r => identifier + r.parameter).ToList();
                                    if (vrca.expressionParameters)
                                    {
                                        VRCExpressionParameters.Parameter[] newParams = vrca.expressionParameters.parameters;
                                        for (int i = newParams.Length - 1; i >= 0; i--)
                                        {
                                            if (ExpressionToRemove.Contains(newParams[i].name))
                                            {
                                                Debug.Log($"<color=green>[Winter Dresser]</color> Removed Parameter {newParams[i].name} from Expression Parameters");
                                                ArrayUtility.RemoveAt(ref newParams, i);
                                            }
                                        }

                                        vrca.expressionParameters.parameters = newParams;

                                        EditorUtility.SetDirty(vrca.expressionParameters);
                                    }

                                    if (vrca.expressionsMenu)
                                    {
                                        IterateMenus(vrca.expressionsMenu, m =>
                                        {
                                            for (int i = m.controls.Count - 1; i >= 0; i--)
                                            {
                                                if (m.controls[i].type == VRCExpressionsMenu.Control.ControlType.Toggle && ExpressionToRemove.Contains(m.controls[i].parameter.name))
                                                {
                                                    Debug.Log($"<color=green>[Winter Dresser]</color> Removed Control {m.controls[i].name} from Expression Menu \"{m.name}\"");
                                                    m.controls.RemoveAt(i);
                                                }
                                            }
                                        });
                                        EditorUtility.SetDirty(vrca.expressionsMenu);
                                    }

                                }

                                if (detectedMeshes.Any(m => m.option.HasFlag(RemovableMesh.RemovalOption.RemoveMesh)))
                                {
                                    List<string> MeshToRemove = detectedMeshes.Where(r => r.option.HasFlag(RemovableMesh.RemovalOption.RemoveMesh)).Select(r => identifier + r.parameter).ToList();
                                    List<Transform> allWinterMeshes = vrca.GetComponentsInChildren<Transform>(true).Where(t => t.name.StartsWith(identifier)).ToList();
                                    if (allWinterMeshes.Count > 0 && MeshToRemove.Count > 0)
                                    {
                                        List<string> deletedMeshNames = new List<string>();

                                        List<RendererInfo> allRenderersInfo = new List<RendererInfo>();
                                        foreach (var renderer in vrca.GetComponentsInChildren<Renderer>(true))
                                            allRenderersInfo.Add(new RendererInfo(renderer));


                                        for (int i = allWinterMeshes.Count - 1; i >= 0; i--)
                                        {
                                            if (!allWinterMeshes[i]) continue;
                                            if (!MeshToRemove.Contains(allWinterMeshes[i].name)) continue;

                                            deletedMeshNames.AddRange(from info in allRenderersInfo where (info.renderer != null && info.mesh && (info.renderer.transform == allWinterMeshes[i] || info.renderer.transform.IsChildOf(allWinterMeshes[i]))) select info.mesh.name);

                                            Debug.Log($"<color=green>[Winter Dresser]</color> Removed GameObject {allWinterMeshes[i].name}");
                                            DestroyImmediate(allWinterMeshes[i].gameObject);
                                        }

                                        foreach (var otherRenderer in allRenderersInfo)
                                        {
                                            foreach (var (_, index) in otherRenderer.togglesShapes.Where(pair => deletedMeshNames.Contains(pair.Item1)))
                                            {
                                                if (otherRenderer.renderer)
                                                {
                                                    ((SkinnedMeshRenderer)otherRenderer.renderer).SetBlendShapeWeight(index, 0);
                                                    EditorUtility.SetDirty(otherRenderer.renderer);
                                                }
                                            }
                                        }
                                    }
                                }

                                Debug.Log("<color=green>[Winter Dresser]</color> Finished Removing!");
                            }

                            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                            OnAvatarChanged();
                        }
                }
            }

            if (dressTab || propTab)
            {
                DrawSeparator();
                AssetFolderPath(ref assetFolderPath, "New Assets Path", "WinterDresserAssetFolderPath");
                DreadCredit();
            }


            GUILayout.EndScrollView();


        }


        #region Main Methods

        private static void PrepareVariables()
        {
            _folderPath = string.Empty;
            ReadyPath(folderPath);
            
            myMenu = null;
            myParameters = null;
        }
        private static void MergeDressMeshes()
        {
            DressMesh[] filteredTargets = mergeMeshes.Where(t => t.isSkinned).ToArray();
            string[] parameterNames = new string[filteredTargets.Length];
            for (int i = 0; i < parameterNames.Length; i++)
            {
                parameterNames[i] = GenerateUniqueString(filteredTargets[i].root.name, s => detectedMeshes.All(m => m.parameter != s));
                parameterNames[i] = "[WD] " + parameterNames[i];
            }
            var targetParameterPair = filteredTargets.Zip(parameterNames, (target, parameter) => (target, parameter));

            PrepareVariables();
            CheckIfValid(filteredTargets);
            
            #region Make Backup
            if (makeBackup)
            {
                vrca.name = vrca.name.Replace(" (WDBackup)", "");
                if (!Resources.FindObjectsOfTypeAll<GameObject>().Any(go => !EditorUtility.IsPersistent(go.transform.root.gameObject) && !(go.hideFlags == HideFlags.NotEditable || go.hideFlags == HideFlags.HideAndDontSave) && go.transform.root.name == vrca.gameObject.name + " (WDBackup)"))
                {
                    GameObject backupInstance = Instantiate(vrca.gameObject);
                    backupInstance.name = backupInstance.name.Replace("(Clone)", " (WDBackup)");
                    backupInstance.SetActive(false);
                }
            }
            #endregion

            #region Merge Rigs

            foreach (var (target, parameter) in targetParameterPair)
            {
                if (PrefabUtility.IsPartOfAnyPrefab(target.root.gameObject))
                    PrefabUtility.UnpackPrefabInstance(target.root.gameObject, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                foreach (var rendererInfo in target.RenderersInfo)
                {
                    ((SkinnedMeshRenderer)rendererInfo.renderer).bones = mainMesh.bones;
                    ((SkinnedMeshRenderer)rendererInfo.renderer).localBounds = mainMesh.localBounds;
                    ((SkinnedMeshRenderer)rendererInfo.renderer).rootBone = mainMesh.rootBone;
                    EditorUtility.SetDirty(rendererInfo.renderer);
                    if (target.RenderersInfo.Count == 1)
                        rendererInfo.renderer.transform.parent = mainMesh.transform.parent;
                }

                target.toggleTarget.gameObject.SetActive(target.defaultEnabled);
                target.toggleTarget.name = parameter;
            }
            #endregion

            #region Add Blendshapes to Clips
            if (clipsIntegrateMeshes)
                IntegrateMeshesIntoClip(filteredTargets);
            #endregion

            #region Make Toggles

            AnimatorController FXController = GetPlayableLayer(vrca, VRCAvatarDescriptor.AnimLayerType.FX);

            List<VRCExpressionsMenu.Control> newControls = new List<VRCExpressionsMenu.Control>();
            List<VRCExpressionParameters.Parameter> newParameters = new List<VRCExpressionParameters.Parameter>();

            List<RendererInfo> allRenderersInfo = new List<RendererInfo>();
            foreach (var renderer in vrca.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                allRenderersInfo.Add(new RendererInfo(renderer));

            foreach (var (target, parameter) in targetParameterPair)
            {
                newControls.Add(new VRCExpressionsMenu.Control() { name = target.root.name, parameter = new VRCExpressionsMenu.Control.Parameter() { name = parameter }, type = VRCExpressionsMenu.Control.ControlType.Toggle, value = 1, icon = target.icon});
                newParameters.Add(new VRCExpressionParameters.Parameter { name = parameter, valueType = VRCExpressionParameters.ValueType.Bool, saved = true, defaultValue = target.defaultEnabled ? 1 : 0 });

                AnimationClip onClip = null;
                AnimationClip offClip = null;

                if (createClips) CreateClip(allRenderersInfo, target, false, out onClip, out offClip);
                
                if (addToFX)
                {
                    ReadyParameter(FXController, new AnimatorControllerParameter() { name = parameter, type = AnimatorControllerParameterType.Bool, defaultBool = target.defaultEnabled });
                    AnimatorControllerLayer newToggleLayer = AddLayer(FXController, GenerateUniqueString(target.root.name.Replace(identifier, ""), s => FXController.layers.All(l => l.name != s)), 1);
                    AddTag(newToggleLayer, parameter);

                    AnimatorStateMachine m = newToggleLayer.stateMachine;
                    AnimatorState onState = m.AddState("On", m.entryPosition + new Vector3(-110, 80));
                    AnimatorState offState = m.AddState("Off", m.entryPosition + new Vector3(110, 80));
                    onState.motion = onClip;
                    offState.motion = offClip;

                    m.defaultState = target.defaultEnabled ? onState : offState;
                    var t = onState.AddTransition(offState, false);
                    t.duration = 0;
                    t.AddCondition(AnimatorConditionMode.IfNot, 0, parameter);

                    t = offState.AddTransition(onState, false);
                    t.duration = 0;
                    t.AddCondition(AnimatorConditionMode.If, 0, parameter);
                }
            }

            ModifyMenuAndParameters(newParameters, newControls);
            #endregion

            #region Clean up Remains
            foreach (var r in filteredTargets)
            {
                if (r.RenderersInfo.Count == 1)
                    DestroyImmediate(r.root.gameObject);
                else
                    DestroyImmediate(r.armature);
            }
            #endregion

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            OnAvatarChanged();
        }

        private static void AddDressProps()
        {
            Animator vrcanimator = vrca.GetComponent<Animator>();
            if (!vrcanimator)
            {
                EditorUtility.DisplayDialog("Warning!", "Avatar does not have an Animator Component. Props can't be added.", "Ok");
                return;
            }

            DressMesh[] filteredTargets = propMeshes.Where(t => t.propTargetBone >= 0).ToArray();
            string[] parameterNames = new string[filteredTargets.Length];
            for (int i = 0; i < parameterNames.Length; i++)
            {
                parameterNames[i] = GenerateUniqueString(filteredTargets[i].root.name, s => detectedMeshes.All(m => m.parameter != s));
                parameterNames[i] = identifier + parameterNames[i];
            }
            var targetParameterPair = filteredTargets.Zip(parameterNames, (target, parameter) => (target, parameter));

            CheckIfValid(filteredTargets);
            PrepareVariables();

            foreach (var (target, parameter) in targetParameterPair)
            {
                if (PrefabUtility.IsPartOfAnyPrefab(target.root.gameObject))
                    PrefabUtility.UnpackPrefabInstance(target.root.gameObject, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                target.root.gameObject.name = parameter;
                Transform targetParent = vrca.transform;
                if (target.propTargetBone > 0)
                    targetParent = vrcanimator.GetBoneTransform((HumanBodyBones)((int)target.propTargetBone - 1));
                target.root.parent = targetParent;
                target.root.gameObject.SetActive(target.defaultEnabled);
            }

            if (clipsIntegrateMeshes)
            {
                var filtered = filteredTargets.Where(m => m.isSkinned).ToArray();
                IntegrateMeshesIntoClip(filtered);
            }

            AnimatorController FXController = GetPlayableLayer(vrca, VRCAvatarDescriptor.AnimLayerType.FX);
            
            List<VRCExpressionsMenu.Control> newControls = new List<VRCExpressionsMenu.Control>();
            List<VRCExpressionParameters.Parameter> newParameters = new List<VRCExpressionParameters.Parameter>();

            List<RendererInfo> allRenderersInfo = new List<RendererInfo>();
            foreach (var renderer in vrca.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                allRenderersInfo.Add(new RendererInfo(renderer));

            foreach (var (target, parameter) in targetParameterPair)
            {
                newControls.Add(new VRCExpressionsMenu.Control() { name = target.root.name.Replace(identifier, ""), parameter = new VRCExpressionsMenu.Control.Parameter() { name = parameter }, type = VRCExpressionsMenu.Control.ControlType.Toggle, value = 1, icon = target.icon });
                newParameters.Add(new VRCExpressionParameters.Parameter { name = parameter, valueType = VRCExpressionParameters.ValueType.Bool, saved = true, defaultValue = target.defaultEnabled ? 1 : 0 });

                AnimationClip onClip = null;
                AnimationClip offClip = null;

                if (createClips) CreateClip(allRenderersInfo, target, true, out onClip, out offClip);
                

                if (addToFX)
                {
                    ReadyParameter(FXController, new AnimatorControllerParameter() { name = parameter, type = AnimatorControllerParameterType.Bool, defaultBool = target.defaultEnabled });
                    AnimatorControllerLayer newToggleLayer = AddLayer(FXController, GenerateUniqueString(target.root.name.Replace(identifier, ""), s => FXController.layers.All(l => l.name != s)), 1);
                    AddTag(newToggleLayer, parameter);

                    AnimatorStateMachine m = newToggleLayer.stateMachine;
                    AnimatorState onState = m.AddState("On", m.entryPosition + new Vector3(-110, 80));
                    AnimatorState offState = m.AddState("Off", m.entryPosition + new Vector3(110, 80));
                    onState.motion = onClip;
                    offState.motion = offClip;

                    m.defaultState = target.defaultEnabled ? onState : offState;

                    var t = onState.AddTransition(offState, false);
                    t.duration = 0;
                    t.AddCondition(AnimatorConditionMode.IfNot, 0, parameter);

                    t = offState.AddTransition(onState, false);
                    t.duration = 0;
                    t.AddCondition(AnimatorConditionMode.If, 0, parameter);
                }
            }

            ModifyMenuAndParameters(newParameters, newControls);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            OnAvatarChanged();
        }

        private static readonly HashSet<string> currentMeshNames = new HashSet<string>();

        private static void CreateClip(List<RendererInfo> allRenderers, DressMesh target, bool isProp, out AnimationClip onClip, out AnimationClip offClip)
        {
            currentMeshNames.Clear();
            onClip = new AnimationClip();
            offClip = new AnimationClip();

            if (isProp)
            {
                onClip.SetCurve(APath(target.root), typeof(GameObject), "m_IsActive", AnimationCurve.Constant(0, 1 / 60f, 1));
                offClip.SetCurve(APath(target.root), typeof(GameObject), "m_IsActive", AnimationCurve.Constant(0, 1 / 60f, 0));
            }

            foreach (var rendererInfo in target.RenderersInfo)
            {
                if (!isProp)
                {
                    onClip.SetCurve(APath(rendererInfo.renderer.transform), typeof(GameObject), "m_IsActive", AnimationCurve.Constant(0, 1 / 60f, 1));
                    offClip.SetCurve(APath(rendererInfo.renderer.transform), typeof(GameObject), "m_IsActive", AnimationCurve.Constant(0, 1 / 60f, 0));
                } 
                currentMeshNames.Add(rendererInfo.mesh?.name);
            }

            foreach (var otherRenderer in allRenderers)
            {
                foreach (var (shape, index) in otherRenderer.togglesShapes.Where(pair => currentMeshNames.Contains(pair.Item1)))
                {
                    onClip.SetCurve(APath(otherRenderer.renderer.transform), typeof(SkinnedMeshRenderer), $"blendShape.{shape}^", AnimationCurve.Constant(0, 1 / 60f, 100));
                    offClip.SetCurve(APath(otherRenderer.renderer.transform), typeof(SkinnedMeshRenderer), $"blendShape.{shape}^", AnimationCurve.Constant(0, 1 / 60f, 0));

                    if (target.defaultEnabled)
                    {
                        ((SkinnedMeshRenderer) otherRenderer.renderer).SetBlendShapeWeight(index, 100);
                        EditorUtility.SetDirty(otherRenderer.renderer);
                    }
                }
            }

            string clipPath = $"{folderPath}/Animations";
            ReadyPath(clipPath);
            AssetDatabase.CreateAsset(onClip, AssetDatabase.GenerateUniqueAssetPath(clipPath + "/" + target.root.name + " On.anim"));
            AssetDatabase.CreateAsset(offClip, AssetDatabase.GenerateUniqueAssetPath(clipPath + "/" + target.root.name + " Off.anim"));

        }

        private static void IntegrateMeshesIntoClip(params DressMesh[] myMeshes)
        {
            HashSet<AnimationClip> visitedClips = new HashSet<AnimationClip>();
            HashSet<ClipMeshInfo> validClips = new HashSet<ClipMeshInfo>();

            IterateStates(vrca, s =>
            {
                IterateMotions(s.motion, m =>
                {
                    if (m)
                    {
                        AnimationClip currentClip = (AnimationClip)m;
                        if (!visitedClips.Contains(currentClip))
                        {
                            visitedClips.Add(currentClip);
                            ClipMeshInfo clipInfo = new ClipMeshInfo(currentClip);
                            if (clipInfo.PathBlendshapeCurve.Count > 0)
                                validClips.Add(clipInfo);
                        }
                    }
                });
            });

            HashSet<string> visitedShapes = new HashSet<string>();
            foreach (var rendererInfo in myMeshes.SelectMany(t => t.RenderersInfo))
            {
                string rendererPath = APath(rendererInfo.renderer.transform);
                foreach (var clipInfo in validClips)
                {
                    visitedShapes.Clear();
                    foreach (var (_, shapeName, animationCurve) in clipInfo.PathBlendshapeCurve)
                    {
                        if (!visitedShapes.Contains(shapeName))
                        {
                            visitedShapes.Add(shapeName);
                            if (rendererInfo.blendshapes.Contains(shapeName))
                            {
                                if (clipInfo.PathBlendshapeCurve.Where(p => p.Item2 == shapeName).All(p => p.Item1 != rendererPath))
                                {
                                    clipInfo.clip.SetCurve(rendererPath, typeof(SkinnedMeshRenderer), $"blendShape.{shapeName}", animationCurve);
                                }
                            }
                        }
                    }
                }
            }
        }

        private static void CheckIfValid(params DressMesh[] myMeshes)
        {
            if (addToFX && !GetPlayableLayer(vrca, VRCAvatarDescriptor.AnimLayerType.FX))
                DisplayError("Missing FX Controller in Avatar Descriptor");

            if (!addToExpressions) return;
            HandleMenuAndParameters();
            
            if (MAX_PARAMETER_COST - (myParameters.CalcTotalCost() + myMeshes.Length) < 0)
                DisplayError("Not enough free memory in Expression Parameters");

            int freeSlots = (8 - myMenu.controls.Count) * 8 
                            + myMenu.controls.Where(c => c.subMenu && c.type == VRCExpressionsMenu.Control.ControlType.SubMenu).Sum(c => 8 - c.subMenu.controls.Count);
            
            if (freeSlots < myMeshes.Length)
                DisplayError("Not enough free memory in Expression Menu");

        }

        private static void HandleMenuAndParameters()
        {
            string vrcPath = $"{folderPath}/VRC";
            ReadyPath(vrcPath);
            myMenu = expressionMenu;
            if (!myMenu)
            {
                myMenu = ReadyExpressionsMenu(vrca, vrcPath);
                VRCExpressionsMenu clothingMenu = myMenu;
                
                clothingMenu = clothingMenu.controls.FirstOrDefault(c => c.type == VRCExpressionsMenu.Control.ControlType.SubMenu && c.subMenu && c.subMenu.name.Contains("Clothing+Props"))?.subMenu;
                if (!clothingMenu)
                {
                    clothingMenu = ScriptableObject.CreateInstance<VRCExpressionsMenu>();
                    AssetDatabase.CreateAsset(clothingMenu, $"{vrcPath}/{vrca.name} Clothing+Props.asset");
                    myMenu.AddControls(new VRCExpressionsMenu.Control() {name = "Clothing+Props", type = VRCExpressionsMenu.Control.ControlType.SubMenu, subMenu = clothingMenu});
                }

                myMenu = expressionMenu = clothingMenu;
            }

            myParameters = ReadyExpressionParameters(vrca, vrcPath);
        }

        private static void ModifyMenuAndParameters(List<VRCExpressionParameters.Parameter> newParameters, List<VRCExpressionsMenu.Control> newControls)
        {
            if (!addToExpressions) return;
            AddParameters(myParameters, newParameters);
            var validMenus = myMenu.controls.Where(c => c.type == VRCExpressionsMenu.Control.ControlType.SubMenu && c.subMenu && c.subMenu.name.Contains("Toggles") && c.subMenu.controls.Count < 8).Select(c => c.subMenu).ToList();

            foreach (var c in newControls)
            {
                if (validMenus.Count == 0)
                {
                    int index = myMenu.controls.Count + 1;
                    var newMenu = ScriptableObject.CreateInstance<VRCExpressionsMenu>();
                    AssetDatabase.CreateAsset(newMenu, $"{folderPath}/VRC/Toggles {index}.asset");
                    myMenu.controls.Add(new VRCExpressionsMenu.Control() { name = $"Toggles {index}", type = VRCExpressionsMenu.Control.ControlType.SubMenu, subMenu = newMenu });
                    validMenus.Add(newMenu);
                }

                var currentMenu = validMenus[0];

                currentMenu.controls.Add(c);

                if (currentMenu.controls.Count == 8)
                    validMenus.RemoveAt(0);
            }
        }
        #endregion

        #region Automated Methods
        private static void OnAvatarChanged()
        {
            if (!vrca)
                return;

            mergeMeshes.Clear();
            propMeshes.Clear();
            detectedMeshes.Clear();
            mainMesh = null;

            Transform vrcaRoot = vrca.transform;
            for (int i = 0; i < vrcaRoot.childCount; i++)
            {
                Transform c = vrcaRoot.GetChild(i);

                if (!mainMesh)
                {
                    SkinnedMeshRenderer potentialMainMesh = c.GetComponent<SkinnedMeshRenderer>();
                    if (potentialMainMesh && c.name == "Body")
                        mainMesh = potentialMainMesh;
                }

                DressMesh potentialTarget = new DressMesh(c);
                if (potentialTarget.isSkinned && !potentialTarget.isProp)
                    mergeMeshes.Add(potentialTarget);
                else if (potentialTarget.isProp)
                    propMeshes.Add(potentialTarget);
            }

            if (!mainMesh)
                mainMesh = vrca.VisemeSkinnedMesh;
            if (!mainMesh)
                mainMesh = vrca.customEyeLookSettings.eyelidsSkinnedMesh;

            if (!mainMesh)
            {
                for (int i = 0; i < vrcaRoot.childCount; i++)
                {
                    Transform c = vrcaRoot.GetChild(i);
                    SkinnedMeshRenderer potentialMainMesh = c.GetComponent<SkinnedMeshRenderer>();
                    if (potentialMainMesh)
                        mainMesh = potentialMainMesh;
                }
            }

            List<string> detectedStrings = new List<string>();
            IterateControllers(vrca, controller =>
            {
                IterateLayers(controller, l =>
                {
                    foreach (string s in GetTags(l).Where(s => s.StartsWith(identifier)))
                    {
                        detectedStrings.Add(s.Replace(identifier, ""));
                    }
                });
            });

            if (vrca.expressionParameters)
            {
                VRCExpressionParameters.Parameter[] myParams = vrca.expressionParameters.parameters;
                for (int i = myParams.Length - 1; i >= 0; i--)
                {
                    if (myParams[i].name.StartsWith(identifier))
                        detectedStrings.Add(myParams[i].name.Replace(identifier, ""));
                }
            }

            if (vrca.expressionsMenu)
            {
                IterateMenus(vrca.expressionsMenu, m =>
                {
                    detectedStrings.AddRange(m.controls.Where(c => c.type == VRCExpressionsMenu.Control.ControlType.Toggle && c.parameter.name.StartsWith(identifier)).Select(c => c.parameter.name.Replace(identifier, "")));
                });
            }

            detectedStrings.AddRange(vrca.GetComponentsInChildren<Transform>(true).Where(t => t.name.StartsWith(identifier)).Select(t => t.name.Replace(identifier, "")));

            detectedStrings = detectedStrings.Distinct().ToList();
            detectedStrings.ForEach(s => detectedMeshes.Add(new RemovableMesh(s)));
        }

        private void OnEnable()
        {
            greenLight = new GUIContent(EditorGUIUtility.IconContent("d_greenLight")) { tooltip = "Enabled" };
            redLight = new GUIContent(EditorGUIUtility.IconContent("d_redLight")) { tooltip = "Disabled" };
            warnIcon = new GUIContent(EditorGUIUtility.IconContent("d_console.warnicon.sml"));

            assetFolderPath = PlayerPrefs.GetString("WinterDresserAssetFolderPath", "Assets/DreadScripts/Winter Dresser/Generated Assets");
            advancedPopupMethod = typeof(EditorGUI).GetMethod("AdvancedPopup", BindingFlags.NonPublic | BindingFlags.Static, null, new[] { typeof(Rect), typeof(int), typeof(string[]) }, null);
            boneNames = Enum.GetNames(typeof(DressMesh.CustomBodyBones));
            
            if (!vrca)
            {
                vrca = FindObjectOfType<VRCAvatarDescriptor>();
                OnAvatarChanged();
            }

            RefreshList();
        }

        private void RefreshList()
        {
            mergeReorderableList = new UnityEditorInternal.ReorderableList(mergeMeshes, typeof(DressMesh), true, true, true, false)
            {
                drawElementCallback = DrawClothesElement,
                drawHeaderCallback = DrawClothesHeader
            };

            propReorderableList = new UnityEditorInternal.ReorderableList(propMeshes, typeof(DressMesh), true, true, true, false)
            {
                drawElementCallback = DrawPropsElement,
                drawHeaderCallback = DrawPropsHeader
            };

        }

        #endregion

        #region GUI Methods
        private static void DrawSeparator(int thickness = 2, int padding = 10)
        {
            Rect r = EditorGUILayout.GetControlRect(GUILayout.Height(thickness + padding));
            r.height = thickness;
            r.y += padding / 2;
            r.x -= 2;
            r.width += 6;
            ColorUtility.TryParseHtmlString(EditorGUIUtility.isProSkin ? "#595959" : "#858585", out Color lineColor);
            EditorGUI.DrawRect(r, lineColor);
        }

        private void DrawClothesElement(Rect rect, int index, bool isActive, bool isFocused)
        {
            if (!(index < mergeMeshes.Count && index >= 0))
                return;
            if (GUI.Button(new Rect(rect.x, rect.y + 2, 20, EditorGUIUtility.singleLineHeight), "X"))
            {
                mergeMeshes.RemoveAt(index);
                return;
            }

            DressMesh dressMesh = mergeMeshes[index];
            Rect myRect = new Rect(rect.x + 22, rect.y + 2, rect.width - 100, EditorGUIUtility.singleLineHeight);
            
            EditorGUI.BeginChangeCheck();
            Transform dummy = (Transform)EditorGUI.ObjectField(myRect, dressMesh.root, typeof(Transform), true);
            if (EditorGUI.EndChangeCheck())
            {
                if (dummy == null) mergeMeshes[index] = new DressMesh(null);
                else
                {
                    if (dummy.gameObject.scene.IsValid()) mergeMeshes[index] = new DressMesh(dummy);
                    else Debug.LogWarning("[WinterDresser] GameObject must be a scene object!");
                }

            }

            Rect iconRect = new Rect(myRect);
            iconRect.x += myRect.width + 4;
            iconRect.width = iconRect.height = 18;

            dressMesh.icon = (Texture2D)EditorGUI.ObjectField(iconRect, dressMesh.icon, typeof(Texture2D), false);


            float xCoord = rect.x + rect.width - 38;
            if (!dressMesh.root) EditorGUI.LabelField(new Rect(xCoord, rect.y + 2, 25, EditorGUIUtility.singleLineHeight), new GUIContent(warnIcon) { tooltip = "Field is empty" });
            if (!dressMesh.isSkinned) EditorGUI.LabelField(new Rect(xCoord, rect.y + 2, 25, EditorGUIUtility.singleLineHeight), new GUIContent(warnIcon) { tooltip = "Target does not contain any Skinned Mesh Renderers" });
            if (GUI.Button(new Rect(xCoord + 20, rect.y + 2, 25, EditorGUIUtility.singleLineHeight), dressMesh.defaultEnabled ? greenLight : redLight, GUIStyle.none))
                dressMesh.defaultEnabled = !dressMesh.defaultEnabled;
        }

        private void DrawPropsElement(Rect rect, int index, bool isActive, bool isFocused)
        {
            if (!(index < propMeshes.Count && index >= 0))
                return;
            if (GUI.Button(new Rect(rect.x, rect.y + 2, 20, EditorGUIUtility.singleLineHeight), "X"))
            {
                propMeshes.RemoveAt(index);
                return;
            }

            DressMesh dressMesh = propMeshes[index];
            Rect myRect = new Rect(rect.x + 22, rect.y + 2, rect.width - 244, EditorGUIUtility.singleLineHeight);
            EditorGUI.BeginChangeCheck();

            Transform dummy = (Transform)EditorGUI.ObjectField(myRect, dressMesh.root, typeof(Transform), true);

            if (EditorGUI.EndChangeCheck())
            {
                if (dummy == null)
                    propMeshes[index] = new DressMesh(null);
                else
                {
                    if (dummy.gameObject.scene.IsValid())
                        propMeshes[index] = new DressMesh(dummy);
                    else
                        Debug.LogWarning("[WinterDresser] GameObject must be a scene object!");
                }
            }

            Rect boneRect = new Rect( myRect.x + myRect.width + 4, rect.y + 2, 140, EditorGUIUtility.singleLineHeight);
            //dressMesh.propTargetBone = (DressMesh.CustomBodyBones)EditorGUI.EnumPopup(boneRect, dressMesh.propTargetBone);

            int pickedIndex = (int) dressMesh.propTargetBone;
            object[] arguments = { boneRect, pickedIndex, boneNames};
            int picked = (int)advancedPopupMethod.Invoke(null, arguments);
            if (picked != pickedIndex)
                dressMesh.propTargetBone = (DressMesh.CustomBodyBones)picked;

            Rect iconRect = new Rect(boneRect);
            iconRect.x += boneRect.width + 4;
            iconRect.width = iconRect.height = 18;

            dressMesh.icon = (Texture2D)EditorGUI.ObjectField(iconRect, dressMesh.icon, typeof(Texture2D), false);
            
            float xCoord = rect.x + rect.width - 38;
            if (!dressMesh.root)
                EditorGUI.LabelField(new Rect(xCoord, rect.y + 2, 25, EditorGUIUtility.singleLineHeight), new GUIContent(warnIcon) { tooltip = "Field is empty" });
            if (dressMesh.propTargetBone < 0)
                EditorGUI.LabelField(new Rect(xCoord, rect.y + 2, 25, EditorGUIUtility.singleLineHeight), new GUIContent(warnIcon) { tooltip = "Target requires a bone target" });
            if (GUI.Button(new Rect(xCoord + 20, rect.y + 2, 25, EditorGUIUtility.singleLineHeight), dressMesh.defaultEnabled ? greenLight : redLight, GUIStyle.none))
                dressMesh.defaultEnabled = !dressMesh.defaultEnabled;
        }

        private static void DrawClothesHeader(Rect rect) => DrawHeader(rect, "Meshes to merge with Avatar");
        private static void DrawPropsHeader(Rect rect) => DrawHeader(rect, "Props to put on Avatar");
        
        private static void DrawHeader(Rect rect, string headerTitle)
        {
            EditorGUI.LabelField(rect, headerTitle);
            GUI.Label(new Rect(rect.x + rect.width - 38, rect.y, 65, EditorGUIUtility.singleLineHeight), new GUIContent("On/Off", "Should this be On or Off by default?"));
            GUI.Label(new Rect(rect.x + rect.width - 78, rect.y, 65, EditorGUIUtility.singleLineHeight), new GUIContent("Icon", "The icon to display on the menu"));
        }

        private static void DreadCredit()
        {
            using (new GUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Made By Dreadrith#3238", "boldlabel"))
                    Application.OpenURL("https://linktr.ee/Dreadrith");
            }
        }
        private static void AssetFolderPath(ref string variable, string title, string playerpref)
        {
            using (new GUILayout.HorizontalScope())
            {
                EditorGUI.BeginDisabledGroup(true);
                EditorGUILayout.TextField(title, variable);
                EditorGUI.EndDisabledGroup();
                if (GUILayout.Button("...", GUILayout.Width(30)))
                {
                    var dummyPath = EditorUtility.OpenFolderPanel(title, AssetDatabase.IsValidFolder(variable) ? variable : string.Empty, string.Empty);
                    if (string.IsNullOrEmpty(dummyPath))
                        return;

                    if (!dummyPath.StartsWith("Assets"))
                    {
                        Debug.LogWarning("New Path must be a folder within Assets!");
                        return;
                    }

                    variable = FileUtil.GetProjectRelativePath(dummyPath);
                    PlayerPrefs.SetString(playerpref, variable);
                }
            }
        }
        private class BGColoredScope : System.IDisposable
        {
            private readonly Color ogColor;

            internal BGColoredScope(Color active, Color inactive, bool isActive)
            {
                ogColor = GUI.backgroundColor;
                GUI.backgroundColor = isActive ? active : inactive;
            }

            internal BGColoredScope(Color color)
            {
                ogColor = GUI.backgroundColor;
                GUI.backgroundColor = color;
            }
            public void Dispose()
            {
                GUI.backgroundColor = ogColor;
            }
        }
        #endregion

        #region Helper Methods

        private static int _MAX_PARAMETER_COST;
        private static int MAX_PARAMETER_COST
        {
            get
            {
                if (_MAX_PARAMETER_COST == 0)
                {
                    try
                    { _MAX_PARAMETER_COST = (int)typeof(VRCExpressionParameters).GetField("MAX_PARAMETER_COST", BindingFlags.Static | BindingFlags.Public).GetValue(null); }
                    catch
                    {
                        Debug.LogError("Failed to dynamically get MAX_PARAMETER_COST. Falling back to 256");
                        _MAX_PARAMETER_COST = 256;
                    }
                }

                return _MAX_PARAMETER_COST;
            }
        }

        private static void DisplayError(string error, string title = "Error")
        {
            EditorUtility.DisplayDialog(title, error, "OK");
            throw new Exception(error);
        }
        
        private static void ReadyPath(string path)
        {
            string[] folderNames = path.Split('/');
            string[] folderPaths = new string[folderNames.Length];

            for (int i = 0; i < folderNames.Length; i++)
            {
                folderPaths[i] = folderNames[0];
                for (int j = 1; j <= i; j++)
                    folderPaths[i] += $"/{folderNames[j]}";
            }

            for (int i = 1; i < folderPaths.Length; i++)
                if (!AssetDatabase.IsValidFolder(folderPaths[i]))
                    AssetDatabase.CreateFolder(folderPaths[i - 1], folderNames[i]);
        }
        private static VRCExpressionsMenu ReadyExpressionsMenu(VRCAvatarDescriptor avatar, string folderPath, bool createCopy = true)
        {
            VRCExpressionsMenu menu = avatar.expressionsMenu;
            if (menu)
            {
                if (createCopy) menu = CopyAssetAndReturn(menu, $"{folderPath}/{menu.name}.asset");
            }
            else
            {
                menu = ScriptableObject.CreateInstance<VRCExpressionsMenu>();
                AssetDatabase.CreateAsset(menu, $"{folderPath}/{avatar.name} Menu.asset");
            }
            avatar.customExpressions = true;
            avatar.expressionsMenu = menu;
            EditorUtility.SetDirty(avatar);
            return menu;
        }

        private static VRCExpressionParameters ReadyExpressionParameters(VRCAvatarDescriptor avatar, string folderPath, bool createCopy = true)
        {
            VRCExpressionParameters parameters = avatar.expressionParameters;
            if (parameters)
            {
                if (createCopy) parameters = CopyAssetAndReturn(parameters, $"{folderPath}/{parameters.name}.asset");
            }
            else
            {
                parameters = ScriptableObject.CreateInstance<VRCExpressionParameters>();
                parameters.parameters = Array.Empty<VRCExpressionParameters.Parameter>();
                AssetDatabase.CreateAsset(parameters, $"{folderPath}/{avatar.name} Parameters.asset");
            }
            avatar.customExpressions = true;
            avatar.expressionParameters = parameters;
            EditorUtility.SetDirty(avatar);
            return parameters;
        }
        private static T CopyAssetAndReturn<T>(T obj, string newPath) where T : Object
        {
            string assetPath = AssetDatabase.GetAssetPath(obj);
            Object mainAsset = AssetDatabase.LoadMainAssetAtPath(assetPath);

            if (!mainAsset) return null;
            if (obj != mainAsset)
            {
                T newAsset = Object.Instantiate(obj);
                AssetDatabase.CreateAsset(newAsset, newPath);
                return newAsset;
            }

            AssetDatabase.CopyAsset(assetPath, newPath);
            return AssetDatabase.LoadAssetAtPath<T>(newPath);
        }
        private static AnimatorController GetController(RuntimeAnimatorController controller)
        {
            return AssetDatabase.LoadAssetAtPath<AnimatorController>(AssetDatabase.GetAssetPath(controller));
        }
        private static AnimatorControllerLayer AddLayer(AnimatorController controller, string name, float defaultWeight)
        {
            var newLayer = new AnimatorControllerLayer
            {
                name = name,
                defaultWeight = defaultWeight,
                stateMachine = new AnimatorStateMachine
                {
                    name = name,
                    hideFlags = HideFlags.HideInHierarchy
                },
            };
            AssetDatabase.AddObjectToAsset(newLayer.stateMachine, controller);
            controller.AddLayer(newLayer);
            return newLayer;
        }
        private static void AddTag(AnimatorControllerLayer layer, string tag)
        {
            if (HasTag(layer, tag)) return;

            var t = layer.stateMachine.AddAnyStateTransition((AnimatorState)null);
            t.isExit = true;
            t.mute = true;
            t.name = tag;
        }
        private static bool HasTag(AnimatorControllerLayer layer, string tag)
        {
            return GetTags(layer).Any(s => s == tag);
        }

        private static IEnumerable<string> GetTags(AnimatorControllerLayer layer)
        {
            return layer.stateMachine.anyStateTransitions.Where(t => t.isExit && t.mute).Select(t => t.name);
        }

        private static void ReadyParameter(AnimatorController controller, AnimatorControllerParameter parameter)
        {
            if (!GetParameter(controller, parameter.name, out _))
                controller.AddParameter(parameter);
        }
        private static bool GetParameter(AnimatorController controller, string parameter, out int index)
        {
            index = -1;
            for (int i = 0; i < controller.parameters.Length; i++)
            {
                if (controller.parameters[i].name == parameter)
                {
                    index = i;
                    return true;
                }
            }
            return false;
        }

        private static void IterateMenus(VRCExpressionsMenu menu, System.Action<VRCExpressionsMenu> action, HashSet<VRCExpressionsMenu> visitedMenus = null)
        {
            if (visitedMenus == null)
                visitedMenus = new HashSet<VRCExpressionsMenu>();
            if (visitedMenus.Contains(menu)) return;

            action(menu);

            foreach (var subMenu in menu.controls.Where(c => c.subMenu && c.type == VRCExpressionsMenu.Control.ControlType.SubMenu).Select(c => c.subMenu))
                IterateMenus(subMenu, action, visitedMenus);
        }

        private static void IterateControllers(VRCAvatarDescriptor avi, System.Action<AnimatorController> action, bool nested = true)
        {
            HashSet<AnimatorController> visitedControllers = new HashSet<AnimatorController>();
            foreach (var playable in avi.baseAnimationLayers.Concat(avi.specialAnimationLayers))
            {
                var run = playable.animatorController;
                if (!run) continue;

                AnimatorController nextController = GetController(run);
                if (visitedControllers.Contains(nextController)) continue;

                visitedControllers.Add(nextController);
                action(nextController);
            }

            if (!nested) return;
            var animators = avi.GetComponentsInChildren<Animator>(true);
            {
                foreach (var ani in animators)
                {
                    var run = ani.runtimeAnimatorController;
                    if (!run) continue;

                    AnimatorController nextController = GetController(run);
                    if (visitedControllers.Contains(nextController)) continue;

                    visitedControllers.Add(nextController);
                    action(nextController);
                }
            }
        }

        private static void IterateStates(VRCAvatarDescriptor avi, System.Action<AnimatorState> action, bool nested = true)
        {
            IterateControllers(avi, c =>
            {
                IterateStates(c, action);
            });
        }
        private static void IterateStates(AnimatorController con, System.Action<AnimatorState> action)
        {
            foreach (AnimatorControllerLayer layer in con.layers)
                IterateStates(layer.stateMachine, action);
        }

        private static void IterateLayers(AnimatorController con, System.Action<AnimatorControllerLayer> action)
        {
            foreach (AnimatorControllerLayer layer in con.layers)
            {
                action(layer);
            }
        }

        private static void IterateStates(AnimatorStateMachine machine, System.Action<AnimatorState> stateAction, bool deep = true)
        {
            foreach (var cs in machine.states)
                stateAction(cs.state);

            if (deep)
                foreach (var cm in machine.stateMachines)
                    if (cm.stateMachine != machine)
                        IterateStates(cm.stateMachine, stateAction);
        }
        private static void IterateMotions(Motion myMotion, System.Action<Motion> action, bool applyToTree = false)
        {
            if (myMotion is BlendTree tree)
            {
                if (applyToTree)
                    action(myMotion);
                foreach (ChildMotion motion in tree.children)
                    IterateMotions(motion.motion, action, applyToTree);
            }
            else action(myMotion);
        }

        private static string GenerateUniqueString(string s, System.Func<string, bool> check)
        {
            if (check(s))
                return s;

            int suffix = 0;

            int.TryParse(s.Substring(s.Length - 2, 2), out int d);
            if (d >= 0)
                suffix = d;
            if (suffix > 0) s = suffix > 9 ? s.Substring(0, s.Length - 2) : s.Substring(0, s.Length - 1);

            s = s.Trim();

            suffix++;

            string newString = s + " " + suffix;
            while (!check(newString))
            {
                suffix++;
                newString = s + " " + suffix;
            }

            return newString;
        }

        private static void AddParameters(VRCExpressionParameters target, List<VRCExpressionParameters.Parameter> newParams, bool undo = false)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty("parameters");
            foreach (var p in newParams)
            {
                prop.arraySize++;
                var elem = prop.GetArrayElementAtIndex(prop.arraySize - 1);
                elem.FindPropertyRelative("name").stringValue = p.name;
                elem.FindPropertyRelative("valueType").enumValueIndex = (int)p.valueType;
                elem.FindPropertyRelative("saved").boolValue = p.saved;
                elem.FindPropertyRelative("defaultValue").floatValue = p.defaultValue;
            }

            if (undo) so.ApplyModifiedProperties();
            else so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static AnimatorController GetPlayableLayer(VRCAvatarDescriptor avi, VRCAvatarDescriptor.AnimLayerType type)
        {
            for (var i = 0; i < avi.baseAnimationLayers.Length; i++)
                if (avi.baseAnimationLayers[i].type == type)
                    return GetController(avi.baseAnimationLayers[i].animatorController);

            for (var i = 0; i < avi.specialAnimationLayers.Length; i++)
                if (avi.specialAnimationLayers[i].type == type)
                    return GetController(avi.specialAnimationLayers[i].animatorController);

            return null;
        }

        private static string APath(Transform t)
        {
            return AnimationUtility.CalculateTransformPath(t, vrca.transform);
        }
        #endregion

    }

    internal sealed class RemovableMesh
    {
        internal enum RemovalOption
        {
            Ignore = 0,
            RemoveFX = 1 << 0,
            RemoveMesh = 1 << 1,
            RemoveExpression = 1 << 2,
            RemoveAll = ~0
        }

        internal string parameter;
        internal RemovalOption option;

        internal RemovableMesh(string p)
        {
            parameter = p;
            option = RemovalOption.Ignore;
        }
    }
    internal sealed class DressMesh
    {
        internal Transform root;
        internal Transform armature;
        internal Transform toggleTarget;
        internal List<RendererInfo> RenderersInfo = new List<RendererInfo>();
        internal bool isSkinned;
        internal bool defaultEnabled = true;
        internal bool isProp;
        internal CustomBodyBones propTargetBone = CustomBodyBones.Root;

        internal Texture2D icon;

        internal enum CustomBodyBones
        {
            Root,
            Hips,
            LeftUpperLeg,
            RightUpperLeg,
            LeftLowerLeg,
            RightLowerLeg,
            LeftFoot,
            RightFoot,
            Spine,
            Chest,
            Neck,
            Head,
            LeftShoulder,
            RightShoulder,
            LeftUpperArm,
            RightUpperArm,
            LeftLowerArm,
            RightLowerArm,
            LeftHand,
            RightHand,
            LeftToes,
            RightToes,
            LeftEye,
            RightEye,
            Jaw,
            LeftThumbProximal,
            LeftThumbIntermediate,
            LeftThumbDistal,
            LeftIndexProximal,
            LeftIndexIntermediate,
            LeftIndexDistal,
            LeftMiddleProximal,
            LeftMiddleIntermediate,
            LeftMiddleDistal,
            LeftRingProximal,
            LeftRingIntermediate,
            LeftRingDistal,
            LeftLittleProximal,
            LeftLittleIntermediate,
            LeftLittleDistal,
            RightThumbProximal,
            RightThumbIntermediate,
            RightThumbDistal,
            RightIndexProximal,
            RightIndexIntermediate,
            RightIndexDistal,
            RightMiddleProximal,
            RightMiddleIntermediate,
            RightMiddleDistal,
            RightRingProximal,
            RightRingIntermediate,
            RightRingDistal,
            RightLittleProximal,
            RightLittleIntermediate,
            RightLittleDistal,
            UpperChest
        }
        
        public DressMesh() { }

        internal DressMesh(Transform root)
        {
            this.root = root;
            if (!root) return;
            toggleTarget = root;

            if (root.childCount > 0)
            {
                armature = root.Find("Armature");
                if (!armature)
                    armature = root.GetChild(0);
            }

            isProp = GetPropInfo(root, out propTargetBone);

            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);

                if (isProp = GetPropInfo(child, out propTargetBone))
                    break;

                Renderer r = child.GetComponent<Renderer>();
                if (r) RenderersInfo.Add(new RendererInfo(r));
            }

            if (isProp)
            {
                Renderer r = root.GetComponent<Renderer>();
                if (r) RenderersInfo.Add(new RendererInfo(r));
            }

            isSkinned = RenderersInfo.Count > 0;
            //isWeighted = isSkinned && RenderersInfo.Any(r => r.renderer is SkinnedMeshRenderer skin && skin.bones.Length > 0);
            if (isSkinned && RenderersInfo.Count == 1)
                toggleTarget = RenderersInfo[0].renderer.transform;
            defaultEnabled = toggleTarget.gameObject.activeInHierarchy;
        }

        internal static bool GetPropInfo(Transform t, out CustomBodyBones targetBone)
        {
            targetBone = CustomBodyBones.Root;
            Mesh myMesh = GetMesh(t);
            if (!myMesh)
                return false;

            string[] labels = AssetDatabase.GetLabels(myMesh);
            foreach (var l in labels)
            {
                if (Enum.TryParse(l, out targetBone))
                {
                    return true;
                }
            }

            return false;
        }

        internal static Mesh GetMesh(Transform t)
        {
            Mesh myMesh = null;
            Component Component = t.GetComponent<MeshFilter>();
            if (Component)
                myMesh = ((MeshFilter)Component).sharedMesh;

            if (myMesh) return myMesh;

            Component = t.GetComponent<SkinnedMeshRenderer>();
            if (Component)
                myMesh = ((SkinnedMeshRenderer)Component).sharedMesh;

            return myMesh;
        }
    }
    internal struct RendererInfo
    {
        internal Renderer renderer;
        internal HashSet<string> blendshapes;
        internal List<System.Tuple<string, int>> togglesShapes;
        internal Mesh mesh;
        private bool isValid;
        private bool isSkinned;
        internal RendererInfo(Renderer r)
        {
            renderer = r;
            blendshapes = new HashSet<string>();
            togglesShapes = new List<Tuple<string, int>>();
            mesh = DressMesh.GetMesh(r.transform);
            isValid = isSkinned = false;
            if (!mesh) return;
            isValid = true;
            isSkinned = r as SkinnedMeshRenderer;
            if (isSkinned)
            {
                for (int i = 0; i < mesh.blendShapeCount; i++)
                {
                    string shapeName = mesh.GetBlendShapeName(i);
                    blendshapes.Add(shapeName);
                    if (shapeName.EndsWith("^"))
                        togglesShapes.Add(new Tuple<string, int>(shapeName.Replace("^", ""), i));
                }
            }
        }
    }
    internal struct ClipMeshInfo
    {
        internal AnimationClip clip;
        internal List<System.Tuple<string, string, AnimationCurve>> PathBlendshapeCurve;

        internal ClipMeshInfo(AnimationClip c)
        {
            clip = c;
            PathBlendshapeCurve = new List<System.Tuple<string, string, AnimationCurve>>();
            EditorCurveBinding[] floatCurves = AnimationUtility.GetCurveBindings(c);
            foreach (var curve in floatCurves)
            {
                if (curve.type == typeof(SkinnedMeshRenderer))
                    PathBlendshapeCurve.Add(new System.Tuple<string, string, AnimationCurve>(curve.path, curve.propertyName.Replace("blendShape.", ""), AnimationUtility.GetEditorCurve(c, curve)));
            }
        }
    }

    internal static class DSHelper
    {
        internal static void AddControls(this VRCExpressionsMenu target, params VRCExpressionsMenu.Control[] newControls)
        {
            if (target.controls == null) target.controls = new List<VRCExpressionsMenu.Control>();

            foreach (var c in newControls)
                target.controls.Add(CopyControl(c));

            EditorUtility.SetDirty(target);
        }

        internal static VRCExpressionsMenu.Control CopyControl(VRCExpressionsMenu.Control source)
        {
            VRCExpressionsMenu.Control newControl = new VRCExpressionsMenu.Control()
            {
                type = source.type,
                value = source.value,
                icon = source.icon,
                name = source.name,
                subMenu = source.subMenu,
                parameter = CopyControlParameter(source.parameter),
            };

            int count = source.subParameters?.Length ?? 0;
            newControl.subParameters = new VRCExpressionsMenu.Control.Parameter[count];

            for (int i = 0; i < count; i++)
            { newControl.subParameters[i] = CopyControlParameter(source.subParameters[i]); }

            return newControl;
        }

        internal static VRCExpressionsMenu.Control.Parameter CopyControlParameter(VRCExpressionsMenu.Control.Parameter source) => new VRCExpressionsMenu.Control.Parameter() { name = source?.name ?? string.Empty };

    }

}
