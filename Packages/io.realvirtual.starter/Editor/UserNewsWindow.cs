// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz

using System;
using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;

namespace realvirtual
{
    public class UserNewsWindow : EditorWindow
    {
        [InitializeOnLoadMethod]
        private static void RegisterNewsCallback()
        {
            LicenseTracker.ShowNewsCallback = ShowWindow;
        }

        /// <summary>Professional sets this to open Login/Downloads/Packages alongside News.</summary>
        public static Action OnShowHubCallback;

        private VisualElement _contentContainer;

        [MenuItem("Tools/realvirtual/User Hub", false, 998)]
        public static void ShowHub()
        {
            ShowWindow();
            OnShowHubCallback?.Invoke();
        }

        public static void ShowWindow()
        {
            GetWindow<UserNewsWindow>("News");
        }

        private void OnDestroy()
        {
            int maxOrder = 0;
            if (LicenseTracker.userResponse?.news != null)
                foreach (var n in LicenseTracker.userResponse.news)
                    if (n.order > maxOrder) maxOrder = n.order;
            if (maxOrder > 0)
                EditorPrefs.SetInt("rv_news_last_seen_order", maxOrder);
        }

        private void CreateGUI()
        {
            EditorUIFactory.AttachStylesheet(rootVisualElement);
            rootVisualElement.AddToClassList("rv-editor-root");

            var scrollView = new ScrollView();
            scrollView.AddToClassList("rv-editor-scrollview");
            rootVisualElement.Add(scrollView);

            _contentContainer = scrollView;
            RebuildContent();
        }

        private void OnFocus() => RebuildContent();

        private void RebuildContent()
        {
            if (_contentContainer == null) return;
            _contentContainer.Clear();

            LicenseTracker.LoadUserResponse();

            if (LicenseTracker.userResponse?.news == null ||
                LicenseTracker.userResponse.news.Length == 0)
            {
                var section = EditorUIFactory.CreateSection("News");
                var label = new Label("No news available.");
                label.AddToClassList("rv-editor-text-label");
                label.style.color = EditorUIFactory.ColorMuted;
                label.style.marginTop = 4;
                section.Add(label);
                _contentContainer.Add(section);
                return;
            }

            var newsSection = EditorUIFactory.CreateSection("News");
            foreach (var news in LicenseTracker.userResponse.news)
            {
                var entry = new VisualElement();
                entry.AddToClassList("rv-editor-section-bg");
                entry.style.marginTop = 4;
                entry.style.marginBottom = 4;
                entry.style.paddingTop = 6;
                entry.style.paddingBottom = 6;
                entry.style.paddingLeft = 8;
                entry.style.paddingRight = 8;

                var titleLabel = new Label(news.title);
                titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                titleLabel.style.marginBottom = 4;
                entry.Add(titleLabel);

                var textLabel = new Label(news.text);
                textLabel.AddToClassList("rv-editor-text-label");
                textLabel.style.whiteSpace = WhiteSpace.Normal;
                entry.Add(textLabel);

                if (!string.IsNullOrEmpty(news.linkTitle) && !string.IsNullOrEmpty(news.link))
                {
                    var linkUrl = news.link;
                    var linkBtn = MaterialIcons.CreateIconButton("open_in_new", news.linkTitle,
                        () => Application.OpenURL(linkUrl));
                    linkBtn.AddToClassList("rv-editor-btn-action");
                    linkBtn.AddToClassList("rv-editor-btn-action-secondary");
                    linkBtn.style.marginTop = 6;
                    entry.Add(linkBtn);
                }

                newsSection.Add(entry);
            }

            _contentContainer.Add(newsSection);
        }
    }
}
