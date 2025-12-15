using Mono.Cecil.Cil;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using AtlasAuto.Compiler;
using System.Collections.Generic;
using AtlasAuto.Editor;
using System.Linq;
using System.Reflection;

namespace AtlasAuto
{
    internal struct AutoConfig
    {

    }

    public class VehicleGui : EditorWindow
    {
        static VehicleGui currentGui;

        [MenuItem("Window/AtlasAuto/Open Car Creator")]
        static void OpenGui()
        {
            if (currentGui != null)
            {
                currentGui.Clean();
                currentGui.Close();
            }

            var gui = GetWindow<VehicleGui>();

            gui.titleContent = new GUIContent("Vehicle Editor");

            gui.maxSize = new Vector2(800, 600);
            gui.minSize = new Vector2(600, 400);
            gui.position = new Rect(gui.position.x, gui.position.y, 800, 600);
            currentGui = gui;
        }

        Label integrity;
        Button beginBtn;
        VisualElement parts;
        VisualElement renderImage;
        ScrollView partScroll;

        bool doPreviewRender = false;

        PreviewRenderer renderer;

        struct Wheels
        {
            public ObjectField WheelFL;
            public ObjectField WheelFR;
            public ObjectField WheelRL;
            public ObjectField WheelRR;
        }

        ObjectField modelInput;

        Wheels wheels;
        GameObject selectedModel;
        VisualElement modelTab;
        VisualElement authoringTab;
        bool isRotating = false;

        VisualElement sideBarHolder;
        ScrollView tuningPage;
        VisualElement renderPage;

        Dictionary<string, GameObject> carParts;

        TuningEditorBehaviours tuningBehaviours;

        public void CreateGUI()
        {
            renderer = new PreviewRenderer();
            var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/Editor/CarGui.uxml");
            visualTree.CloneTree(rootVisualElement);

            tuningBehaviours = new TuningEditorBehaviours();


            var root = rootVisualElement;

            integrity = root.Q<Label>("Integrity");
            beginBtn = root.Q<Button>("Begin");

            parts = root.Q<VisualElement>("Parts");
            partScroll = root.Q<ScrollView>("CarParts");

            renderer.InitRenderer(512);

            wheels.WheelFL = root.Q<ObjectField>("WheelFL");
            wheels.WheelFR = root.Q<ObjectField>("WheelFR");
            wheels.WheelRL = root.Q<ObjectField>("WheelRL");
            wheels.WheelRR = root.Q<ObjectField>("WheelRR");

            wheels.WheelFL.RegisterValueChangedCallback(evt => OnWheelInput(wheels.WheelFL, evt.newValue));
            wheels.WheelFR.RegisterValueChangedCallback(evt => OnWheelInput(wheels.WheelFR, evt.newValue));
            wheels.WheelRL.RegisterValueChangedCallback(evt => OnWheelInput(wheels.WheelRL, evt.newValue));
            wheels.WheelRR.RegisterValueChangedCallback(evt => OnWheelInput(wheels.WheelRR, evt.newValue));

            wheels.WheelFL.objectType = typeof(Object);
            wheels.WheelFR.objectType = typeof(Object);
            wheels.WheelRL.objectType = typeof(Object);
            wheels.WheelRR.objectType = typeof(Object);

            modelTab = root.Q<VisualElement>("ModelTab");
            authoringTab = root.Q<VisualElement>("AuthoringTab");

            renderImage = root.Q<VisualElement>("PreviewVid");
            renderImage.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button != 1) return;

                isRotating = true;

                evt.StopPropagation();
                renderImage.CaptureMouse();
            });
            renderImage.RegisterCallback<PointerUpEvent>(evt =>
            {
                if (evt.button != 1) return;

                isRotating = false;
                renderImage.ReleaseMouse();

            });
            renderImage.RegisterCallback<PointerMoveEvent>(evt =>
            {
                if (!isRotating)
                {
                    var pos = evt.localPosition;
                    var screenPos = new Vector2(pos.x / renderImage.resolvedStyle.width, 1f - (pos.y / renderImage.resolvedStyle.height));
                    renderer.CheckForHover(screenPos);
                    return;
                }

                renderer.PositionCameraBy(evt.deltaPosition);
            });

            renderImage.RegisterCallback<WheelEvent>(evt =>
            {
                var delta = evt.delta.y;
                renderer.Zoom(delta);
            });


            modelTab.style.display = DisplayStyle.Flex;
            authoringTab.style.display = DisplayStyle.None;

            modelInput = root.Q<ObjectField>("ModelInputField");
            modelInput.objectType = typeof(GameObject);
            modelInput.RegisterValueChangedCallback(OnModelInputChanged);

            beginBtn.clicked += BeginAuthoring;
            EditorApplication.update += UpdatePreview;

            sideBarHolder = root.Q<VisualElement>("BtnSidebar");
            tuningPage = root.Q<ScrollView>("TuningPage");
            renderPage = root.Q<VisualElement>("PreviewPage");
        }

        void ready(bool showParts = false)
        {
            integrity.text = "Model is ready for authoring!";
            integrity.style.color = new StyleColor(Color.green);
            beginBtn.SetEnabled(true);

            doShowParts(showParts);
        }

        void notReady(bool showParts = true)
        {
            integrity.text = "Model is not quite ready for authoring (missing parts)";
            integrity.style.color = new StyleColor(Color.yellow);
            beginBtn.SetEnabled(false);

            doShowParts(showParts);
        }

        void doShowParts(bool show)
        {
            if (show)
            {
                parts.style.visibility = Visibility.Visible;
                partScroll.style.visibility = Visibility.Visible;
            }
            else
            {
                parts.style.visibility = Visibility.Hidden;
                partScroll.style.visibility = Visibility.Hidden;
            }
        }

        void OnModelInputChanged(ChangeEvent<Object> evt)
        {
            GameObject picked = evt.newValue as GameObject;

            if (picked == null)
            {
                integrity.text = "No model selected";
                integrity.style.color = Color.white;
                beginBtn.SetEnabled(false);
                doShowParts(false);
                return;
            }
            int reason;
            var result = CarCompilerManager.CheckIntegrity(picked, out reason, out carParts);

            if (result)
            {
                ready();
                selectedModel = picked;
            }
            else
            {
                notReady();
                wheels.WheelFR.value = null;
                wheels.WheelFL.value = null;
                wheels.WheelRL.value = null;
                wheels.WheelRR.value = null;
            }
        }

        void OnWheelInput(ObjectField field, Object value)
        {
            var obj = value as GameObject;
            if (obj == null) field.value = null;
            if (HasAllWheelSet())
            {
                ready(true);
                carParts = new Dictionary<string, GameObject>
                {
                    {"Wheel_fl", wheels.WheelFL.value as GameObject},
                    {"Wheel_fr", wheels.WheelFR.value as GameObject},
                    {"Wheel_rl", wheels.WheelRL.value as GameObject},
                    {"Wheel_rr", wheels.WheelRR.value as GameObject}
                };
                selectedModel = modelInput.value as GameObject;
            }
            else
            {
                notReady(true);
            }
        }

        bool HasAllWheelSet()
        {
            if (wheels.WheelFR.value == null) return false;
            if (wheels.WheelFL.value == null) return false;
            if (wheels.WheelRL.value == null) return false;
            if (wheels.WheelRR.value == null) return false;

            return true;
        }

        void BeginAuthoring()
        {
            AuthorTab();
        }

        void AuthorTab()
        {
            authoringTab.style.display = DisplayStyle.Flex;
            modelTab.style.display = DisplayStyle.None;
            doPreviewRender = true;
            renderer.AddRenderObject(selectedModel, carParts);
            renderer.ApplyCosmetics();
            RenderEditorSidebar(sideBarHolder);
        }
        void UpdatePreview()
        {
            if (!doPreviewRender) return;

            var texture = renderer.RenderFrame();

            renderImage.style.backgroundImage = Background.FromRenderTexture(texture);
            renderImage.MarkDirtyRepaint();
        }

        void Clean()
        {
            EditorApplication.update -= UpdatePreview;
            renderer.Clean();
        }

        void OnDestroy()
        {
            Clean();
        }

        (System.Type, object) LoadPage(string page)
        {
            foreach (var f in tuningBehaviours.GetType().GetFields())
            {
                var atr = f.GetCustomAttribute<ExportToSidebarAttribute>();
                if (atr == null) continue;

                if (atr.Name == page) return (f.FieldType, f.GetValue(tuningBehaviours));
            }

            return (null, null);
        }

        string FormatName(string name)
        {
            string formatted = "";
            for (int i = 0; i < name.Length; i++)
            {
                char c = name[i];
                if (i == 0)
                {
                    formatted += char.ToUpper(c);
                }
                else if (char.IsUpper(c))
                {
                    formatted += " " + c;
                }
                else
                {
                    formatted += c;
                }
            }

            return formatted;
        }

        void RenderEditorPage(string pageName)
        {

            tuningPage.Clear();
            tuningPage.parent.style.display = DisplayStyle.Flex;
            renderPage.style.display = DisplayStyle.None;
            var (pageType, pageParent) = LoadPage(pageName);
            Debug.Log(pageType);
            if (pageType == null) return;

            Debug.Log(pageType.GetFields());
            Debug.Log(pageParent);

            foreach (var f in pageType.GetFields())
            {
                var attr = f.GetCustomAttribute<EditorRangeAttribute>();
                var propHolder = new VisualElement();
                propHolder.AddToClassList("editorProperty");
                var labelHolder = new VisualElement();
                labelHolder.AddToClassList("editorPropertyLabel");

                var value = (float)f.GetValue(pageParent);

                var nameLabel = new Label();
                nameLabel.text = FormatName(f.Name);

                var valueLabel = new Label();
                valueLabel.text = value.ToString("F0");

                labelHolder.Add(nameLabel);
                labelHolder.Add(valueLabel);

                var slider = new Slider();
                slider.value = value;
                slider.lowValue = attr.Min;
                slider.highValue = attr.Max;

                slider.RegisterValueChangedCallback(evt =>
                {
                    valueLabel.text = evt.newValue.ToString("F0");
                    f.SetValue(pageParent, evt.newValue);
                });

                propHolder.Add(labelHolder);
                propHolder.Add(slider);

                propHolder.style.overflow = Overflow.Hidden;

                tuningPage.Add(propHolder);
            }
        }

        void RenderEditorSidebar(VisualElement holder)
        {
            foreach (var f in tuningBehaviours.GetType().GetFields())
            {
                var atr = f.GetCustomAttribute<ExportToSidebarAttribute>();
                if (atr == null) continue;

                AddToSidebar(holder, atr.Name);
            }
        }

        void AddToSidebar(VisualElement holder, string name)
        {
            Button btn = new Button();
            btn.text = name;
            btn.clicked += () => RenderEditorPage(name);
            holder.Add(btn);
        }
    }
}