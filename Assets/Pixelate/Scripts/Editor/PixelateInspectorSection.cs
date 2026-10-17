using System;
using UnityEditor;
using UnityEngine;

namespace Pixelate
{
    internal static class PixelateDocumentationLinks
    {
        public const string Root = "https://docs.pixelate.tomblack.ca/";
        public const string Camera = Root + "camera";
        public const string Animation = Root + "animation";
        public const string Preview = Root + "preview";
        public const string ColorPalette = Root + "color-palette";
        public const string NormalMap = Root + "normal-map";
        public const string Output = Root + "output";
        public const string Profiles = Root + "profiles";
        public const string Faq = Root + "faq";
    }

    internal abstract class PixelateInspectorSection
    {
        private static GUIStyle smallTickboxStyle;
        private static GUIStyle contextButtonStyle;
        private static GUIStyle helpButtonStyle;

        protected PixelateInspectorSection(
            string title,
            Func<bool> getExpanded,
            Action<bool> setExpanded,
            Action collapseAll,
            Action expandAll)
        {
            Title = title;
            GetExpanded = getExpanded;
            SetExpanded = setExpanded;
            CollapseAll = collapseAll;
            ExpandAll = expandAll;
        }

        public string Title { get; }
        protected Func<bool> GetExpanded { get; }
        protected Action<bool> SetExpanded { get; }
        protected Action CollapseAll { get; }
        protected Action ExpandAll { get; }

        protected virtual bool HasToggle => false;
        protected virtual bool IsActive => true;
        protected virtual void SetActive(bool active) { }
        protected virtual bool HasSettingsMenu => false;
        protected virtual bool CanPasteSettings => false;
        protected virtual string DocumentationUrl => null;
        protected virtual void CopySettings() { }
        protected virtual void PasteSettings() { }
        protected virtual void ResetSettings() { }
        protected abstract bool DrawBody();

        public bool Draw()
        {
            bool changed = false;
            DrawSplitter();
            bool expanded = GetExpanded();
            bool active = IsActive;
            bool expandedChanged = false;
            DrawHeader(ref expanded, ref active, ref changed, ref expandedChanged);
            if (expandedChanged)
            {
                SetExpanded(expanded);
            }

            if (expanded)
            {
                using (new EditorGUI.DisabledScope(HasToggle && active == false))
                using (new EditorGUILayout.VerticalScope())
                {
                    GUILayout.Space(10f);
                    changed |= DrawBody();
                    GUILayout.Space(10f);
                }
            }

            return changed;
        }

        private void DrawHeader(ref bool expanded, ref bool active, ref bool changed, ref bool expandedChanged)
        {
            Rect backgroundRect = EditorGUI.IndentedRect(GUILayoutUtility.GetRect(1f, 17f));
            Rect fullBackgroundRect = ToFullWidth(backgroundRect);
            DrawHeaderBackground(fullBackgroundRect);

            Rect foldoutRect = backgroundRect;
            foldoutRect.y += 1f;
            foldoutRect.width = 13f;
            foldoutRect.height = 13f;

            Rect toggleRect = backgroundRect;
            toggleRect.x += 16f;
            toggleRect.y += 2f;
            toggleRect.width = 13f;
            toggleRect.height = 13f;

            Rect menuRect = backgroundRect;
            menuRect.x = backgroundRect.xMax - 18f;
            menuRect.y += 1f;
            menuRect.width = 16f;
            menuRect.height = 16f;

            Rect helpRect = menuRect;
            helpRect.x -= 20f;

            Rect labelRect = backgroundRect;
            labelRect.xMin += 32f;
            labelRect.xMax = helpRect.xMin - 3f;

            bool nextExpanded = GUI.Toggle(foldoutRect, expanded, GUIContent.none, EditorStyles.foldout);
            if (nextExpanded != expanded)
            {
                expanded = nextExpanded;
                expandedChanged = true;
            }

            if (HasToggle)
            {
                bool nextActive = GUI.Toggle(toggleRect, active, GUIContent.none, SmallTickboxStyle);
                if (nextActive != active)
                {
                    SetActive(nextActive);
                    active = nextActive;
                    changed = true;
                }
            }
            else
            {
                using (new EditorGUI.DisabledScope(true))
                {
                    GUI.Toggle(toggleRect, true, GUIContent.none, SmallTickboxStyle);
                }
            }

            using (new EditorGUI.DisabledScope(HasToggle && active == false))
            {
                EditorGUI.LabelField(labelRect, Title, EditorStyles.boldLabel);
            }

            string documentationUrl = DocumentationUrl;
            bool hasDocumentation = string.IsNullOrWhiteSpace(documentationUrl) == false;
            using (new EditorGUI.DisabledScope(hasDocumentation == false))
            {
                if (GUI.Button(helpRect, HelpContent, HelpButtonStyle) && hasDocumentation)
                {
                    Application.OpenURL(documentationUrl);
                }
            }

            if (GUI.Button(menuRect, ContextMenuContent, ContextButtonStyle))
            {
                ShowContextMenu(new Vector2(menuRect.x, menuRect.yMax));
            }

            Event currentEvent = Event.current;
            if (currentEvent.type == EventType.MouseDown && fullBackgroundRect.Contains(currentEvent.mousePosition))
            {
                if (currentEvent.button == 1)
                {
                    ShowContextMenu(currentEvent.mousePosition);
                    currentEvent.Use();
                }
                else if (currentEvent.button == 0
                    && !foldoutRect.Contains(currentEvent.mousePosition)
                    && !toggleRect.Contains(currentEvent.mousePosition)
                    && !helpRect.Contains(currentEvent.mousePosition)
                    && !menuRect.Contains(currentEvent.mousePosition))
                {
                    expanded = !expanded;
                    expandedChanged = true;
                    currentEvent.Use();
                }
            }
        }

        private void ShowContextMenu(Vector2 position)
        {
            var menu = new GenericMenu();
            if (HasSettingsMenu)
            {
                menu.AddItem(new GUIContent("Copy Settings"), false, () => CopySettings());
                if (CanPasteSettings)
                {
                    menu.AddItem(new GUIContent("Paste Settings"), false, () => PasteSettings());
                }
                else
                {
                    menu.AddDisabledItem(new GUIContent("Paste Settings"));
                }

                menu.AddSeparator(string.Empty);
                menu.AddItem(new GUIContent("Reset"), false, () => ResetSettings());
                menu.AddSeparator(string.Empty);
            }

            menu.AddItem(new GUIContent("Collapse All"), false, () => CollapseAll?.Invoke());
            menu.AddItem(new GUIContent("Expand All"), false, () => ExpandAll?.Invoke());
            menu.DropDown(new Rect(position, Vector2.zero));
        }

        private static GUIStyle SmallTickboxStyle => smallTickboxStyle ??= new GUIStyle("ShurikenToggle");

        private static GUIStyle ContextButtonStyle
        {
            get
            {
                if (contextButtonStyle == null)
                {
                    contextButtonStyle = new GUIStyle(EditorStyles.iconButton)
                    {
                        padding = new RectOffset(0, 0, 0, 0),
                        margin = new RectOffset(0, 0, 0, 0),
                    };
                }

                return contextButtonStyle;
            }
        }

        private static GUIStyle HelpButtonStyle
        {
            get
            {
                if (helpButtonStyle == null)
                {
                    helpButtonStyle = new GUIStyle(EditorStyles.iconButton)
                    {
                        padding = new RectOffset(0, 0, 0, 0),
                        margin = new RectOffset(0, 0, 0, 0),
                    };
                }

                return helpButtonStyle;
            }
        }

        private static GUIContent HelpContent => EditorGUIUtility.IconContent("_Help", "|Open documentation");
        private static GUIContent ContextMenuContent => EditorGUIUtility.IconContent("_Menu", "|Section options");

        private static void DrawSplitter()
        {
            Rect rect = GUILayoutUtility.GetRect(1f, 1f);
            rect = ToFullWidth(rect);

            if (Event.current.type != EventType.Repaint)
            {
                return;
            }

            EditorGUI.DrawRect(rect, EditorGUIUtility.isProSkin
                ? new Color(0.12f, 0.12f, 0.12f, 1.333f)
                : new Color(0.6f, 0.6f, 0.6f, 1.333f));
        }

        private static void DrawHeaderBackground(Rect rect)
        {
            if (Event.current.type != EventType.Repaint)
            {
                return;
            }

            float tint = EditorGUIUtility.isProSkin ? 0.1f : 1f;
            EditorGUI.DrawRect(rect, new Color(tint, tint, tint, 0.2f));
        }

        private static Rect ToFullWidth(Rect rect)
        {
            rect.xMin = 0f;
            rect.width += 4f;
            return rect;
        }
    }

    internal sealed class DelegatePixelateInspectorSection : PixelateInspectorSection
    {
        private readonly Func<bool> drawBody;
        private readonly Func<bool> hasToggle;
        private readonly Func<bool> getActive;
        private readonly Action<bool> setActive;
        private readonly Func<bool> hasSettingsMenu;
        private readonly Func<bool> canPasteSettings;
        private readonly Action copySettings;
        private readonly Action pasteSettings;
        private readonly Action resetSettings;
        private readonly Func<string> getDocumentationUrl;

        public DelegatePixelateInspectorSection(
            string title,
            Func<bool> getExpanded,
            Action<bool> setExpanded,
            Func<bool> drawBody,
            Action collapseAll,
            Action expandAll,
            Func<bool> hasToggle = null,
            Func<bool> getActive = null,
            Action<bool> setActive = null,
            Func<bool> hasSettingsMenu = null,
            Func<bool> canPasteSettings = null,
            Action copySettings = null,
            Action pasteSettings = null,
            Action resetSettings = null,
            Func<string> getDocumentationUrl = null)
            : base(title, getExpanded, setExpanded, collapseAll, expandAll)
        {
            this.drawBody = drawBody;
            this.hasToggle = hasToggle;
            this.getActive = getActive;
            this.setActive = setActive;
            this.hasSettingsMenu = hasSettingsMenu;
            this.canPasteSettings = canPasteSettings;
            this.copySettings = copySettings;
            this.pasteSettings = pasteSettings;
            this.resetSettings = resetSettings;
            this.getDocumentationUrl = getDocumentationUrl;
        }

        protected override bool HasToggle => hasToggle?.Invoke() == true;
        protected override bool IsActive => getActive?.Invoke() ?? true;
        protected override bool HasSettingsMenu => hasSettingsMenu?.Invoke() == true;
        protected override bool CanPasteSettings => canPasteSettings?.Invoke() == true;
        protected override string DocumentationUrl => getDocumentationUrl?.Invoke();
        protected override void SetActive(bool active) => setActive?.Invoke(active);
        protected override void CopySettings() => copySettings?.Invoke();
        protected override void PasteSettings() => pasteSettings?.Invoke();
        protected override void ResetSettings() => resetSettings?.Invoke();
        protected override bool DrawBody() => drawBody?.Invoke() == true;
    }
}
